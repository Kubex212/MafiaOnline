import React, { useEffect, useRef, useState } from 'react';
import type {
    LobbyResponse, RoleResponse, SeatInfo, PlayerResponse,
    NightTurnStarted, NightResolvedPayload, LobbyOrderChangedPayload,
    DayStartedPayload, VoteCastPayload, DayResolvedPayload, GameOverPayload, GameConfig, GameStartedPayload, MeResponse
} from './api';
import {
    createLobby, joinLobby, createHubConnection, getLobby,
    startLobby, getMyRole, movePlayerUp, movePlayerDown, shufflePlayers, reconnect,
    updateConfig, getOrCreateSession, getDiscordLoginUrl, getGoogleLoginUrl, getMe, createRematch,
    leaveLobby, kickPlayer
} from './api';
import { HubConnection } from '@microsoft/signalr';
import NightPhase, { type NightPhaseHandle } from './NightPhase';
import DayPhase, { type DayPhaseHandle } from './DayPhase';
import GameOver from './GameOver';
import GameConfigPanel from './GameConfigPanel';
import GameConfigView from './GameConfigView';
import Landing from './Landing';
import ProfileModal from './ProfileModal';
import TopBar from './TopBar';
import { useTranslation } from 'react-i18next';
import { ROLE_SIGILS, roleDescKey, roleDescParams } from './roleText';
import { addLog, clearLog } from './gameLog';
import { playSound, cueYourTurn } from './sound';

type GameView = 'lobby' | 'role-reveal' | 'night' | 'day' | 'gameover';

function whenActivated(): Promise<void> {
    const doc: any = document;
    if (!doc.prerendering) return Promise.resolve();
    return new Promise(resolve => {
        document.addEventListener('prerenderingchange', () => resolve(), { once: true });
    });
}

