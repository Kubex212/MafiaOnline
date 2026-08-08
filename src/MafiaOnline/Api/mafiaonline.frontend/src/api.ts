import { HubConnection, HubConnectionBuilder } from '@microsoft/signalr';
import type { ActionType } from './NightPhase';

// ── Session token ────────────────────────────────────────────────────────────

const SESSION_KEY = 'sessionToken';

export function getStoredToken(): string | null {
    return sessionStorage.getItem(SESSION_KEY);
}

function storeToken(token: string) {
    sessionStorage.setItem(SESSION_KEY, token);
    console.log('stored token');
}

export async function getOrCreateSession(): Promise<{ userId: string }> {
    // If Discord just redirected back with ?token=xxx, store it and clean the URL
    const params = new URLSearchParams(window.location.search);
    const urlToken = params.get('token');
    console.log('token: ', urlToken);
    if (urlToken) {
        storeToken(urlToken);
        //window.history.replaceState({}, '', window.location.pathname);
    }

    const existing = getStoredToken();
    const headers: Record<string, string> = { 'Content-Type': 'application/json' };
    if (existing) headers['X-Session-Token'] = existing;

    const res = await fetch('/api/auth/session', { headers });
    if (!res.ok) throw new Error('Session init failed: ' + res.statusText);
    const data: { userId: string; token?: string } = await res.json();
    if (data.token) storeToken(data.token);
    return { userId: data.userId };
}

export function getDiscordLoginUrl(): string {
    return '/api/auth/discord';
}

export function getGoogleLoginUrl(): string {
    return '/api/auth/google';
}

function authHeaders(): Record<string, string> {
    const headers: Record<string, string> = { 'Content-Type': 'application/json' };
    const token = getStoredToken();
    if (token) headers['X-Session-Token'] = token;
    return headers;
}

// ── Types ────────────────────────────────────────────────────────────────────

export type PlayerResponse = {
    id: string;
    nickname: string;
    isHost: boolean;
    lobbyPosition: number;
    seatIndex: number;
};

export type RoleSelection = {
    guardian: boolean;
    detective: boolean;
    dogTamer: boolean;
    poacher: boolean;
    trailer: boolean;
    seer: boolean;
    peeper: boolean;
    fool: boolean;
    dracula: boolean;
    deceiver: boolean;
    ghost: boolean;
    charmer: boolean;
};

export type DrawResolution = 'KickNone' | 'KickRandom' | 'KickAll';
export type ShowVotes = 'Never' | 'AfterVotingEnds' | 'Always';

export type DayConfig = {
    durationSeconds: number;
    votesRequiredFraction: number;
    drawResolution: DrawResolution;
    showVoteTargets: ShowVotes;
};

export type WerewolfConfig = {
    /** How a tie between werewolf attack votes is resolved. */
    drawResolution: DrawResolution;
    /** Whether werewolves see each other's attack votes live. */
    showVoteTargets: ShowVotes;
    /** Whether werewolves receive a fake alibi at the start of the game. */
    alibi: boolean;
};

export const DEFAULT_WEREWOLF_CONFIG: WerewolfConfig = {
    drawResolution: 'KickRandom',
    showVoteTargets: 'Always',
    alibi: true,
};

export type GuardianConfig = { canProtectSelf: boolean; mustAlternate: boolean; announceProtection: boolean };
export type DetectiveConfig = { canRevealWerewolves: boolean; };
export type DogTamerConfig = { range: number; numWerewolves: number; };
export type PoacherConfig = { allowSelfTarget: boolean; mustAlternate: boolean; };
export type GhostConfig = { shouldGetAlibi: boolean; };

export type GameConfig = {
    roles: RoleSelection;
    werewolfCount: number;
    turnDurationSeconds: number;
    guardian: GuardianConfig;
    detective: DetectiveConfig;
    dogTamer: DogTamerConfig;
    day: DayConfig;
    poacher: PoacherConfig;
    werewolf?: WerewolfConfig;
    ghost: GhostConfig;
};

export type LobbyResponse = {
    code: string;
    started: boolean;
    players: PlayerResponse[];
    config: GameConfig;
};

export type RoleResponse = {
    name: string;
    teamName?: string;
    teamId: number;
    alibi?: string;
    otherWerewolvesIds?: string[];
};

export type SeatInfo = {
    id: string;
    nickname: string;
    seatIndex: number;
};

export type NightTurnStarted = {
    activePlayerId: string;
    activeSeatIndex: number;
    hasAbility: boolean;
    timeoutSeconds: number;
    minTargets?: number;
    maxTargets?: number;
    forbiddenTargets?: string[];
};

export type NightResolvedPayload = { killed: string[]; attackBlocked: boolean; };

export enum ClueType {
    Detective = "Detective",
    DogTamer = "DogTamer",
    Seer = "Seer",
    Trailer = "Trailer",
    Peeper = "Peeper",
}

