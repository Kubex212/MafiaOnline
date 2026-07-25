import React, { useEffect, useRef, useState } from 'react';
import type {
    LobbyResponse, RoleResponse, SeatInfo,
    NightTurnStarted, NightResolvedPayload, LobbyOrderChangedPayload,
    DayStartedPayload, VoteCastPayload, DayResolvedPayload, GameOverPayload,
    GameConfig,
} from './api';
import {
    createLobby, joinLobby, createHubConnection, getLobby,
    startLobby, getMyRole, movePlayerUp, movePlayerDown, shufflePlayers, reconnect,
    updateConfig,
} from './api';
import { HubConnection } from '@microsoft/signalr';
import NightPhase, { type NightPhaseHandle } from './NightPhase';
import DayPhase, { type DayPhaseHandle } from './DayPhase';
import GameOver from './GameOver';
import GameConfigPanel from './GameConfigPanel';
import { useTranslation } from 'react-i18next';

type GameView = 'lobby' | 'role-reveal' | 'night' | 'day' | 'gameover';

/**
 * Fill the roleName/team fields the frontend expects from the various
 * shapes the backend returns (some endpoints omit `team` / `teamName`).
 */
function normalizeRole(r: RoleResponse): RoleResponse {
    if (r.team && r.teamName) return r;
    const derived = r.name === 'Werewolf' ? 'Werewolf' : 'Town';
    return { ...r, team: r.team ?? derived, teamName: r.teamName ?? derived };
}

