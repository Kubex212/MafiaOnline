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

function sleep(ms: number) {
    return new Promise(resolve => setTimeout(resolve, ms));
}

export async function getOrCreateSession(): Promise<{ userId: string }> {
    // If Discord just redirected back with ?token=xxx, store it and clean the URL
    const params = new URLSearchParams(window.location.search);
    const urlToken = params.get('token');
    if (urlToken) {
        storeToken(urlToken);
        window.history.replaceState({}, '', window.location.pathname);
    }

    const MAX_ATTEMPTS = 3;
    let attempt = 0;
    let backoffMs = 250;

    // Try to initialize a session, retrying on transient errors. If a stored
    // token is invalid (401/403) try once more without the token.
    let lastErr: unknown = null;
    while (attempt < MAX_ATTEMPTS) {
        attempt++;
        const existing = getStoredToken();
        const headers: Record<string, string> = { 'Content-Type': 'application/json' };
        if (existing) headers['X-Session-Token'] = existing;

        try {
            const res = await fetch('/api/auth/session', { headers });
            if (res.ok) {
                const data: { userId: string; token?: string } = await res.json();
                if (data.token) storeToken(data.token);
                return { userId: data.userId };
            }

            // If token is invalid or unauthorized, clear it and retry immediately
            if (res.status === 401 || res.status === 403) {
                sessionStorage.removeItem(SESSION_KEY);
                lastErr = new Error('Session unauthorized, cleared stored token');
                // retry quickly without token
                await sleep(backoffMs);
                backoffMs *= 2;
                continue;
            }

            // Treat server errors as transient and retry
            if (res.status >= 500 && res.status < 600) {
                const text = await res.text().catch(() => '');
                lastErr = new Error('Server error during session init: ' + res.status + ' ' + text);
                await sleep(backoffMs);
                backoffMs *= 2;
                continue;
            }

            // Other non-success statuses are treated as terminal
            const body = await res.text().catch(() => '');
            throw new Error('Session init failed: ' + res.status + ' ' + body);
        }
        catch (err) {
            // Network or other unexpected error — retry a few times
            lastErr = err;
            await sleep(backoffMs);
            backoffMs *= 2;
        }
    }

    throw lastErr ?? new Error('Session init failed after retries');
}

export function getDiscordLoginUrl(returnUrl?: string): string {
    let url = '/api/auth/discord';
    if (returnUrl) url += '?returnUrl=' + encodeURIComponent(returnUrl);
    return url;
}

export function getGoogleLoginUrl(returnUrl?: string): string {
    let url = '/api/auth/google';
    if (returnUrl) url += '?returnUrl=' + encodeURIComponent(returnUrl);
    return url;
}

function authHeaders(): Record<string, string> {
    const headers: Record<string, string> = { 'Content-Type': 'application/json' };
    const token = getStoredToken();
    if (token) headers['X-Session-Token'] = token;
    return headers;
}

// Helper to perform fetch with automatic retry on transient errors and
// a single retry clearing stored token on 401/403.
async function fetchWithAuth(input: RequestInfo, init: RequestInit = {}, maxAttempts = 1): Promise<Response> {
    let attempt = 0;
    let backoff = 200;

    while (true) {
        attempt++;
        const mergedHeaders: Record<string, string> = {
            ...(init.headers as Record<string, string> || {}),
            ...authHeaders()
        };

        try {
            const res = await fetch(input, { ...init, headers: mergedHeaders });

            if (res.ok) return res;

            // If unauthorized and we had a stored token, clear it and retry
            if ((res.status === 401 || res.status === 403) && getStoredToken()) {
                sessionStorage.removeItem(SESSION_KEY);
                if (attempt < maxAttempts) {
                    await sleep(backoff);
                    backoff *= 2;
                    continue;
                }
            }

            return res;
        } catch (err) {
            if (attempt >= maxAttempts) throw err;
            await sleep(backoff);
            backoff *= 2;
        }
    }
}

