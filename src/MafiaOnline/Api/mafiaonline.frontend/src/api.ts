import { HubConnection, HubConnectionBuilder } from '@microsoft/signalr';
import type { ActionType } from './NightPhase';

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
    fool: boolean;
    dracula: boolean;
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

export type GuardianConfig = {
    canProtectSelf: boolean;
    canProtectSamePlayerConsecutively: boolean;
};

export type DetectiveConfig = {
    canRevealWerewolves: boolean;
};

export type DogTamerConfig = {
    range: number;
    numWerewolves: number;
};

export type PoacherConfig = {
    allowSelfTarget: boolean;
    mustAlternate: boolean;
};

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
};

export type NightResolvedPayload = {
    killed: string[];
    attackBlocked: boolean;
};

export enum ClueType {
    Detective = "Detective",
    DogTamer = "DogTamer",
    Seer = "Seer",
    Trailer = "Trailer"
}

export interface ClueResponse {
    type: ClueType;
    data: Record<string, unknown>;
    clue: unknown;
}

export interface DetectiveClueResponse extends ClueResponse {
    type: ClueType.Detective;
    clue: string;
}

export interface DogTamerClueResponse extends ClueResponse {
    type: ClueType.DogTamer;
    clue: boolean;
}

export interface SeerClueResponse extends ClueResponse {
    type: ClueType.Seer;
    clue: boolean;
    data: {
        SubjectAName: string;
        SubjectBName: string;
        SubjectA: string;
        SubjectB: string;
    };
}

export interface TrailerClueResponse extends ClueResponse {
    type: ClueType.Trailer;
    clue: string;
}

export type AnyClueResponse =
    | DetectiveClueResponse
    | DogTamerClueResponse
    | SeerClueResponse
    | TrailerClueResponse;

export type LobbyOrderChangedPayload = {
    id: string;
    nickname: string;
    isHost: boolean;
    lobbyPosition: number;
}[];

export type DayStartedPayload = {
    durationSeconds: number;
    alivePlayers: SeatInfo[];
    enshrouded?: string[];
    showVoteTargets: 'Always' | 'Never' | 'AfterVotingEnds';
};

export type WerewolfAttackPayload = {
    actorId: string;
    targetId: string;
    targetName: string;
};

export type VoteCastPayload = {
    voterId: string;
    candidateId: string | null; // null when showVoteTargets is false
    totalVotesCast: number;
};

export type DayResolvedPayload = {
    banished: string[];
    thresholdMet: boolean;
    tally: { playerId: string; votes: number }[];
};

export type GameOverPayload = {
    winner: 'Villagers' | 'Werewolves' | 'Fool';
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

export async function submitNightAction(code: string, actorId: string, targetIdOrTargets: string | string[], actionType: ActionType): Promise<void> {
    const targets = Array.isArray(targetIdOrTargets) ? targetIdOrTargets : [targetIdOrTargets];
    const body: any = { actorId, targets, actionType };
    const res = await fetch(`/api/lobbies/${encodeURIComponent(code)}/night/action`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(body)
    });
    if (!res.ok) throw new Error('Action failed: ' + res.statusText);
}

export async function skipNightTurn(code: string, actorId: string): Promise<void> {
    const res = await fetch(`/api/lobbies/${encodeURIComponent(code)}/night/skip`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ actorId })
    });
    if (!res.ok) throw new Error('Skip failed: ' + res.statusText);
}

export async function getPlayerNightInfo(code: string, playerId: string): Promise<AnyClueResponse | null> {
    const res = await fetch(`/api/lobbies/${encodeURIComponent(code)}/night/clue/${encodeURIComponent(playerId)}`);
    if (res.status === 204) return null;
    if (!res.ok) throw new Error('Night info fetch failed: ' + res.statusText);
    return res.json();
}

export async function movePlayerUp(code: string, requestingPlayerId: string, targetPlayerId: string): Promise<LobbyResponse> {
    const res = await fetch(`/api/lobbies/${encodeURIComponent(code)}/order/move-up`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ requestingPlayerId, targetPlayerId })
    });
    if (!res.ok) throw new Error('Move failed: ' + res.statusText);
    return res.json();
}

export async function movePlayerDown(code: string, requestingPlayerId: string, targetPlayerId: string): Promise<LobbyResponse> {
    const res = await fetch(`/api/lobbies/${encodeURIComponent(code)}/order/move-down`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ requestingPlayerId, targetPlayerId })
    });
    if (!res.ok) throw new Error('Move failed: ' + res.statusText);
    return res.json();
}

export async function shufflePlayers(code: string, requestingPlayerId: string): Promise<LobbyResponse> {
    const res = await fetch(`/api/lobbies/${encodeURIComponent(code)}/order/shuffle`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ requestingPlayerId })
    });
    if (!res.ok) throw new Error('Shuffle failed: ' + res.statusText);
    return res.json();
}

export async function castVote(code: string, voterId: string, candidateId: string): Promise<void> {
    const res = await fetch(`/api/lobbies/${encodeURIComponent(code)}/day/vote`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ voterId, candidateId })
    });
    if (!res.ok) throw new Error('Vote failed: ' + res.statusText);
}

export type ReconnectResponse = {
    lobby: LobbyResponse;
    gamePhase: 'lobby' | 'role-reveal' | 'night' | 'day' | 'gameover';
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
        remainingSeconds: number;
        submittedActions?: Record<string, string>;
    };
};

export async function reconnect(userId: string, lobbyCode: string): Promise<ReconnectResponse> {
    const res = await fetch(`/api/lobbies/${encodeURIComponent(lobbyCode)}/reconnect`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ userId })
    });
    if (res.status === 404) throw new Error('Lobby or player not found');
    if (!res.ok) throw new Error('Reconnect failed: ' + res.statusText);
    return res.json();
}

export async function updateConfig(code: string, requestingPlayerId: string, config: GameConfig): Promise<LobbyResponse> {
    const res = await fetch(`/api/lobbies/${encodeURIComponent(code)}/config`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ requestingPlayerId, config })
    });
    if (!res.ok) throw new Error('Update config failed: ' + res.statusText);
    return res.json();
}