import React, { useEffect, useRef, useState } from 'react';
import type { LobbyResponse, RoleResponse, SeatInfo } from './api';
import {
  createLobby, joinLobby, createHubConnection,
  getLobby, startLobby, getMyRole
} from './api';
import { HubConnection } from '@microsoft/signalr';
import RoundTable from './RoundTable';

export default function App() {
  const [nickname, setNickname] = useState('Player' + Math.floor(Math.random() * 1000));
  const [lobby, setLobby] = useState<LobbyResponse | null>(null);
  const [codeToJoin, setCodeToJoin] = useState('');
  const [messages, setMessages] = useState<string[]>([]);
  const [hubStarted, setHubStarted] = useState(false);

  // Persisted across renders via refs
  const connRef = useRef<HubConnection | null>(null);
  const lobbyRef = useRef<LobbyResponse | null>(null);

  // Game state
  const [myPlayerId, setMyPlayerId] = useState<string | null>(null);
  const [seats, setSeats] = useState<SeatInfo[] | null>(null);
  const [myRole, setMyRole] = useState<RoleResponse | null>(null);

  // Keep lobbyRef in sync
  useEffect(() => { lobbyRef.current = lobby; }, [lobby]);

  useEffect(() => {
    const conn = createHubConnection();
    connRef.current = conn;

    conn.on('PlayerJoined', async (nick: string) => {
      setMessages(m => [...m, `${nick} joined the lobby`]);
      const code = lobbyRef.current?.code;
      if (code) {
        try { setLobby(await getLobby(code)); } catch { /* ignore */ }
      }
    });

    conn.on('GameStarted', async (seatList: SeatInfo[]) => {
      setMessages(m => [...m, 'Game started!']);
      const sorted = [...seatList].sort((a, b) => a.seatIndex - b.seatIndex);
      setSeats(sorted);

      // Fetch own role
      const code = lobbyRef.current?.code;
      const pid = myPlayerIdRef.current;
      if (code && pid) {
        try {
          const role = await getMyRole(code, pid);
          setMyRole(role);
        } catch { /* ignore */ }
      }
    });

    conn.start()
      .then(() => { setHubStarted(true); })
      .catch(err => console.error('Hub start error', err));

    return () => {
      conn.stop().catch(() => {});
      connRef.current = null;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // We need a ref for myPlayerId inside the hub callback
  const myPlayerIdRef = useRef<string | null>(null);
  useEffect(() => { myPlayerIdRef.current = myPlayerId; }, [myPlayerId]);

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
    try {
      await startLobby(lobby.code, myPlayerId);
      // GameStarted event will fire via SignalR for everyone including us
    } catch (e) { alert(String(e)); }
  }

  const isHost = lobby?.players.find(p => p.id === myPlayerId)?.isHost ?? false;
  const canStart = isHost && (lobby?.players.length ?? 0) >= 4 && !lobby?.started;

  // ?? GAME VIEW ??????????????????????????????????????????????????????????????
  if (seats && myPlayerId) {
    return (
      <div style={styles.page}>
        <h2 style={{ color: '#f0c040', marginBottom: 4 }}>Lobby {lobby?.code}</h2>
        <p style={{ color: '#aaa', margin: '0 0 20px' }}>The night begins…</p>
        <RoundTable seats={seats} myPlayerId={myPlayerId} myRole={myRole} />
      </div>
    );
  }

  // ?? LOBBY VIEW ?????????????????????????????????????????????????????????????
  return (
    <div style={styles.page}>
      <h1 style={{ color: '#f0c040' }}>MafiaOnline</h1>

      {!lobby && (
        <>
          <div style={styles.row}>
            <label style={styles.label}>
              Nickname:
              <input value={nickname} onChange={e => setNickname(e.target.value)} style={styles.input} />
            </label>
          </div>
          <div style={styles.row}>
            <button style={styles.btn} onClick={onCreate}>Create Lobby</button>
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
          <div style={{ display: 'flex', alignItems: 'center', gap: 16, marginBottom: 12 }}>
            <h3 style={{ margin: 0, color: '#f0c040' }}>Lobby <span style={{ fontFamily: 'monospace' }}>{lobby.code}</span></h3>
            <span style={{ color: '#aaa', fontSize: 13 }}>{hubStarted ? '?? connected' : '?? connecting…'}</span>
          </div>

          <ul style={styles.playerList}>
            {lobby.players.map(p => (
              <li key={p.id} style={{
                ...styles.playerItem,
                color: p.id === myPlayerId ? '#f0c040' : '#ddd',
                fontWeight: p.id === myPlayerId ? 700 : 400,
              }}>
                {p.nickname}
                {p.isHost ? <span style={styles.badge}>host</span> : null}
                {p.id === myPlayerId ? <span style={{ ...styles.badge, background: '#555' }}>you</span> : null}
              </li>
            ))}
          </ul>

          <div style={{ color: '#aaa', fontSize: 13, marginBottom: 12 }}>
            {lobby.players.length} / 4+ players {lobby.players.length < 4 && `— need ${4 - lobby.players.length} more`}
          </div>

          {canStart && (
            <button style={{ ...styles.btn, background: '#5cb85c', fontSize: 16 }} onClick={onStart}>
              Start Game
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
    padding: 24,
    fontFamily: 'Segoe UI, Roboto, Arial',
    background: '#0f0f1a',
    minHeight: '100vh',
    color: '#ddd',
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
    padding: 20, maxWidth: 400, border: '1px solid #333',
  } as React.CSSProperties,
  playerList: { listStyle: 'none', padding: 0, margin: '0 0 8px' } as React.CSSProperties,
  playerItem: {
    display: 'flex', alignItems: 'center', gap: 8,
    padding: '5px 0', borderBottom: '1px solid #2a2a3e',
  } as React.CSSProperties,
  badge: {
    fontSize: 10, background: '#c0392b', color: '#fff',
    borderRadius: 4, padding: '1px 6px', textTransform: 'uppercase' as const,
  } as React.CSSProperties,
};
