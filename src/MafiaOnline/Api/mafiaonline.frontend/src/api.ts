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

export type DayConfig = {
    durationSeconds: number;
    votesRequiredFraction: number;
    drawResolution: 'KickNone' | 'KickRandom' | 'KickAll';
    showVoteTargets: boolean;
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


export type DetectiveNightInfo = {
    type: 'DetectiveClue';
    data: { SubjectNickname: string; IsWerewolf: boolean };
};

export type DogTamerNightInfo = {
    type: 'DogTamerClue';
    data: { DidBark: boolean };
};

export type SeerNightInfo = {
    type: 'SeerClue';
    data: { SubjectA: string; SubjectB: string; SubjectAName: string; SubjectBName: string; SameTeam: boolean };
};

export type TrailerNightInfo = {
    type: 'TrailerClue';
    data: { Direction: 'Left' | 'Right' | 'None' };
};

export type PlayerNightInfoResponse = DetectiveNightInfo | DogTamerNightInfo | SeerNightInfo | TrailerNightInfo;

export type LobbyOrderChangedPayload = {
    id: string;
    nickname: string;
    isHost: boolean;
    lobbyPosition: number;
}[];

export type DayStartedPayload = {
    durationSeconds: number;
    showVoteTargets: boolean;
    alivePlayers: SeatInfo[];
    enshrouded?: string[];
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

export async function getPlayerNightInfo(code: string, playerId: string): Promise<PlayerNightInfoResponse | null> {
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
        showVoteTargets: boolean;
        alivePlayers: SeatInfo[];
        votes?: Record<string, string>;
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