export interface ClueResponse { type: ClueType; data: Record<string, unknown>; clue: unknown; }
export interface DetectiveClueResponse extends ClueResponse { type: ClueType.Detective; clue: string; }
export interface DogTamerClueResponse extends ClueResponse { type: ClueType.DogTamer; clue: boolean; }
export interface SeerClueResponse extends ClueResponse {
    type: ClueType.Seer; clue: boolean;
    data: { SubjectAName: string; SubjectBName: string; SubjectA: string; SubjectB: string; };
}
export interface TrailerClueResponse extends ClueResponse { type: ClueType.Trailer; clue: string; }
export interface PeeperClueResponse extends ClueResponse { type: ClueType.Peeper; clue: boolean; }


export type AnyClueResponse =
    | DetectiveClueResponse | DogTamerClueResponse | SeerClueResponse | TrailerClueResponse | PeeperClueResponse;

export type LobbyOrderChangedPayload = {
    id: string; nickname: string; isHost: boolean; lobbyPosition: number;
}[];

export type DayStartedPayload = {
    durationSeconds: number;
    alivePlayers: SeatInfo[];
    enshrouded?: string[];
    showVoteTargets: 'Always' | 'Never' | 'AfterVotingEnds';
};

export type WerewolfAttackPayload = { actorId: string; targetId: string; targetName: string; };
export type VoteCastPayload = { voterId: string; candidateId: string | null; totalVotesCast: number; };
export type DayResolvedPayload = { banished: string[]; thresholdMet: boolean; tally: { playerId: string; votes: number }[]; };
export type GameOverPayload = { winner: 'Villagers' | 'Werewolves' | 'Fool'; };

export type MeResponse = {
    id: string;
    preferredNickname: string | null;
    isAnonymous: boolean;
    linkedProviders: string[];
};

export type UpdateMeRequest = {
    preferredNickname: string;
};

export type RoleStatisticsResponse = {
    games: number;
    wins: number;
    winRate: number;
};

export type UserStatisticsResponse = {
    gamesPlayed: number;
    wins: number;
    losses: number;
    winRate: number;
    survivalRate: number;
    roles: Record<string, RoleStatisticsResponse>;
};

export type GameHistoryItemResponse = {
    playedAtUtc: string;
    roleId: string;
    won: boolean;
    survived: boolean;
    playerCount: number;
    duration: string;
};

// ── API calls ────────────────────────────────────────────────────────────────

export async function createLobby(nickname: string): Promise<LobbyResponse> {
    const res = await fetch('/api/lobbies', {
        method: 'POST',
        headers: authHeaders(),
        body: JSON.stringify({ nickname })
    });
    if (!res.ok) throw new Error('Create failed: ' + res.statusText);
    return res.json();
}

export async function joinLobby(code: string, nickname: string): Promise<LobbyResponse> {
    const res = await fetch('/api/lobbies/join', {
        method: 'POST',
        headers: authHeaders(),
        body: JSON.stringify({ code, nickname })
    });
    if (!res.ok) throw new Error('Join failed: ' + res.statusText);
    return res.json();
}

export async function getLobby(code: string): Promise<LobbyResponse> {
    const res = await fetch(`/api/lobbies/${encodeURIComponent(code)}`, { headers: authHeaders() });
    if (!res.ok) throw new Error('Get lobby failed: ' + res.statusText);
    return res.json();
}

export async function startLobby(code: string): Promise<LobbyResponse> {
    const res = await fetch(`/api/lobbies/${encodeURIComponent(code)}/start`, {
        method: 'POST',
        headers: authHeaders(),
        body: JSON.stringify({})
    });
    if (!res.ok) throw new Error('Start failed: ' + res.statusText);
    return res.json();
}

export async function getMyRole(code: string): Promise<RoleResponse> {
    const res = await fetch(`/api/lobbies/${encodeURIComponent(code)}/role`, { headers: authHeaders() });
    if (!res.ok) throw new Error('Get role failed: ' + res.statusText);
    return res.json();
}

export function createHubConnection(): HubConnection {
    const token = getStoredToken();
    const url = token ? `/lobbyHub?token=${encodeURIComponent(token)}` : '/lobbyHub';
    return new HubConnectionBuilder()
        .withUrl(url)
        .withAutomaticReconnect()
        .build();
}

export async function submitNightAction(code: string, targets: string | string[], actionType: ActionType): Promise<void> {
    const targetArr = Array.isArray(targets) ? targets : [targets];
    const res = await fetch(`/api/lobbies/${encodeURIComponent(code)}/night/action`, {
        method: 'POST',
        headers: authHeaders(),
        body: JSON.stringify({ targets: targetArr, actionType })
    });
    if (!res.ok) throw new Error('Action failed: ' + res.statusText);
}