// ── Types ────────────────────────────────────────────────────────────────────

export type PlayerResponse = {
    id: string;
    userId: string;
    nickname: string;
    isHost: boolean;
    lobbyPosition: number;
    seatIndex: number;
    ready?: boolean;
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
    showAlibi: boolean;
};

export const DEFAULT_WEREWOLF_CONFIG: WerewolfConfig = {
    drawResolution: 'KickRandom',
    showVoteTargets: 'Always',
    showAlibi: true,
};

export type GuardianConfig = { canProtectSelf: boolean; mustAlternate: boolean; announceProtection: boolean };
export type DetectiveConfig = { canRevealWerewolves: boolean; };
export type DogTamerConfig = { range: number; numWerewolves: number; };
export type PoacherConfig = { allowSelfTarget: boolean; mustAlternate: boolean; };
export type GhostConfig = { shouldGetAlibi: boolean; };

export type DrunkConfig = {
    drunkOccuranceChance: number;
    drunkEffectChance: number;
    announceDrunkOccurance: boolean;
};

export type InnkeeperConfig = {
    suspectCount: number;
};

export const roleTypes = [
    'Villager',
    'Werewolf',
    'Guardian',
    'Detective',
    'DogTamer',
    'Poacher',
    'Trailer',
    'Seer',
    'Peeper',
    'Spy',
    'Ranger',
    'Alchemist',
    'Sorcerer',
    'Innkeeper',
    'Fool',
    'Dracula',
    'Deceiver',
    'Ghost',
    'Charmer',
] as const;

export const nonStandardRoleTypes = roleTypes.filter(
    role => role !== 'Villager' && role !== 'Werewolf'
);

export type RoleType = typeof roleTypes[number];

export type GameConfig = {
    enabledRoles: RoleType[];
    werewolfCount: number;
    turnDurationSeconds: number;
    guardian: GuardianConfig;
    detective: DetectiveConfig;
    dogTamer: DogTamerConfig;
    day: DayConfig;
    poacher: PoacherConfig;
    werewolf: WerewolfConfig;
    ghost: GhostConfig;
    innkeeper: InnkeeperConfig;
    drunk?: DrunkConfig;
};

export type LobbyResponse = {
    code: string;
    started: boolean;
    players: PlayerResponse[];
    config: GameConfig;
    rematchCode?: string | null;
    isPrivate: boolean;
    hasPassword: boolean;
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
    gameId?: string;
    activePlayerId: string;
    activeSeatIndex: number;
    hasAbility: boolean;
    timeoutSeconds: number;
    minTargets?: number;
    maxTargets?: number;
    forbiddenTargets?: string[];
};

export type NightStartedPayload = {
    gameId?: string;
    durationSeconds: number;
    playerInfos: Array<{
        playerId: string;
        hasAbility: boolean;
        minTargets: number;
        maxTargets: number;
        forbiddenTargets: string[];
    }>;
    allAlivePlayers: SeatInfo[];
};

export type TurnFinishedPayload = {
    gameId?: string;
    actorId: string;
    actorName: string;
};

export type NightResolvedPayload = { gameId?: string; killed: string[]; attackBlocked: boolean; };

export const ClueType = {
    Detective: "Detective",
    DogTamer: "DogTamer",
    Seer: "Seer",
    Trailer: "Trailer",
    Peeper: "Peeper",
    Spy: "Spy",
    Ranger: "Ranger",
    Alchemist: "Alchemist",
    Sorcerer: "Sorcerer",
    Innkeeper: "Innkeeper",
} as const;
export type ClueType = typeof ClueType[keyof typeof ClueType];

