import type { GameConfig } from './api';


export function roleDescKey(roleName: string, kind: 'Short' | 'Long') {
    return `${roleName}Description${kind}`;
}

/** Interpolation values used by the role descriptions (Dog Tamer only, today). */
export function roleDescParams(config?: GameConfig | null) {
    return {
        numWerewolves: config?.dogTamer?.numWerewolves ?? 1,
        rangeDoubled: (config?.dogTamer?.range ?? 1) * 2,
    };
}

export const ROLE_SIGILS: Record<string, string> = {
    Villager: '🌾',
    Guardian: '⚔️',
    Detective: '🔍',
    Seer: '🔮',
    Fool: '🃏',
    Dogtamer: '🐕',
    DogTamer: '🐕',
    Poacher: '🏹',
    Trailer: '🐾',
    Peeper: '👀',
    Spy: '🕵️‍',
    Ranger: '🧶',
    Alchemist: '🧪',
    Sorcerer: '🕯',
    Innkeeper: '🍻', 
    Werewolf: '🐺',
    Dracula: '🧛',
    Deceiver: '🦹‍',
    Ghost: '👻',
    Charmer: '😴',
};

/** Sigil lookup tolerant of casing / spacing differences. */
export function roleSigil(roleName: string) {
    return ROLE_SIGILS[roleName] ?? '🌾';
}