// Defer a callback until AFTER the pending React commit lands — this is how
// we hand off initial NightTurnStarted / DayStarted payloads to phase
// components that are just now being mounted for the first time.
const runAfterCommit = (cb: () => void) => queueMicrotask(() => setTimeout(cb, 0));

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

    const connRef = useRef<HubConnection | null>(null);
    const lobbyRef = useRef<LobbyResponse | null>(null);
    const myPlayerIdRef = useRef<string | null>(null);
    const nightRef = useRef<NightPhaseHandle | null>(null);
    const dayRef = useRef<DayPhaseHandle | null>(null);

    const { t } = useTranslation();

    useEffect(() => { lobbyRef.current = lobby; }, [lobby]);
    useEffect(() => { myPlayerIdRef.current = myPlayerId; }, [myPlayerId]);

    // Update document title reactively based on state instead of during render.
    useEffect(() => {
        if (view === 'role-reveal' && myRole) {
            document.title = `${nickname}: ${t(myRole.name)} — ${t('appTitle')}`;
        } else {
            document.title = t('appTitle');
        }
    }, [view, myRole, nickname, t]);

    // Reflect current phase on <html> so the theme retunes atmospherically.
    useEffect(() => {
        document.documentElement.setAttribute('data-phase', view);
    }, [view]);

    // ── SignalR wiring ────────────────────────────────────────────────────
    useEffect(() => {
        const conn = createHubConnection();
        connRef.current = conn;

        conn.on('PlayerJoined', async (nick: string) => {
            setMessages(m => [...m, t('playerJoined', { nick })]);
            const code = lobbyRef.current?.code;
            if (code) {
                try { setLobby(await getLobby(code)); } catch { /* ignore */ }
            }
        });

        conn.on('LobbyOrderChanged', (ordered: LobbyOrderChangedPayload) => {
            const prev = lobbyRef.current;
            if (!prev) return;
            const newPlayers = ordered.map(o => ({
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

        conn.on('GameStarted', async (seatList: SeatInfo[]) => {
            const sorted = [...seatList].sort((a, b) => a.seatIndex - b.seatIndex);
            setSeats(sorted);
            const code = lobbyRef.current?.code;
            const pid = myPlayerIdRef.current;
            if (code && pid) {
                try {
                    setMyRole(normalizeRole(await getMyRole(code, pid)));
                    setView('role-reveal');
                    setTimeout(() => setView('night'), 3000);
                } catch { /* ignore */ }
            }
        });

        conn.on('NightTurnStarted', (turn: NightTurnStarted) => {
            setView('night');
            runAfterCommit(() => nightRef.current?.onTurnStarted(turn));
        });

        conn.on('NightResolved', (payload: NightResolvedPayload) => {
            nightRef.current?.onNightResolved(payload);
        });

        conn.on('DayStarted', (payload: DayStartedPayload) => {
            setView('day');
            runAfterCommit(() => dayRef.current?.onDayStarted(payload));
        });

        conn.on('VoteCast', (payload: VoteCastPayload) => {
            dayRef.current?.onVoteCast(payload);
        });

        conn.on('DayResolved', (payload: DayResolvedPayload) => {
            dayRef.current?.onDayResolved(payload);
        });

        conn.on('GameOver', (payload: GameOverPayload) => {
            setGameOver(payload);
            setView('gameover');
        });

        conn.start()
            .then(async () => {
                setHubStarted(true);
                const storedUserId = sessionStorage.getItem('userId');
                const storedLobbyCode = sessionStorage.getItem('lobbyCode');
                if (!storedUserId || !storedLobbyCode) return;

                try {
                    const data = await reconnect(storedUserId, storedLobbyCode);
                    setLobby(data.lobby);
                    setMyPlayerId(storedUserId);
                    setSeats(data.seats);
                    setMyRole(data.myRole ? normalizeRole(data.myRole) : null);
                    setView(data.gamePhase);
                    setMessages([t('reconnected')]);
                    await conn.invoke('JoinLobby', storedLobbyCode);

                    if (data.gamePhase === 'day' && data.day) {
                        setSeats(data.day.alivePlayers);
                        runAfterCommit(() => {
                            dayRef.current?.onDayStarted({
                                durationSeconds: data.day!.durationSeconds,
                                showVoteTargets: data.day!.showVoteTargets,
                                alivePlayers: data.day!.alivePlayers,
                            });
                            if (data.day!.votes) {
                                Object.entries(data.day!.votes).forEach(([voterId, candidateId]) => {
                                    dayRef.current?.onVoteCast({
                                        voterId, candidateId,
                                        totalVotesCast: Object.keys(data.day!.votes!).length,
                                    });
                                });
                            }
                        });
                    } else if (data.gamePhase === 'night' && data.night) {
                        runAfterCommit(() => {
                            nightRef.current?.onTurnStarted({
                                activePlayerId: data.night!.activePlayerId ?? '',
                                activeSeatIndex: data.night!.activeSeatIndex,
                                hasAbility: data.night!.hasAbility,
                                timeoutSeconds: data.night!.remainingSeconds ?? 1000,
                            });
                        });
                    }
                } catch (e) {
                    sessionStorage.removeItem('userId');
                    sessionStorage.removeItem('lobbyCode');
                    console.error('Reconnection failed:', e);
                }
            })
            .catch(err => console.error('Hub start error', err));

        return () => { conn.stop().catch(() => { }); connRef.current = null; };
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, []);

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
            sessionStorage.setItem('userId', me.id);
            sessionStorage.setItem('lobbyCode', res.code);
            setMessages([t('lobbyCreated', { code: res.code })]);
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
            sessionStorage.setItem('userId', me.id);
            sessionStorage.setItem('lobbyCode', res.code);
            setMessages([t('joinedLobby', { code: res.code })]);
            await joinHubGroup(res.code);
        } catch (e) { alert(String(e)); }
    }

    async function onStart() {
        if (!lobby || !myPlayerId) return;
        try { await startLobby(lobby.code, myPlayerId); }
        catch (e) { alert(String(e)); }
    }

    async function withLobby(fn: (code: string, pid: string) => Promise<LobbyResponse>) {
        if (!lobby || !myPlayerId) return;
        try { setLobby(await fn(lobby.code, myPlayerId)); }
        catch (e) { alert(String(e)); }
    }

    async function onConfigChange(config: GameConfig) {
        if (!lobby || !myPlayerId) return;
        try { await updateConfig(lobby.code, myPlayerId, config); }
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

    function clearSession() {
        sessionStorage.removeItem('userId');
        sessionStorage.removeItem('lobbyCode');
    }

    const isHost = lobby?.players.find(p => p.id === myPlayerId)?.isHost ?? false;
    const canStart = isHost && (lobby?.players.length ?? 0) >= 4 && !lobby?.started;
    const orderedPlayers = lobby
        ? [...lobby.players].sort((a, b) => a.lobbyPosition - b.lobbyPosition)
        : [];

    // ── GAME OVER ────────────────────────────────────────────────────────
    if (view === 'gameover' && gameOver && myRole) {
        clearSession();
        return (
            <div className="phase"><div className="phase__inner">
                <GameOver payload={gameOver} myRoleLabel={t(myRole.name)} myRole={myRole.name} />
            </div></div>
        );
    }

    // ── ROLE REVEAL ──────────────────────────────────────────────────────
    if (view === 'role-reveal' && myRole) {
        const teamColorVar = myRole.teamId === 1 ? 'var(--wolf)'
            : myRole.teamId === 2 ? 'var(--neutral)'
            : 'var(--town-2)';
        const sigil = myRole.name === 'Werewolf' ? '🐺'
            : myRole.name === 'Guardian' ? '🛡'
            : myRole.name === 'Detective' ? '🔍'
            : myRole.name === 'Seer' ? '🔮'
            : myRole.name === 'Fool' ? '🃏'
            : myRole.name === 'Dog Tamer' ? '🐕'
            : myRole.name === 'Poacher' ? '🪤'
            : myRole.name === 'Trailer' ? '🐾'
            : '🌾';
        return (
            <div className="phase"><div className="phase__inner reveal">
                <div className="reveal__eyebrow">{t('yourRoleIs')}</div>
                <div className="reveal__sigil" style={{ color: teamColorVar }}>{sigil}</div>
                <div className="reveal__role" style={{ color: teamColorVar, textShadow: `0 0 40px ${teamColorVar}` }}>
                    {t(myRole.name)}
                </div>
                <div className="reveal__team">{myRole.teamName ?? myRole.team}</div>
                <div style={{ color: 'var(--text-dim)', fontSize: 13, marginTop: 12, fontStyle: 'italic' }}>
                    {t('nightBeginsSoon')}
                </div>
            </div></div>
        );
    }

    // ── NIGHT PHASE ──────────────────────────────────────────────────────
    if (view === 'night' && seats && myPlayerId && myRole) {
        return (
            <div className="phase"><div className="phase__inner">
                <h2 className="phase__title">
                    🌙 {t('nightPhase')}
                    <small>Lobby <span className="phase__code">{lobby?.code}</span></small>
                </h2>
                <NightPhase ref={nightRef}
                    lobbyCode={lobby!.code}
                    seats={seats}
                    myPlayerId={myPlayerId}
                    myRole={myRole} />
            </div></div>
        );
    }

    // ── DAY PHASE ────────────────────────────────────────────────────────
    if (view === 'day' && seats && myPlayerId && myRole) {
        return (
            <div className="phase"><div className="phase__inner">
                <h2 className="phase__title">
                    ☀ {t('dayPhase')}
                    <small>Lobby <span className="phase__code">{lobby?.code}</span></small>
                </h2>
                <DayPhase ref={dayRef}
                    lobbyCode={lobby!.code}
                    allSeats={seats}
                    myPlayerId={myPlayerId}
                    myRole={myRole} />
            </div></div>
        );
    }

    // ── LOBBY VIEW ───────────────────────────────────────────────────────
    return (
        <div className="phase">
            <div className="phase__inner">
                {!lobby && (
                    <>
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
                                onClick={onCreate} disabled={creating || !nickname.trim()}>
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
                                    disabled={!codeToJoin.trim() || !nickname.trim()}>
                                    {t('join')}
                                </button>
                            </div>
                        </div>
                    </>
                )}

                {lobby && (
                    <div className="lobby-room">
                        <div className="lobby-room__header">
                            <div>
                                <button className="lobby-room__code" onClick={copyCode}
                                    title={t('copyCode')}>
                                    {lobby.code}
                                </button>
                                <div className="lobby-room__code-hint">
                                    {codeCopied ? '✓ ' + t('codeCopied') : t('tapToCopy')}
                                </div>
                            </div>
                            <div className={`status-dot ${hubStarted ? '' : 'status-dot--off'}`}>
                                {hubStarted ? t('connected') : t('connecting')}
                            </div>
                        </div>

                        <div className="card card--parchment">
                            <div style={{ display: 'flex', justifyContent: 'space-between',
                                alignItems: 'center', marginBottom: 10 }}>
                                <span className="field__label">
                                    {t('seatOrder')} {!isHost && `— ${t('setByHost')}`}
                                </span>
                                {isHost && (
                                    <button className="icon-btn"
                                        onClick={() => withLobby((c, p) => shufflePlayers(c, p))}
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
                                        {p.isHost && <span className="tag">{t('host')}</span>}
                                        {p.id === myPlayerId && <span className="tag tag--you">{t('you')}</span>}
                                        {isHost && !lobby.started && (
                                            <span style={{ display: 'flex', gap: 3, marginLeft: 4 }}>
                                                <button className="icon-btn"
                                                    disabled={i === 0}
                                                    onClick={() => withLobby((c, pid) => movePlayerUp(c, pid, p.id))}
                                                    title={t('moveUp')}>▲</button>
                                                <button className="icon-btn"
                                                    disabled={i === orderedPlayers.length - 1}
                                                    onClick={() => withLobby((c, pid) => movePlayerDown(c, pid, p.id))}
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

                            <div style={{ color: 'var(--text-dim)', fontSize: 12,
                                fontStyle: 'italic', textAlign: 'center', marginBottom: 6 }}>
                                {t('playersCount', { count: lobby.players.length })}
                                {lobby.players.length < 4 && ` — ${t('needMore', { n: 4 - lobby.players.length })}`}
                            </div>
                        </div>

                        {isHost && !lobby.started && (
                            <GameConfigPanel config={lobby.config} onChange={onConfigChange} />
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
        </div>
    );
}