export interface ClueResponse { type: ClueType | string; data: Record<string, unknown>; clue: unknown; }
export interface DetectiveClueResponse extends ClueResponse { type: typeof ClueType.Detective; clue: string; }
export interface DogTamerClueResponse extends ClueResponse { type: typeof ClueType.DogTamer; clue: boolean; }
export interface SeerClueResponse extends ClueResponse {
    type: typeof ClueType.Seer; clue: boolean;
    data: { SubjectAName: string; SubjectBName: string; SubjectA: string; SubjectB: string; };
}
export interface TrailerClueResponse extends ClueResponse { type: typeof ClueType.Trailer; clue: string; }
export interface PeeperClueResponse extends ClueResponse { type: typeof ClueType.Peeper; clue: boolean; }
export interface SpyClueResponse extends ClueResponse { type: typeof ClueType.Spy; clue: boolean; }
export interface RangerClueResponse extends ClueResponse { type: typeof ClueType.Ranger; clue: number; }
export interface AlchemistClueResponse extends ClueResponse { type: typeof ClueType.Alchemist; clue: any; }
export interface SorcererClueResponse extends ClueResponse { type: typeof ClueType.Sorcerer; clue: number; }
export interface InnkeeperClueResponse extends ClueResponse { type: typeof ClueType.Innkeeper; clue: string[]; }

export type AnyClueResponse =
    | DetectiveClueResponse | DogTamerClueResponse | SeerClueResponse | TrailerClueResponse | PeeperClueResponse | SpyClueResponse | RangerClueResponse | AlchemistClueResponse | SorcererClueResponse | InnkeeperClueResponse;

export type LobbyOrderChangedPayload = {
    players: PlayerResponse[];
};

export type GameStartedPayload = {
    gameId?: string;
    seats: SeatInfo[];
    drunkPresent: boolean;
    config?: GameConfig;
};

export type DayStartedPayload = {
    gameId?: string;
    durationSeconds: number;
    alivePlayers: SeatInfo[];
    enshrouded?: string[];
    asleep?: string[];
    showVoteTargets: 'Always' | 'Never' | 'AfterVotingEnds';
};

export type WerewolfAttackPayload = { gameId?: string; actorId: string; targetId: string; targetName: string; };
export type VoteCastPayload = { gameId?: string; voterId: string; candidateId: string | null; totalVotesCast: number; };
export type DayResolvedPayload = { gameId?: string; banished: string[]; thresholdMet: boolean; tally: { playerId: string; votes: number }[]; };
export type GameOverPayload = { gameId?: string; winner: 'Villagers' | 'Werewolves' | 'Fool'; drunkId?: string | null; drunkName?: string | null; rematchCode?: string | null };

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

export type LobbyGameHistoryItemResponse = {
    gameId: string;
    lobbyCode: string;
    startedAtUtc: string;
    finishedAtUtc: string;
    winner: string;
    playerCount: number;
    dayCount: number;
    duration: string;
};

// ── API calls ────────────────────────────────────────────────────────────────

export async function createLobby(nickname: string): Promise<LobbyResponse> {
    const res = await fetchWithAuth('/api/lobbies', {
        method: 'POST',
        body: JSON.stringify({ nickname })
    });
    if (!res.ok) throw new Error('Create failed: ' + res.statusText);
    return res.json();
}

export async function joinLobby(code: string, nickname: string, password?: string): Promise<LobbyResponse> {
    const res = await fetchWithAuth('/api/lobbies/join', {
        method: 'POST',
        body: JSON.stringify({ code, nickname, password: password ?? null })
    });
    if (!res.ok) {
        if (res.status === 422) throw new Error('passwordRequired');
        throw new Error('Join failed: ' + res.statusText);
    }
    return res.json();
}

export async function getLobby(code: string): Promise<LobbyResponse> {
    const res = await fetchWithAuth(`/api/lobbies/${encodeURIComponent(code)}`);
    if (!res.ok) throw new Error('Get lobby failed: ' + res.statusText);
    return res.json();
}