export async function skipNightTurn(code: string): Promise<void> {
    const res = await fetch(`/api/lobbies/${encodeURIComponent(code)}/night/skip`, {
        method: 'POST',
        headers: authHeaders(),
        body: JSON.stringify({})
    });
    if (!res.ok) throw new Error('Skip failed: ' + res.statusText);
}

export async function getPlayerNightInfo(code: string): Promise<AnyClueResponse | null> {
    const res = await fetch(`/api/lobbies/${encodeURIComponent(code)}/night/clue`, { headers: authHeaders() });
    if (res.status === 204) return null;
    if (!res.ok) throw new Error('Night info fetch failed: ' + res.statusText);
    return res.json();
}

export async function movePlayerUp(code: string, targetPlayerId: string): Promise<LobbyResponse> {
    const res = await fetch(`/api/lobbies/${encodeURIComponent(code)}/order/move-up`, {
        method: 'POST',
        headers: authHeaders(),
        body: JSON.stringify({ targetPlayerId })
    });
    if (!res.ok) throw new Error('Move failed: ' + res.statusText);
    return res.json();
}

export async function movePlayerDown(code: string, targetPlayerId: string): Promise<LobbyResponse> {
    const res = await fetch(`/api/lobbies/${encodeURIComponent(code)}/order/move-down`, {
        method: 'POST',
        headers: authHeaders(),
        body: JSON.stringify({ targetPlayerId })
    });
    if (!res.ok) throw new Error('Move failed: ' + res.statusText);
    return res.json();
}

export async function shufflePlayers(code: string): Promise<LobbyResponse> {
    const res = await fetch(`/api/lobbies/${encodeURIComponent(code)}/order/shuffle`, {
        method: 'POST',
        headers: authHeaders(),
        body: JSON.stringify({})
    });
    if (!res.ok) throw new Error('Shuffle failed: ' + res.statusText);
    return res.json();
}

export async function castVote(code: string, candidateId: string): Promise<void> {
    const res = await fetch(`/api/lobbies/${encodeURIComponent(code)}/day/vote`, {
        method: 'POST',
        headers: authHeaders(),
        body: JSON.stringify({ candidateId })
    });
    if (!res.ok) throw new Error('Vote failed: ' + res.statusText);
}

export async function getMe(): Promise<MeResponse> {
    const res = await fetch('/api/me', {
        headers: authHeaders()
    });

    if (!res.ok)
        throw new Error('Failed to get profile: ' + res.statusText);

    return res.json();
}

export async function updateMe(request: UpdateMeRequest): Promise<void> {
    const res = await fetch('/api/me', {
        method: 'PUT',
        headers: authHeaders(),
        body: JSON.stringify(request)
    });

    if (!res.ok)
        throw new Error('Failed to update profile: ' + res.statusText);
}

export async function getMyStats(): Promise<UserStatisticsResponse> {
    const res = await fetch('/api/me/stats', {
        headers: authHeaders()
    });

    if (!res.ok)
        throw new Error('Stats fetch failed: ' + res.statusText);

    return res.json();
}

export async function getMyHistory(): Promise<GameHistoryItemResponse[]> {
    const res = await fetch('/api/me/history', {
        headers: authHeaders()
    });

    if (!res.ok)
        throw new Error('History fetch failed: ' + res.statusText);

    return res.json();
}

export type ReconnectResponse = {
    lobby: LobbyResponse;
    gamePhase: 'lobby' | 'role-reveal' | 'night' | 'day' | 'gameover';
    myPlayerId: string;
    myRole: RoleResponse | null;
    seats: SeatInfo[] | null;
    day?: {
        durationSeconds: number;
        remainingSeconds: number;
        showVoteTargets: ShowVotes;
        alivePlayers: SeatInfo[];
        votes?: Record<string, string>;
        enshrouded?: string[];
    };
    night?: {
        activePlayerId?: string | null;
        activeSeatIndex: number;
        hasAbility: boolean;
        alivePlayers: SeatInfo[];
        remainingSeconds: number;
        submittedActions?: Record<string, string>;
    };
};

export async function reconnect(lobbyCode: string): Promise<ReconnectResponse> {
    const res = await fetch(`/api/lobbies/${encodeURIComponent(lobbyCode)}/reconnect`, {
        method: 'POST',
        headers: authHeaders(),
        body: JSON.stringify({})
    });
    if (res.status === 404) throw new Error('Lobby or player not found');
    if (!res.ok) throw new Error('Reconnect failed: ' + res.statusText);
    return res.json();
}

export async function updateConfig(code: string, config: GameConfig): Promise<LobbyResponse> {
    const res = await fetch(`/api/lobbies/${encodeURIComponent(code)}/config`, {
        method: 'POST',
        headers: authHeaders(),
        body: JSON.stringify({ config })
    });
    if (!res.ok) throw new Error('Update config failed: ' + res.statusText);
    return res.json();
}