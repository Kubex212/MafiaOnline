import React, { useEffect, useRef, useState } from 'react';
import type {
    LobbyResponse, RoleResponse, SeatInfo,
    NightTurnStarted, NightResolvedPayload, LobbyOrderChangedPayload,
    DayStartedPayload, VoteCastPayload, DayResolvedPayload, GameOverPayload,
} from './api';
import {
    createLobby, joinLobby, createHubConnection, getLobby,
    startLobby, getMyRole, movePlayerUp, movePlayerDown, shufflePlayers,
} from './api';
import { HubConnection } from '@microsoft/signalr';
import NightPhase, { type NightPhaseHandle } from './NightPhase';
import DayPhase, { type DayPhaseHandle } from './DayPhase';
import GameOver from './GameOver';
import { useTranslation } from "react-i18next";

type GameView = 'lobby' | 'role-reveal' | 'night' | 'day' | 'gameover';

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

    const connRef = useRef<HubConnection | null>(null);
    const lobbyRef = useRef<LobbyResponse | null>(null);
    const myPlayerIdRef = useRef<string | null>(null);
    const nightRef = useRef<NightPhaseHandle | null>(null);
    const dayRef = useRef<DayPhaseHandle | null>(null);

    const { t } = useTranslation();

    useEffect(() => { lobbyRef.current = lobby; }, [lobby]);
    useEffect(() => { myPlayerIdRef.current = myPlayerId; }, [myPlayerId]);

    useEffect(() => {
        const conn = createHubConnection();
        connRef.current = conn;

        conn.on('PlayerJoined', async (nick: string) => {
            setMessages(m => [...m, `${nick} joined the lobby`]);
            const code = lobbyRef.current?.code;
            const lobby = await getLobby(code);
            console.log(lobby);
            if (code) { try { setLobby(lobby); } catch { /* ignore */ } }
        });

        conn.on('LobbyOrderChanged', (ordered: LobbyOrderChangedPayload) => {
            setLobby(prev => {
                if (!prev) return prev;
                const byId = new Map(prev.players.map(p => [p.id, p]));
                const newPlayers = ordered.map(o => ({
                    ...(byId.get(o.id) ?? { id: o.id, nickname: o.nickname, isHost: o.isHost, seatIndex: -1 }),
                    lobbyPosition: o.lobbyPosition,
                }));
                return { ...prev, players: newPlayers };
            });
        });

        conn.on('GameStarted', async (seatList: SeatInfo[]) => {
            const sorted = [...seatList].sort((a, b) => a.seatIndex - b.seatIndex);
            setSeats(sorted);
            const code = lobbyRef.current?.code;
            const pid = myPlayerIdRef.current;
            if (code && pid) {
                try {
                    const role = await getMyRole(code, pid);
                    setMyRole(role);
                    setView('role-reveal');
                    setTimeout(() => setView('night'), 3000);
                } catch { /* ignore */ }
            }
        });

        conn.on('NightTurnStarted', (turn: NightTurnStarted) => {
            setView('night');
            nightRef.current?.onTurnStarted(turn);
        });

        conn.on('NightResolved', (payload: NightResolvedPayload) => {
            nightRef.current?.onNightResolved(payload);
        });


        // TODO: fix it properly instead of using timeout (dayRef.current is null because DayPhase component is not rendered yet)
        conn.on('DayStarted', (payload: DayStartedPayload) => {
            setView('day');

            setTimeout(() => {
                console.log(dayRef.current);
                dayRef.current?.onDayStarted(payload);
            }, 0);
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
            .then(() => setHubStarted(true))
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
            const res = await createLobby(nickname);
            const me = res.players.find(p => p.isHost)!;
            setMyPlayerId(me.id);
            setLobby(res);
            setMessages([`Lobby ${res.code} created`]);
            await joinHubGroup(res.code);
        } catch (e) { alert(String(e)); }
    }

    async function onJoin() {
        try {
            const res = await joinLobby(codeToJoin, nickname);
            const me = res.players.find(p => p.nickname.toLowerCase() === nickname.toLowerCase())!;
            setMyPlayerId(me.id);
            setLobby(res);
            setMessages([`Joined lobby ${res.code}`]);
            await joinHubGroup(res.code);
        } catch (e) { alert(String(e)); }
    }

    async function onStart() {
        if (!lobby || !myPlayerId) return;
        try { await startLobby(lobby.code, myPlayerId); }
        catch (e) { alert(String(e)); }
    }

    async function onMoveUp(targetId: string) {
        if (!lobby || !myPlayerId) return;
        try { setLobby(await movePlayerUp(lobby.code, myPlayerId, targetId)); }
        catch (e) { alert(String(e)); }
    }

    async function onMoveDown(targetId: string) {
        if (!lobby || !myPlayerId) return;
        try { setLobby(await movePlayerDown(lobby.code, myPlayerId, targetId)); }
        catch (e) { alert(String(e)); }
    }

    async function onShuffle() {
        if (!lobby || !myPlayerId) return;
        try { setLobby(await shufflePlayers(lobby.code, myPlayerId)); }
        catch (e) { alert(String(e)); }
    }

    const isHost = lobby?.players.find(p => p.id === myPlayerId)?.isHost ?? false;
    const canStart = isHost && (lobby?.players.length ?? 0) >= 4 && !lobby?.started;
    const orderedPlayers = lobby
        ? [...lobby.players].sort((a, b) => a.lobbyPosition - b.lobbyPosition)
        : [];

    // ── GAME OVER ──────────────────────────────────────────────────────────────
    if (view === 'gameover' && gameOver && myRole) {
        return (
            <div style={styles.page}>
                <GameOver payload={gameOver} myRole={myRole.name} />
            </div>
        );
    }

    // ── ROLE REVEAL ────────────────────────────────────────────────────────────
    if (view === 'role-reveal' && myRole) {
        const teamColor = myRole.team === 'Werewolf' ? '#d9534f' : '#5cb85c';
        return (
            <div style={{ ...styles.page, display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', gap: 16 }}>
                <div style={{ fontSize: 14, color: '#aaa', letterSpacing: 3, textTransform: 'uppercase' }}>
                    Your role is{'\u2026'}
                </div>
                <div style={{ fontSize: 48, fontWeight: 900, color: teamColor, textShadow: `0 0 40px ${teamColor}88` }}>
                    {myRole.name}
                </div>
                <div style={{ color: '#aaa', fontSize: 15 }}>Team: {myRole.team}</div>
                <div style={{ color: '#555', fontSize: 13, marginTop: 12 }}>Night phase begins in a moment{'\u2026'}</div>
            </div>
        );
    }

    // ── NIGHT PHASE ────────────────────────────────────────────────────────────
    if (view === 'night' && seats && myPlayerId && myRole) {
        return (
            <div style={styles.page}>
                <h2 style={{ color: '#aaa', margin: '0 0 16px', textAlign: 'center' }}>
                    {'\uD83C\uDF19'} Night Phase {'\u2014'} Lobby{' '}
                    <span style={{ fontFamily: 'monospace', color: '#f0c040' }}>{lobby?.code}</span>
                </h2>
                <NightPhase
                    ref={nightRef}
                    lobbyCode={lobby!.code}
                    seats={seats}
                    myPlayerId={myPlayerId}
                    myRole={myRole}
                />
            </div>
        );
    }

    // ── DAY PHASE ──────────────────────────────────────────────────────────────
    if (view === 'day' && seats && myPlayerId && myRole) {
        return (
            <div style={styles.page}>
                <h2 style={{ color: '#aaa', margin: '0 0 16px', textAlign: 'center' }}>
                    {'\u2600\uFE0F'} Day Phase {'\u2014'} Lobby{' '}
                    <span style={{ fontFamily: 'monospace', color: '#f0c040' }}>{lobby?.code}</span>
                </h2>
                <DayPhase
                    ref={dayRef}
                    lobbyCode={lobby!.code}
                    allSeats={seats}
                    myPlayerId={myPlayerId}
                    myRole={myRole}
                />
            </div>
        );
    }

    // ── LOBBY VIEW ─────────────────────────────────────────────────────────────
    return (
        <div style={styles.page}>
            <h1 style={{ color: '#f0c040' }}>MafiaOnline</h1>

            {!lobby && (
                <>
                    <div style={styles.row}>
                        <label style={styles.label}>
                            Nickname:
                            <input
                                value={nickname}
                                onChange={e => setNickname(e.target.value)}
                                style={styles.input}
                            />
                        </label>
                    </div>
                    <div style={styles.row}>
                        <button style={styles.btn} onClick={onCreate}>{t('createLobby')}</button>
                        <span style={{ color: '#aaa', margin: '0 12px' }}>or join:</span>
                        <input
                            value={codeToJoin}
                            placeholder="Lobby code"
                            onChange={e => setCodeToJoin(e.target.value.toUpperCase())}
                            style={{ ...styles.input, width: 100, textTransform: 'uppercase' }}
                        />
                        <button style={{ ...styles.btn, marginLeft: 8 }} onClick={onJoin}>Join</button>
                    </div>
                </>
            )}

            {lobby && (
                <div style={styles.card}>
                    <div style={{ display: 'flex', alignItems: 'center', gap: 16, marginBottom: 14 }}>
                        <h3 style={{ margin: 0, color: '#f0c040' }}>
                            Lobby <span style={{ fontFamily: 'monospace' }}>{lobby.code}</span>
                        </h3>
                        <span style={{ color: '#aaa', fontSize: 13 }}>
                            {hubStarted ? '\uD83D\uDFE2 connected' : '\uD83D\uDD34 connecting\u2026'}
                        </span>
                    </div>

                    <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 6 }}>
                        <span style={{ fontSize: 12, color: '#666', textTransform: 'uppercase', letterSpacing: 1 }}>
                            Seat order {isHost ? '' : '(set by host)'}
                        </span>
                        {isHost && (
                            <button onClick={onShuffle} style={styles.shuffleBtn} title="Shuffle randomly">
                                {'\uD83C\uDFB2'} Shuffle
                            </button>
                        )}
                    </div>

                    <ul style={styles.playerList}>
                        {orderedPlayers.map((p, i) => (
                            <li key={p.id} style={{
                                ...styles.playerItem,
                                background: p.id === myPlayerId ? '#1e2e1e' : '#161626',
                                borderColor: p.id === myPlayerId ? '#3a5a3a' : '#2a2a3e',
                            }}>
                                <span style={styles.seatNum}>{i + 1}</span>
                                <span style={{
                                    flex: 1,
                                    color: p.id === myPlayerId ? '#f0c040' : '#ddd',
                                    fontWeight: p.id === myPlayerId ? 700 : 400,
                                }}>
                                    {p.nickname}
                                </span>
                                {p.isHost && <span style={styles.badge}>host</span>}
                                {p.id === myPlayerId && <span style={{ ...styles.badge, background: '#444' }}>you</span>}
                                {isHost && !lobby.started && (
                                    <span style={{ display: 'flex', gap: 3, marginLeft: 6 }}>
                                        <button
                                            onClick={() => onMoveUp(p.id)}
                                            disabled={i === 0}
                                            style={{ ...styles.arrowBtn, opacity: i === 0 ? 0.25 : 1 }}
                                            title="Move up"
                                        >{'\u25B2'}</button>
                                        <button
                                            onClick={() => onMoveDown(p.id)}
                                            disabled={i === orderedPlayers.length - 1}
                                            style={{ ...styles.arrowBtn, opacity: i === orderedPlayers.length - 1 ? 0.25 : 1 }}
                                            title="Move down"
                                        >{'\u25BC'}</button>
                                    </span>
                                )}
                            </li>
                        ))}
                    </ul>

                    <div style={{ color: '#666', fontSize: 12, marginBottom: 12 }}>
                        {lobby.players.length} / 4+ players
                        {lobby.players.length < 4 && ` \u2014 need ${4 - lobby.players.length} more`}
                    </div>

                    {canStart && (
                        <button
                            style={{ ...styles.btn, background: '#5cb85c', fontSize: 15, width: '100%' }}
                            onClick={onStart}
                        >
                            {t('startGame')}test
                        </button>
                    )}
                </div>
            )}

            {messages.length > 0 && (
                <div style={{ marginTop: 20 }}>
                    <h4 style={{ color: '#aaa' }}>Events</h4>
                    <ul style={{ color: '#ccc', fontSize: 13 }}>
                        {messages.map((m, i) => <li key={i}>{m}</li>)}
                    </ul>
                </div>
            )}
        </div>
    );
}

const styles = {
    page: {
        padding: 24, fontFamily: 'Segoe UI, Roboto, Arial',
        background: '#0f0f1a', minHeight: '100vh', color: '#ddd',
    } as React.CSSProperties,
    row: { marginBottom: 12 } as React.CSSProperties,
    label: { display: 'flex', alignItems: 'center', gap: 8, color: '#ccc' } as React.CSSProperties,
    input: {
        background: '#1e1e2e', border: '1px solid #444', borderRadius: 6,
        color: '#ddd', padding: '6px 10px', fontSize: 14,
    } as React.CSSProperties,
    btn: {
        background: '#c0392b', color: '#fff', border: 'none', borderRadius: 8,
        padding: '8px 20px', cursor: 'pointer', fontSize: 14, fontWeight: 600,
    } as React.CSSProperties,
    card: {
        marginTop: 20, background: '#1a1a2e', borderRadius: 12,
        padding: 20, maxWidth: 420, border: '1px solid #333',
    } as React.CSSProperties,
    playerList: {
        listStyle: 'none', padding: 0, margin: '0 0 8px',
        display: 'flex', flexDirection: 'column', gap: 4,
    } as React.CSSProperties,
    playerItem: {
        display: 'flex', alignItems: 'center', gap: 8,
        padding: '7px 10px', borderRadius: 8, border: '1px solid #2a2a3e',
    } as React.CSSProperties,
    seatNum: {
        width: 22, height: 22, borderRadius: '50%', background: '#2a2a4a',
        color: '#888', display: 'flex', alignItems: 'center', justifyContent: 'center',
        fontSize: 11, fontWeight: 700, flexShrink: 0,
    } as React.CSSProperties,
    badge: {
        fontSize: 10, background: '#c0392b', color: '#fff',
        borderRadius: 4, padding: '1px 6px', textTransform: 'uppercase' as const,
    } as React.CSSProperties,
    arrowBtn: {
        background: '#2a2a4a', border: '1px solid #3a3a5a', borderRadius: 4,
        color: '#aaa', cursor: 'pointer', fontSize: 10, padding: '2px 6px', lineHeight: 1,
    } as React.CSSProperties,
    shuffleBtn: {
        background: '#2a2a4a', border: '1px solid #3a3a5a', borderRadius: 6,
        color: '#aaa', cursor: 'pointer', fontSize: 12, padding: '3px 10px',
    } as React.CSSProperties,
};