export async function startLobby(code: string): Promise<LobbyResponse> {
    const res = await fetchWithAuth(`/api/lobbies/${encodeURIComponent(code)}/start`, {
        method: 'POST',
        body: JSON.stringify({})
    });
    if (!res.ok) throw new Error('Start failed: ' + res.statusText);
    return res.json();
}

export async function getMyRole(code: string): Promise<RoleResponse> {
    const res = await fetchWithAuth(`/api/lobbies/${encodeURIComponent(code)}/role`);
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
    const res = await fetchWithAuth(`/api/lobbies/${encodeURIComponent(code)}/night/action`, {
        method: 'POST',
        body: JSON.stringify({ targets: targetArr, actionType })
    });
    if (!res.ok) throw new Error('Action failed: ' + res.statusText);
}

export async function skipNightTurn(code: string): Promise<void> {
    const res = await fetchWithAuth(`/api/lobbies/${encodeURIComponent(code)}/night/skip`, {
        method: 'POST',
        body: JSON.stringify({})
    });
    if (!res.ok) throw new Error('Skip failed: ' + res.statusText);
}

export async function getPlayerNightInfo(code: string): Promise<AnyClueResponse | null> {
    const res = await fetchWithAuth(`/api/lobbies/${encodeURIComponent(code)}/night/clue`);
    if (res.status === 204) return null;
    if (!res.ok) throw new Error('Night info fetch failed: ' + res.statusText);
    return res.json();
}

export async function movePlayerUp(code: string, targetPlayerId: string): Promise<LobbyResponse> {
    const res = await fetchWithAuth(`/api/lobbies/${encodeURIComponent(code)}/order/move-up`, {
        method: 'POST',
        body: JSON.stringify({ targetPlayerId })
    });
    if (!res.ok) throw new Error('Move failed: ' + res.statusText);
    return res.json();
}

export async function movePlayerDown(code: string, targetPlayerId: string): Promise<LobbyResponse> {
    const res = await fetchWithAuth(`/api/lobbies/${encodeURIComponent(code)}/order/move-down`, {
        method: 'POST',
        body: JSON.stringify({ targetPlayerId })
    });
    if (!res.ok) throw new Error('Move failed: ' + res.statusText);
    return res.json();
}

export async function shufflePlayers(code: string): Promise<LobbyResponse> {
    const res = await fetchWithAuth(`/api/lobbies/${encodeURIComponent(code)}/order/shuffle`, {
        method: 'POST',
        body: JSON.stringify({})
    });
    if (!res.ok) throw new Error('Shuffle failed: ' + res.statusText);
    return res.json();
}

export async function castVote(code: string, candidateId: string | null): Promise<void> {
    const res = await fetchWithAuth(`/api/lobbies/${encodeURIComponent(code)}/day/vote`, {
        method: 'POST',
        body: JSON.stringify({ candidateId })
    });
    if (!res.ok) throw new Error('Vote failed: ' + res.statusText);
}

export async function getMe(): Promise<MeResponse> {
    const res = await fetchWithAuth('/api/me');

    if (!res.ok)
        throw new Error('Failed to get profile: ' + res.statusText);

    return res.json();
}

export async function updateMe(request: UpdateMeRequest): Promise<void> {
    const res = await fetchWithAuth('/api/me', {
        method: 'PUT',
        body: JSON.stringify(request)
    });

    if (!res.ok)
        throw new Error('Failed to update profile: ' + res.statusText);
}

export async function getMyStats(): Promise<UserStatisticsResponse> {
    const res = await fetchWithAuth('/api/me/stats');

    if (!res.ok) throw new Error('Stats fetch failed: ' + res.statusText);

    return res.json();
}

export async function getMyHistory(): Promise<GameHistoryItemResponse[]> {
    const res = await fetchWithAuth('/api/me/history');

    if (!res.ok) throw new Error('History fetch failed: ' + res.statusText);

    return res.json();
}