export default function App() {
    const [nickname, setNickname] = useState('Player' + Math.floor(Math.random() * 1000));
    const [lobby, setLobby] = useState<LobbyResponse | null>(null);
    const [codeToJoin, setCodeToJoin] = useState('');
    const [messages, setMessages] = useState<string[]>([]);
    const [hubStarted, setHubStarted] = useState(false);
    const [myPlayerId, setMyPlayerId] = useState<string | null>(null);
    const [seats, setSeats] = useState<SeatInfo[] | null>(null);
    const [myRole, setMyRole] = useState<RoleResponse | null>(null);
    const [view, setView] = useState<GameView>('lobby');
    const [gameOver, setGameOver] = useState<GameOverPayload | null>(null);
    const [creating, setCreating] = useState(false);
    const [codeCopied, setCodeCopied] = useState(false);
    const [linkCopied, setLinkCopied] = useState(false);
    const [deadIds, setDeadIds] = useState<string[]>([]);
    const [profileOpen, setProfileOpen] = useState(false);
    const [rematchCode, setRematchCode] = useState<string | null>(null);
    const [drunkPresent, setDrunkPresent] = useState<boolean | null>(null);
    const [showRolesPanel, setShowRolesPanel] = useState(false);
    const [sessionReady, setSessionReady] = useState(false);

    // Join-via-link state
    const [me, setMe] = useState<MeResponse | null>(null);
    const [joinCodeFromUrl, setJoinCodeFromUrl] = useState<string | null>(null);
    const [showJoinModal, setShowJoinModal] = useState(false);

    // mark rematchCode as used for TS
    useEffect(() => {
        if (rematchCode) console.debug('Rematch code available', rematchCode);
    }, [rematchCode]);

    const connRef = useRef<HubConnection | null>(null);
    const lobbyRef = useRef<LobbyResponse | null>(null);
    const myPlayerIdRef = useRef<string | null>(null);
    const seatsRef = useRef<SeatInfo[] | null>(null);
    const nightRef = useRef<NightPhaseHandle | null>(null);
    const dayRef = useRef<DayPhaseHandle | null>(null);
    const pendingDayStartedRef = useRef<DayStartedPayload | null>(null);
    const pendingNightStartedRef = useRef<NightTurnStarted | null>(null);
    const pendingNightResolvedRef = useRef<NightResolvedPayload | null>(null);
    const pendingWerewolfAttacksRef = useRef<{ actorId: string; actorName: string; targetId: string; targetName: string }[]>([]);
    const pendingDayResolvedRef = useRef<DayResolvedPayload | null>(null);
    const pendingVoteCastsRef = useRef<VoteCastPayload[]>([]);
    const myRoleRef = useRef<RoleResponse | null>(null);

    const { t } = useTranslation();

    useEffect(() => { lobbyRef.current = lobby; }, [lobby]);
    useEffect(() => { myPlayerIdRef.current = myPlayerId; }, [myPlayerId]);
    useEffect(() => { seatsRef.current = seats; }, [seats]);

    // Prevent background scroll when modal open — must be top-level to keep hook order stable
    useEffect(() => {
        const prev = document.body.style.overflow;
        if (showJoinModal) {
            document.body.style.overflow = 'hidden';
        } else {
            document.body.style.overflow = prev;
        }
        return () => { document.body.style.overflow = prev; };
    }, [showJoinModal]);

    /** Nicknames coming back from the hub -> seat ids, merged into the dead set. */
    const markDeadByNickname = (names: string[]) => {
        if (!names?.length) return;
        const all = seatsRef.current ?? [];
        const ids = names
            .map(n => all.find(s => s.nickname === n)?.id)
            .filter((x): x is string => !!x);
        if (ids.length) setDeadIds(prev => Array.from(new Set([...prev, ...ids])));
    };

    // Update document title reactively based on state instead of during render.
    useEffect(() => {
        if (view === 'role-reveal' && myRole) {
            document.title = `${nickname}: ${t(myRole.name)} — ${t('appTitle')}`;
        } else {
            // document.title = t('appTitle'); // for debugging, show the nickname and role
        }
    }, [view, myRole, nickname, t]);

    // Reflect current phase on <html> so the theme retunes atmospherically.
    useEffect(() => {
        document.documentElement.setAttribute('data-phase', view);
    }, [view]);

    // One chronicle for the whole game — mark each phase change in it.
    useEffect(() => {
        if (view === 'night') { addLog(t('nightFalls'), 'night'); playSound('night-start'); }
        else if (view === 'day') { addLog(t('dayBreaks'), 'day'); playSound('day-start'); }
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [view]);

    // Close roles panel when leaving night view
    useEffect(() => { if (view !== 'night') setShowRolesPanel(false); }, [view]);

    // ── SignalR wiring ────────────────────────────────────────────────────
    useEffect(() => {
        let cancelled = false;

        (async () => {
            // Must have a token before we build the hub URL (token is embedded in it).
            try {
                await whenActivated();       // <-- wait until this is the real, visible document
                if (cancelled) return;
                await getOrCreateSession();
                if (cancelled) return;
 
                setSessionReady(true);

            } catch (err) {
                console.error('Session init failed', err);
            }
            if (cancelled) return;

            try {
                const meResp = await getMe();
                setMe(meResp);
                console.log('Loaded profile', JSON.stringify(meResp));
                if (meResp.preferredNickname) setNickname(meResp.preferredNickname);
            }
            catch (err) {
                console.error('Profile load failed', err);
            }

            // If the user navigated to /join/{code}, show join modal.
            try {
                const m = window.location.pathname.match(/\/join\/([A-Za-z0-9]+)/i);
                if (m && m[1]) {
                    const code = m[1].toUpperCase();
                    setJoinCodeFromUrl(code);
                    setCodeToJoin(code);
                    setShowJoinModal(true);
                }
            } catch { /* ignore */ }
            
            const conn = createHubConnection();
            connRef.current = conn;

            conn.on('PlayerJoined', async (nick: string) => {
                setMessages(m => [...m, t('playerJoined', { nick })]);
                const code = lobbyRef.current?.code;
                if (code) {
                    try { setLobby(await getLobby(code)); } catch { /* ignore */ }
                }
            });

            conn.on('PlayerLeft', async (nick: string) => {
                setMessages(m => [...m, t('playerLeft', { nick })]);
                const code = lobbyRef.current?.code;
                if (code) {
                    try { setLobby(await getLobby(code)); } catch (e) {
                        // If lobby was deleted, clear local state
                        sessionStorage.removeItem('lobbyCode');
                        setLobby(null);
                        setMyPlayerId(null);
                    }
                }
                // If I was the one who left/kicked, ensure local state cleared
                if (nick === nickname) {
                    sessionStorage.removeItem('lobbyCode');
                    setLobby(null);
                    setMyPlayerId(null);
                }
            });

            conn.on('PlayerKicked', async (nick: string) => {
                setMessages(m => [...m, t('playerKickedMessage', { nick })]);
                const code = lobbyRef.current?.code;
                if (code) {
                    try { setLobby(await getLobby(code)); } catch (e) {
                        sessionStorage.removeItem('lobbyCode');
                        setLobby(null);
                        setMyPlayerId(null);
                    }
                }
                if (nick === nickname) {
                    // I was kicked
                    sessionStorage.removeItem('lobbyCode');
                    setLobby(null);
                    setMyPlayerId(null);
                    alert(t('youWereKicked'));
                }
            });

            conn.on('LobbyOrderChanged', (ordered: LobbyOrderChangedPayload) => {
                const prev = lobbyRef.current;
                if (!prev) return;
                const newPlayers: PlayerResponse[] = ordered.map(o => ({
                    id: o.id, nickname: o.nickname, isHost: o.isHost,
                    seatIndex: -1, lobbyPosition: o.lobbyPosition,
                }));
                setLobby({ code: prev.code, started: prev.started, players: newPlayers, config: prev.config });
            });

            conn.on('ConfigUpdated', (cfg: GameConfig) => {
                const prev = lobbyRef.current;
                if (!prev) return;
                setLobby({ ...prev, config: cfg });
            });

            conn.on('GameStarted', async (payload: GameStartedPayload) => {
                if (view !== 'lobby') return; // ignore if already in a phase

                const seatList: SeatInfo[] = payload?.seats ?? [];
                const drunkFlag: boolean = (payload?.drunkPresent ?? false);
                const sorted = [...seatList].sort((a, b) => a.seatIndex - b.seatIndex);
                setSeats(sorted as SeatInfo[]);
                seatsRef.current = sorted as SeatInfo[];
                setDeadIds([]);
                clearLog();
                setDrunkPresent(drunkFlag);
                const code = lobbyRef.current?.code;
                if (code) {
                    try {
                        const myRoleResponse = await getMyRole(code);
                        setMyRole(myRoleResponse);
                        myRoleRef.current = myRoleResponse;
                        setView('role-reveal');
                        playSound('game-start');
                        setTimeout(() => setView('night'), 8000);
                    } catch { /* ignore */ }
                }
            });

            conn.on('NightTurnStarted', (turn: NightTurnStarted) => {
                // Ensure the NightPhase component receives the turn even if it
                // isn't mounted yet (mobile browsers may be slow to render).
                pendingNightStartedRef.current = turn;
                setView('night');
                if (turn.activePlayerId && turn.activePlayerId === myPlayerIdRef.current) cueYourTurn();
                // If already mounted, deliver immediately and clear the pending slot.
                if (nightRef.current) {
                    nightRef.current.onTurnStarted(turn);
                    pendingNightStartedRef.current = null;
                }
            });

            conn.on('NightResolved', (payload: NightResolvedPayload) => {
                markDeadByNickname(payload.killed);
                playSound('night-resolved');
                if (nightRef.current) {
                    nightRef.current.onNightResolved(payload);
                } else {
                    pendingNightResolvedRef.current = payload;
                }
            });

            conn.on('DayStarted', (payload: DayStartedPayload) => {
                // Authoritative alive list — anyone missing from it is dead.
                const alive = new Set(payload.alivePlayers.map(p => p.id));
                const all = seatsRef.current ?? [];
                setDeadIds(all.filter(s => !alive.has(s.id)).map(s => s.id));

                pendingDayStartedRef.current = payload;
                setView('day');
            });

            conn.on('VoteCast', (payload: VoteCastPayload) => {
                if (dayRef.current) {
                    dayRef.current.onVoteCast(payload);
                } else {
                    pendingVoteCastsRef.current = [...(pendingVoteCastsRef.current ?? []), payload];
                }
            });

            conn.on('DayResolved', (payload: DayResolvedPayload) => {
                markDeadByNickname(payload.banished);
                playSound('day-resolved');
                if (dayRef.current) {
                    dayRef.current.onDayResolved(payload);
                } else {
                    pendingDayResolvedRef.current = payload;
                }
            });

            conn.on('GameOver', (payload: GameOverPayload) => {
                setGameOver(payload);
                setView('gameover');
            });

            conn.on('WerewolfAttackCast', (payload: { actorId: string; actorName: string, targetId: string; targetName: string }) => {
                // Only notify local werewolf players (client can inspect role)
                if (myRoleRef.current?.teamId === 1) {
                    // Draw the kill vote as an arrow on the table (werewolves only).
                    if (nightRef.current) {
                        nightRef.current.onWerewolfAttackCast({ actorId: payload.actorId, targetId: payload.targetId });
                        if (payload.actorId !== myPlayerIdRef.current) {
                            nightRef.current.addLog(t('werewolfXAttackedY', { attacker: payload.actorName, target: payload.targetName }));
                        }
                    } else {
                        pendingWerewolfAttacksRef.current = [...(pendingWerewolfAttacksRef.current ?? []), payload];
                    }
                }
            });

            conn.on('RematchCreated', (newCode: string) => {
                setMessages(m => [...m, t('rematchCreated', { code: newCode })]);
                setRematchCode(newCode);
            });

            await conn.start();
            if (cancelled) { conn.stop(); return; }
            setHubStarted(true);

            const storedLobbyCode = sessionStorage.getItem('lobbyCode');
            if (!storedLobbyCode) return;

            try {
                const data = await reconnect(storedLobbyCode);

                await conn.invoke('JoinLobby', storedLobbyCode);

                if (data.gamePhase === 'day' && data.day) {
                    // Use the pending/ref pattern instead of runAfterCommit
                    pendingDayStartedRef.current = {
                        durationSeconds: data.day.durationSeconds,
                        showVoteTargets: data.day.showVoteTargets,
                        alivePlayers: data.day.alivePlayers,
                        enshrouded: data.day.enshrouded ?? [],
                        asleep: data.day.asleep ?? [],
                    };

                    // Replay votes once DayPhase mounts
                    if (data.day.votes) {
                        const total = Object.keys(data.day.votes).length;
                        pendingVoteCastsRef.current = Object.entries(data.day.votes).map(([voterId, candidateId]) => ({ voterId, candidateId, totalVotesCast: total }));
                    }

                    setSeats((data.seats ?? null) as SeatInfo[] | null);
                    setView(data.gamePhase);
                    setMessages([t('reconnected')]);

                } else if (data.gamePhase === 'night' && data.night) {
                    // Use the pending/ref pattern instead of runAfterCommit - first set pending then set view so NightPhase can pick it up on mount
                    pendingNightStartedRef.current = {
                        activePlayerId: data.night.activePlayerId ?? '',
                        activeSeatIndex: data.night.activeSeatIndex,
                        hasAbility: data.night.hasAbility,
                        timeoutSeconds: data.night.remainingSeconds ?? 1000,
                        minTargets: undefined,
                        maxTargets: undefined,
                    } as NightTurnStarted;

                    setSeats((data.seats ?? null) as SeatInfo[] | null);
                    setView(data.gamePhase);
                    setMessages([t('reconnected')]);
                } else if (data.gamePhase === 'gameover') {
                    setView(data.gamePhase);
                    setGameOver(data.gameOver ?? null);
                    setRematchCode(data.gameOver?.rematchCode ?? null);
                }


                setLobby(data.lobby);
                setMyPlayerId(data.myPlayerId);
                setMyRole(data.myRole ?? null);

                const alivePlayersSeats = (data.gamePhase === 'day' ? data.day?.alivePlayers : data.night?.alivePlayers) ?? [];
                setDeadIds((data.seats ?? []).filter(s => !alivePlayersSeats.some(p => p.id === s.id)).map(s => s.id));

            } catch (e) {
                sessionStorage.removeItem('lobbyCode');
                console.error('Reconnection failed:', e);
            }
        })();

        return () => {
            cancelled = true;
            connRef.current?.stop().catch(() => { });
            connRef.current = null;
        };
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, []);

    async function joinViaLinkConfirm() {
        const code = joinCodeFromUrl ?? codeToJoin;
        if (!code) return;
        try {
            const nick = (nickname || '').trim();
            if (!nick) { alert('Please enter a nickname'); return; }
            const res = await joinLobby(code, nick);
            const mePlayer = res.players.find(p => p.nickname.toLowerCase() === nick.toLowerCase());
            if (mePlayer) setMyPlayerId(mePlayer.id);
            setLobby(res);
            sessionStorage.setItem('lobbyCode', res.code);
            setMessages([t('joinedLobby', { code: res.code })]);
            playSound('join-lobby');
            setShowJoinModal(false);
            try { await joinHubGroup(res.code); } catch { /* ignore */ }
        } catch (e) {
            alert(String(e));
        }
    }

    function joinViaLinkCancel() {
        setShowJoinModal(false);
        setJoinCodeFromUrl(null);
        try { window.history.replaceState({}, '', window.location.pathname.replace(/\/join\/[A-Za-z0-9]+/i, '/')); } catch { }
    }

    async function joinHubGroup(code: string) {
        const conn = connRef.current;
        if (!conn) return;
        if (conn.state !== 'Connected') { await conn.start(); setHubStarted(true); }
            await conn.invoke('JoinLobby', code);
    }

    async function onCreate() {
        try {
            setCreating(true);
            const res = await createLobby(nickname);
            const me = res.players.find(p => p.isHost)!;
            setMyPlayerId(me.id);
            setLobby(res);
            sessionStorage.setItem('lobbyCode', res.code);
            setMessages([t('lobbyCreated', { code: res.code })]);
            playSound('create-lobby');
            await joinHubGroup(res.code);
        } catch (e) { alert(String(e)); }
        finally { setCreating(false); }
    }

    async function onJoin() {
        try {
            const res = await joinLobby(codeToJoin, nickname);
            const me = res.players.find(p => p.nickname.toLowerCase() === nickname.toLowerCase())!;
            setMyPlayerId(me.id);
            setLobby(res);
            sessionStorage.setItem('lobbyCode', res.code);
            setMessages([t('joinedLobby', { code: res.code })]);
            playSound('join-lobby');
            await joinHubGroup(res.code);
        } catch (e) { alert(String(e)); }
    }

    async function onStart() {
        if (!lobby) return;
        try { await startLobby(lobby.code); }
        catch (e) { alert(String(e)); }
    }

    async function withLobby(fn: (code: string) => Promise<LobbyResponse>) {
        if (!lobby) return;
        try { setLobby(await fn(lobby.code)); }
        catch (e) { alert(String(e)); }
    }

    async function onConfigChange(config: GameConfig) {
        if (!lobby) return;
        try { await updateConfig(lobby.code, config); }
        catch (e) { alert(String(e)); }
    }

    async function copyCode() {
        if (!lobby) return;
        try {
            await navigator.clipboard.writeText(lobby.code);
            setCodeCopied(true);
            setTimeout(() => setCodeCopied(false), 1500);
        } catch { /* ignore */ }
    }

    async function copyJoinLink() {
        if (!lobby) return;
        try {
            const url = `${window.location.origin}/join/${lobby.code}`;
            await navigator.clipboard.writeText(url);
            setLinkCopied(true);
            setTimeout(() => setLinkCopied(false), 1500);
        } catch { /* ignore */ }
    }

    // Leave lobby handler
    async function onLeaveLobby() {
        if (!lobby) return;
        if (!confirm(t('confirmLeave') ?? 'Leave lobby?')) return;
        try {
            await leaveLobby(lobby.code);
            sessionStorage.removeItem('lobbyCode');
            setLobby(null);
            setMyPlayerId(null);
            setSeats(null);
            setMyRole(null);
            setView('lobby');
            setMessages([t('leftLobby')]);
        } catch (e) { alert(String(e)); }
    }

    // Kick player handler (host only)
    async function onKickPlayer(targetId: string) {
        if (!lobby) return;
        const target = lobby.players.find(p => p.id === targetId);
        if (!target) return;
        if (!confirm(t('confirmKick', { name: target.nickname }) ?? `Kick ${target.nickname}?`)) return;
        try {
            await kickPlayer(lobby.code, targetId);
            try { const refreshed = await getLobby(lobby.code); setLobby(refreshed); } catch { /* ignore, SignalR will update */ }
            setMessages(m => [...m, t('playerKicked')]);
        } catch (e) { alert(String(e)); }
    }

    const isHost = lobby?.players.find(p => p.id === myPlayerId)?.isHost ?? false;

    // Fellow werewolves — the backend only sends this to werewolves, so nobody
    // else can ever see the markers.
    const otherWerewolvesSeats: SeatInfo[] = (() => {
        if (!myRole || myRole.teamId !== 1 || !seats) return [];
        const raw = (myRole.otherWerewolvesIds ?? []) as string[];
        const found = raw.map(id => seats.find(s => s.id === id)).filter((s): s is SeatInfo => !!s && s.id !== myPlayerId);
        return Array.from(new Map(found.map(s => [s.id, s])).values());
    })();
    const otherWerewolvesIds = otherWerewolvesSeats.map(s => s.id);

    const canStart = isHost && (lobby?.players.length ?? 0) >= 4 && !lobby?.started;
    const iAmDead = !!myPlayerId && deadIds.includes(myPlayerId);
    const spectatorClass = iAmDead ? ' is-spectating' : '';
    const SpectatorBanner = () => iAmDead ? (
        <div className="spectator-banner" role="status">
            <span aria-hidden>💀</span>
            <span className="spectator-banner__text">
                <strong>{t('youAreDead')}</strong>
                <small>{t('youAreWatching')}</small>
            </span>
        </div>
    ) : null;
    const orderedPlayers = lobby
        ? [...lobby.players].sort((a, b) => a.lobbyPosition - b.lobbyPosition)
        : [];

    // Handlers for rematch creation/joining
    async function createRematchHandler() {
        // Use stored lobby code (the lobby that just finished)
        const storedOld = sessionStorage.getItem('lobbyCode');
        if (!storedOld) { alert('No lobby context'); return; }
        try {
            const newLobby = await createRematch(storedOld);
            // Set lobby state to the newly created lobby and set current player id to host
            setLobby(newLobby);
            const hostPlayer = newLobby.players.find(p => p.isHost);
            if (hostPlayer) setMyPlayerId(hostPlayer.id);
            setMessages(m => [...m, t('rematchCreated', { code: newLobby.code })]);
            setRematchCode(null);
            setView('lobby');
            // Join hub group for the new lobby so the host immediately receives hub events
            try { await joinHubGroup(newLobby.code); } catch (e) { alert(String(e)); }
        } catch (e) {
            alert(String(e));
        }
    }

    async function joinRematchHandler() {
        // rematchCode must have been signalled via SignalR to clients
        const code = rematchCode;
        if (!code) { alert(t('waitForRematch')); return; }
        try {
            // Join the rematch lobby as a normal player using current nickname
            const res = await joinLobby(code, nickname);
            const me = res.players.find(p => p.nickname.toLowerCase() === nickname.toLowerCase())!;
            setMyPlayerId(me.id);
            setLobby(res);
            setRematchCode(null);
            setView('lobby');
            setMessages(m => [...m, t('joinedLobby', { code: res.code })]);
            // Join SignalR group
            try { await joinHubGroup(res.code); } catch (e) { alert(String(e)); }
        } catch (e) {
            alert(String(e));
        }
    }

    // ── GAME OVER ───────────────────────────────────────────────────────
    if (view === 'gameover' && gameOver && myRole) {
        // Do not clear session here — keep stored lobbyCode so rematch works
        return (
            <div className="phase"><div className="phase__inner">
                <GameOver payload={gameOver} myRoleLabel={t(myRole.name)} myRole={myRole.name} myTeam={myRole.teamId}
                    isHost={isHost} rematchCode={rematchCode}
                    onCreateRematch={createRematchHandler} onJoinRematch={joinRematchHandler} />
            </div></div>
        );
    }

    // ── ROLE REVEAL ──────────────────────────────────────────────────────
    if (view === 'role-reveal' && myRole) {
        const teamColorVar = myRole.teamId === 1 ? 'var(--wolf)'
            : myRole.teamId === 2 ? 'var(--neutral)'
                : 'var(--town-2)';
        const sigil = ROLE_SIGILS[myRole.name] ?? '🌾';
        return (
            <div className="phase"><div className="phase__inner reveal">
                <div className="reveal__eyebrow">{t('yourRoleIs')}</div>
                <div className="reveal__sigil" style={{ color: teamColorVar }}>{sigil}</div>
                <div className="reveal__role" style={{ color: teamColorVar, textShadow: `0 0 40px ${teamColorVar}` }}>
                    {t(myRole.name)}
                </div>
                <div className="reveal__team">{t(myRole.teamName)}</div>
                {otherWerewolvesSeats.length > 0 && (
                    <div className="reveal__pack">
                        <span className="reveal__pack-label">🐺 {t('yourPack')}</span>
                        <span className="reveal__pack-names">
                            {otherWerewolvesSeats.map(s => s.nickname).join(' · ')}
                        </span>
                    </div>
                )}
                {myRole.alibi && (
                    <div className="reveal__alibi">
                        <span className="reveal__alibi-label">{t('alibiShort')}</span>
                        <span className="reveal__alibi-value">{t(myRole.alibi)}</span>
                    </div>
                )}
                <p className="reveal__desc">
                    {t(roleDescKey(myRole.name, 'Short'), roleDescParams(lobby?.config))}
                </p>
                {drunkPresent && (
                    <div style={{ color: 'var(--text-dim)', fontSize: 13, marginTop: 8 }}>
                        {t('drunkPresent')}
                    </div>
                )}
                <div style={{ color: 'var(--text-dim)', fontSize: 13, marginTop: 12, fontStyle: 'italic' }}>
                    {t('nightBeginsSoon')}
                </div>
            </div></div>
        );
    }

    // ── NIGHT PHASE ──────────────────────────────────────────────────────
    if (view === 'night' && seats && myPlayerId && myRole) {
        return (
            <div className={`phase${spectatorClass}`}><div className="phase__inner">
                <div className="phase-veil phase-veil--night" key="veil-night" aria-hidden />
                <TopBar showStatus={false} compact onToggleRoles={() => setShowRolesPanel(s => !s)} showRoles={showRolesPanel} />
                <SpectatorBanner />
                <h2 className="phase__title">
                    🌙 {t('nightPhase')}
                    <small>Lobby <span className="phase__code">{lobby?.code}</span></small>
                </h2>
                <NightPhase ref={instance => {
                     nightRef.current = instance;
                     if (instance && pendingNightStartedRef.current) {
                        instance.onTurnStarted(pendingNightStartedRef.current);
                        pendingNightStartedRef.current = null;
                     }

                     // deliver any queued werewolf attack notifications
                     if (instance && pendingWerewolfAttacksRef.current?.length) {
                        pendingWerewolfAttacksRef.current.forEach(a => {
                            instance.onWerewolfAttackCast({ actorId: a.actorId, targetId: a.targetId });
                            if (a.actorId !== myPlayerIdRef.current) {
                                instance.addLog(t('werewolfXAttackedY', { attacker: a.actorName, target: a.targetName }));
                            }
                        });
                        pendingWerewolfAttacksRef.current = [];
                     }

                     // deliver pending night-resolved if present
                     if (instance && pendingNightResolvedRef.current) {
                         instance.onNightResolved(pendingNightResolvedRef.current);
                         pendingNightResolvedRef.current = null;
                     }
                }}
                     lobbyCode={lobby!.code}
                     seats={seats}
                     myPlayerId={myPlayerId}
                     myRole={myRole}
                     deadIds={deadIds}
                     otherWerewolvesIds={otherWerewolvesIds}
                     config={lobby?.config}
                     showRoles={showRolesPanel} />
             </div></div>
         );
     }

    // ── DAY PHASE ────────────────────────────────────────────────────────
    if (view === 'day' && seats && myPlayerId && myRole) {
        return (
            <div className={`phase${spectatorClass}`}><div className="phase__inner">
                <div className="phase-veil phase-veil--day" key="veil-day" aria-hidden />
                <TopBar showStatus={false} compact />
                <SpectatorBanner />
                <h2 className="phase__title">
                    ☀ {t('dayPhase')}
                    <small>Lobby <span className="phase__code">{lobby?.code}</span></small>
                </h2>
                <DayPhase
                    ref={instance => {
                        dayRef.current = instance;

                        if (instance && pendingDayStartedRef.current) {
                            instance.onDayStarted(pendingDayStartedRef.current);
                            pendingDayStartedRef.current = null;
                        }

                        // replay any votes that arrived while DayPhase wasn't mounted
                        if (instance && pendingVoteCastsRef.current?.length) {
                            pendingVoteCastsRef.current.forEach(v => instance.onVoteCast(v));
                            pendingVoteCastsRef.current = [];
                        }

                        // deliver pending day-resolved if present
                        if (instance && pendingDayResolvedRef.current) {
                            instance.onDayResolved(pendingDayResolvedRef.current);
                            pendingDayResolvedRef.current = null;
                        }
                    }}
                    lobbyCode={lobby!.code}
                    allSeats={seats}
                    myPlayerId={myPlayerId}
                    myRole={myRole}
                    deadIds={deadIds}
                    otherWerewolvesIds={otherWerewolvesIds}
                    config={lobby?.config}
                />
            </div></div>
        );
    }

    // ── LOBBY VIEW ───────────────────────────────────────────────────────

    // Define JoinModal here (after all hooks) so it's created consistently on every render
    const JoinModal = ({ show, code, me, nickname, setNickname, onConfirm, onCancel }: {
        show: boolean; code: string | null; me: MeResponse | null; nickname: string; setNickname: (n: string) => void;
        onConfirm: () => Promise<void>; onCancel: () => void;
    }) => {
        if (!show) return null;
        const overlayStyle: React.CSSProperties = {
            position: 'fixed', top: 0, left: 0, right: 0, bottom: 0,
            background: 'rgba(0,0,0,0.45)', display: 'flex', alignItems: 'center', justifyContent: 'center',
            zIndex: 9999,
        };
        const modalStyle: React.CSSProperties = { width: 480, maxWidth: '92%', borderRadius: 8, padding: 20 };
        const c = code ?? '';
        return (
            <div style={overlayStyle} role="dialog" aria-modal="true">
                <div style={modalStyle} className="card card--parchment" aria-labelledby="join-modal-title">
                    <h3 id="join-modal-title" style={{ marginTop: 0 }}>{t('join')} {c}</h3>
                    {me && !me.isAnonymous ? (
                        <div>
                            <p style={{ color: 'var(--text, #222)', marginTop: 6 }}>{t('joinLobbyAs', { code: c, name: me.preferredNickname ?? t('you') })}</p>
                            <label className="field">
                                <span className="field__label">{t('nickname')}</span>
                                <input className="input" value={nickname}
                                    onChange={e => setNickname(e.target.value)} maxLength={16} />
                            </label>
                            <div style={{ display: 'flex', gap: 8, marginTop: 8 }}>
                                <button className="btn btn--primary" onClick={onConfirm}>{t('join')}</button>
                                <button className="btn" onClick={onCancel}>{t('cancel')}</button>
                            </div>
                        </div>
                    ) : (
                        <div>
                            <p>{t('orLoginWith')}</p>
                            <div style={{ display: 'flex', gap: 8 }}>
                                <a className="btn btn--discord" href={getDiscordLoginUrl(`/join/${c}`)}>{t('loginWithDiscord')}</a>
                                <a className="btn btn--google" href={getGoogleLoginUrl(`/join/${c}`)}>{t('loginWithGoogle')}</a>
                            </div>
                            <div style={{ marginTop: 12 }}>
                                <label className="field">
                                    <span className="field__label">{t('nickname')}</span>
                                    <input className="input" value={nickname}
                                        onChange={e => setNickname(e.target.value)} maxLength={16} />
                                </label>
                                <div style={{ display: 'flex', gap: 8, marginTop: 8 }}>
                                    <button className="btn btn--primary" onClick={onConfirm}>{t('join')}</button>
                                    <button className="btn" onClick={onCancel}>{t('cancel')}</button>
                                </div>
                            </div>
                        </div>
                    )}
                </div>
            </div>
        );
    };

    return (
        <div className="phase">
            <div className="phase__inner">
                {!lobby && (
                    <>
                        <TopBar connected={hubStarted} onProfile={() => setProfileOpen(true)} />

                        <header className="lobby-hero">
                            <div className="lobby-hero__eyebrow">◆ {t('villageOfSecrets')} ◆</div>
                            <h1 className="lobby-hero__title">{t('appTitle')}</h1>
                            <p className="lobby-hero__tagline">{t('tagline')}</p>
                        </header>

                        <div className="join-form">
                            <label className="field">
                                <span className="field__label">{t('nickname')}</span>
                                <input className="input" value={nickname}
                                    onChange={e => setNickname(e.target.value)}
                                    disabled={creating}
                                    maxLength={16} />
                            </label>

                            <button className="btn btn--primary btn--block"
                                onClick={onCreate} disabled={creating || !nickname.trim() || !sessionReady}>
                                {creating ? (
                                    <>
                                        <svg width="16" height="16" viewBox="0 0 16 16" aria-hidden>
                                            <g fill="none" stroke="currentColor" strokeWidth="2">
                                                <circle cx="8" cy="8" r="6" strokeOpacity="0.25" />
                                                <path d="M14 8a6 6 0 0 0-6-6" strokeLinecap="round">
                                                    <animateTransform attributeName="transform" type="rotate"
                                                        from="0 8 8" to="360 8 8" dur="0.9s" repeatCount="indefinite" />
                                                </path>
                                            </g>
                                        </svg>
                                        {t('creating')}
                                    </>
                                ) : t('createLobby')}
                            </button>

                            <div className="join-form__divider">{t('orJoin')}</div>

                            <div className="join-form__row">
                                <input className="input input--code"
                                    value={codeToJoin}
                                    placeholder="XXXXXX"
                                    maxLength={8}
                                    onChange={e => setCodeToJoin(e.target.value.toUpperCase())} />
                                <button className="btn" onClick={onJoin}
                                    disabled={!codeToJoin.trim() || !nickname.trim() || !sessionReady}>
                                    {t('join')}
                                </button>
                            </div>

                            <div className="join-form__divider">{t('orLoginWith')}</div>

                            <a className="btn btn--discord btn--block" href={getDiscordLoginUrl(window.location.pathname)}>
                                <svg width="20" height="20" viewBox="0 0 127.14 96.36" aria-hidden fill="currentColor">
                                    <path d="M107.7,8.07A105.15,105.15,0,0,0,81.47,0a72.06,72.06,0,0,0-3.36,6.83A97.68,97.68,0,0,0,49,6.83,72.37,72.37,0,0,0,45.64,0,105.89,105.89,0,0,0,19.39,8.09C2.79,32.65-1.71,56.6.54,80.21h0A105.73,105.73,0,0,0,32.71,96.36,77.7,77.7,0,0,0,39.6,85.25a68.42,68.42,0,0,1-10.85-5.18c.91-.66,1.8-1.34,2.66-2a75.57,75.57,0,0,0,64.32,0c.87.71,1.76,1.39,2.66,2a68.68,68.68,0,0,1-10.87,5.19,77,77,0,0,0,6.89,11.1A105.25,105.25,0,0,0,126.6,80.22h0C129.24,52.84,122.09,29.11,107.7,8.07ZM42.45,65.69C36.18,65.69,31,60,31,53s5-12.74,11.43-12.74S54,46,53.89,53,48.84,65.69,42.45,65.69Zm42.24,0C78.41,65.69,73.25,60,73.25,53s5-12.74,11.44-12.74S96.23,46,96.12,53,91.08,65.69,84.69,65.69Z"/>
                                </svg>
                                {t('loginWithDiscord')}
                            </a>

                            <a className="btn btn--google btn--block" href={getGoogleLoginUrl(window.location.pathname)}>
                                <svg width="18" height="18" viewBox="0 0 48 48" aria-hidden>
                                    <path fill="#EA4335" d="M24 9.5c3.5 0 6.6 1.2 9 3.5l6.8-6.8C35.6 2.4 30.2 0 24 0 14.6 0 6.5 5.4 2.6 13.2l7.9 6.2C12.4 13.4 17.7 9.5 24 9.5z" />
                                    <path fill="#4285F4" d="M46.1 24.5c0-1.6-.1-2.8-.4-4.1H24v8.1h12.6c-.3 2.1-1.6 5.2-4.6 7.3l7.7 6c4.5-4.2 6.4-10.1 6.4-17.3z" />
                                    <path fill="#FBBC05" d="M10.5 28.6A14.5 14.5 0 0 1 9.7 24c0-1.6.3-3.2.8-4.6l-7.9-6.2A24 24 0 0 0 0 24c0 3.9.9 7.5 2.6 10.8l7.9-6.2z" />
                                    <path fill="#34A853" d="M24 48c6.5 0 11.9-2.1 15.7-5.8l-7.7-6c-2.1 1.4-4.8 2.4-8 2.4-6.3 0-11.6-3.9-13.5-9.4l-7.9 6.2C6.5 42.6 14.6 48 24 48z" />
                                </svg>
                                {t('loginWithGoogle')}
                            </a>

                        </div>

                        <Landing />

                        <ProfileModal
                            open={profileOpen}
                            onClose={() => setProfileOpen(false)}
                            onSaved={name => { if (name) setNickname(name); }}
                        />
                    </>
                )}

                {lobby && (
                    <div className="lobby-room">
                        <TopBar showStatus={false} compact onProfile={() => setProfileOpen(true)} />
                        <div className="lobby-room__header">
                            <div>
                                <button className="lobby-room__code" onClick={copyCode}
                                    title={t('copyCode')}>
                                    {lobby.code}
                                </button>
                                <button className="btn" onClick={copyJoinLink} style={{ marginLeft: 8 }} title={t('copyLink')}>
                                    🔗
                                </button>
                                <div className="lobby-room__code-hint">
                                    {codeCopied ? '✓ ' + t('codeCopied') : linkCopied ? '✓ ' + t('codeCopied') : t('tapToCopy')}
                                </div>
                            </div>
                            <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
                                <div className={`status-dot ${hubStarted ? '' : 'status-dot--off'}`}>
                                    {hubStarted ? t('connected') : t('connecting')}
                                </div>
                                <button className="btn" onClick={onLeaveLobby} title={t('leaveLobby')}>
                                    {t('leave')}
                                </button>
                            </div>
                        </div>

                        <div className="card card--parchment">
                            <div style={{
                                display: 'flex', justifyContent: 'space-between',
                                alignItems: 'center', marginBottom: 10
                            }}>
                                <span className="field__label">
                                    {t('seatOrder')} {!isHost && `— ${t('setByHost')}`}
                                </span>
                                {isHost && (
                                    <button className="icon-btn"
                                        onClick={() => withLobby((c) => shufflePlayers(c))}
                                        title={t('shuffle')}>
                                        🎲 {t('shuffle')}
                                    </button>
                                )}
                            </div>

                            <ul className="player-list">
                                {orderedPlayers.map((p, i) => (
                                    <li key={p.id}
                                        className={`player-list__item ${p.id === myPlayerId ? 'player-list__item--me' : ''}`}>
                                        <span className="seat-num">{i + 1}</span>
                                        <span className="player-name">{p.nickname}</span>
                                        {p.isHost && <span className="lobby-tag">{t('host')}</span>}
                                        {p.id === myPlayerId && <span className="lobby-tag tag--you">{t('you')}</span>}
                                        {isHost && !lobby.started && p.id !== myPlayerId && (
                                            <button className="icon-btn" onClick={() => onKickPlayer(p.id)} title={t('kick')}>
                                                ❌
                                            </button>
                                        )}
                                        {isHost && !lobby.started && (
                                            <span className="move-player-arrows" style={{ display: 'flex', gap: 3, marginLeft: 4 }}>
                                                <button className="icon-btn"
                                                    disabled={i === 0}
                                                    onClick={() => withLobby((c) => movePlayerUp(c, p.id))}
                                                    title={t('moveUp')}>▲</button>
                                                <button className="icon-btn"
                                                    disabled={i === orderedPlayers.length - 1}
                                                    onClick={() => withLobby((c) => movePlayerDown(c, p.id))}
                                                    title={t('moveDown')}>▼</button>
                                            </span>
                                        )}
                                    </li>
                                ))}
                                {orderedPlayers.length === 0 && (
                                    <li style={{ color: 'var(--text-dim)', textAlign: 'center', padding: 12 }}>
                                        {t('noPlayersYet')}
                                    </li>
                                )}
                            </ul>

                            <div style={{
                                color: 'var(--text-dim)', fontSize: 12,
                                fontStyle: 'italic', textAlign: 'center', marginBottom: 6
                            }}>
                                {t('playersCount', { count: lobby.players.length })}
                                {lobby.players.length < 4 && ` — ${t('needMore', { n: 4 - lobby.players.length })}`}
                            </div>
                        </div>

                        {!lobby.started && (
                            isHost
                                ? <GameConfigPanel config={lobby.config} onChange={onConfigChange} />
                                : <GameConfigView config={lobby.config} />
                        )}

                        {canStart && (
                            <button className="btn btn--primary btn--block"
                                style={{ marginTop: 18, fontSize: '1rem', padding: '14px 20px' }}
                                onClick={onStart}>
                                🔥 {t('startGame')}
                            </button>
                        )}

                        {messages.length > 0 && (
                            <div className="event-log" style={{ marginTop: 16 }}>
                                {messages.slice(-4).map((m, i) => (
                                    <div key={i} className="event-log__line">— {m}</div>
                                ))}
                            </div>
                        )}
                    </div>
                )}
            </div>
            <JoinModal
                show={showJoinModal}
                code={codeToJoin}
                me={me}
                nickname={nickname}
                setNickname={setNickname}
                onConfirm={joinViaLinkConfirm}
                onCancel={joinViaLinkCancel}
            />
        </div>
    );
}
