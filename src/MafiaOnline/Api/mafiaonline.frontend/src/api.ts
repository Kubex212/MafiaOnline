import { HubConnection, HubConnectionBuilder } from '@microsoft/signalr';

export type PlayerResponse = {
  id: string;
  nickname: string;
  isHost: boolean;
  seatIndex: number;
};

export type LobbyResponse = {
  code: string;
  started: boolean;
  players: PlayerResponse[];
};

export type RoleResponse = {
  name: string;
  team: string;
};

export type SeatInfo = {
  id: string;
  nickname: string;
  seatIndex: number;
};

export async function createLobby(nickname: string): Promise<LobbyResponse> {
  const res = await fetch('/api/lobbies', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ nickname })
  });
  if (!res.ok) throw new Error('Create failed: ' + res.statusText);
  return res.json();
}

export async function joinLobby(code: string, nickname: string): Promise<LobbyResponse> {
  const res = await fetch('/api/lobbies/join', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ code, nickname })
  });
  if (!res.ok) throw new Error('Join failed: ' + res.statusText);
  return res.json();
}

export async function getLobby(code: string): Promise<LobbyResponse> {
  const res = await fetch(`/api/lobbies/${encodeURIComponent(code)}`);
  if (!res.ok) throw new Error('Get lobby failed: ' + res.statusText);
  return res.json();
}

export async function startLobby(code: string, playerId: string): Promise<LobbyResponse> {
  const res = await fetch(`/api/lobbies/${encodeURIComponent(code)}/start`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ playerId })
  });
  if (!res.ok) throw new Error('Start failed: ' + res.statusText);
  return res.json();
}

export async function getMyRole(code: string, playerId: string): Promise<RoleResponse> {
  const res = await fetch(`/api/lobbies/${encodeURIComponent(code)}/role/${encodeURIComponent(playerId)}`);
  if (!res.ok) throw new Error('Get role failed: ' + res.statusText);
  return res.json();
}

export function createHubConnection(): HubConnection {
  return new HubConnectionBuilder()
    .withUrl('/lobbyHub')
    .withAutomaticReconnect()
    .build();
}