export async function getLobbyHistory(code: string, take = 20): Promise<LobbyGameHistoryItemResponse[]> {
    const res = await fetchWithAuth(`/api/lobbies/${encodeURIComponent(code)}/history?take=${take}`);
    if (!res.ok) throw new Error('Lobby history fetch failed: ' + res.statusText);
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
        votes?: Record<string, string | null>;
        enshrouded?: string[];
        asleep?: string[];
    };
    night?: {
        durationSeconds: number;
        remainingSeconds: number;
        playerInfos: Array<{
            playerId: string;
            hasAbility: boolean;
            minTargets: number;
            maxTargets: number;
            forbiddenTargets: string[];
        }>;
        allAlivePlayers: SeatInfo[];
        submittedPlayers?: string[];
        // concrete submitted actions so client can reconstruct per-player submission state
        submittedActions?: Array<{
            actorId: string;
            actionType: number; // matches Engine.Night.ActionType enum
            targets: string[];
        }>;
    };
    gameOver?: GameOverPayload;
};

export async function reconnect(lobbyCode: string): Promise<ReconnectResponse> {
    const res = await fetchWithAuth(`/api/lobbies/${encodeURIComponent(lobbyCode)}/reconnect`, {
        method: 'POST',
        body: JSON.stringify({
        })
    });
    if (res.status === 404) throw new Error('Lobby or player not found');
    if (!res.ok) throw new Error('Reconnect failed: ' + res.statusText);
    return res.json();
}

export async function updateConfig(code: string, config: GameConfig): Promise<LobbyResponse> {
    const res = await fetchWithAuth(`/api/lobbies/${encodeURIComponent(code)}/config`, {
        method: 'POST',
        body: JSON.stringify({ config })
    });
    if (!res.ok) throw new Error('Update config failed: ' + res.statusText);
    return res.json();
}

export async function updatePrivacySettings(code: string, isPrivate: boolean, password: string | null): Promise<LobbyResponse> {
    const res = await fetchWithAuth(`/api/lobbies/${encodeURIComponent(code)}/privacy`, {
        method: 'POST',
        body: JSON.stringify({ isPrivate, password })
    });
    if (!res.ok) throw new Error('Update privacy failed: ' + res.statusText);
    return res.json();
}

export async function createRematch(code: string): Promise<LobbyResponse> {
    const res = await fetchWithAuth(`/api/lobbies/${encodeURIComponent(code)}/rematch`, {
        method: 'POST',
        body: JSON.stringify({})
    });
    if (!res.ok) throw new Error('Rematch failed: ' + res.statusText);
    return res.json();
}

export async function leaveLobby(code: string): Promise<void> {
    const res = await fetchWithAuth(`/api/lobbies/${encodeURIComponent(code)}/leave`, {
        method: 'POST',
        body: JSON.stringify({})
    });
    if (res.status === 204) return;
    if (!res.ok) throw new Error('Leave failed: ' + res.statusText);
}

export async function kickPlayer(code: string, targetPlayerId: string): Promise<void> {
    const res = await fetchWithAuth(`/api/lobbies/${encodeURIComponent(code)}/kick`, {
        method: 'POST',
        body: JSON.stringify({ targetPlayerId })
    });
    if (res.status === 204) return;
    if (!res.ok) throw new Error('Kick failed: ' + res.statusText);
}

export async function markReadyForRematch(code: string): Promise<void> {
    const res = await fetchWithAuth(`/api/lobbies/${encodeURIComponent(code)}/ready-for-rematch`, { method: 'POST' });
    if (!res.ok) throw new Error('Mark ready failed: ' + res.statusText);
}

export async function listAvailableLobbies(skip = 0, take = 50): Promise<LobbyResponse[]> {
    const res = await fetchWithAuth(`/api/lobbies?skip=${skip}&take=${take}`);
    if (!res.ok) throw new Error('List lobbies failed: ' + res.statusText);
    return res.json();
}