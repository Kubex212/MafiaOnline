import type { GameConfig } from './api';

/**
 * Canonical resource base names for role descriptions, keyed by the lowercase
 * form of every spelling we may receive (backend role name, config key, ...).
 * Note the resources spell the dog tamer "Dogtamer".
 */
const DESC_BASE: Record<string, string> = {
    werewolf: 'Werewolf',
    villager: 'Villager',
    guardian: 'Guardian',
    detective: 'Detective',
    dogtamer: 'DogTamer',
    seer: 'Seer',
    fool: 'Fool',
    trailer: 'Trailer',
    poacher: 'Poacher',
    dracula: 'Dracula',
};

/** "Dog Tamer" / "dogTamer" -> "Dogtamer" so it matches the i18n resource keys. */
export function roleKeyBase(roleName: string) {
    const squashed = roleName.replace(/\s+/g, '');
    return DESC_BASE[squashed.toLowerCase()] ?? squashed;
}

export function roleDescKey(roleName: string, kind: 'Short' | 'Long') {
    return `${roleKeyBase(roleName)}Description${kind}`;
}

/** Interpolation values used by the role descriptions (Dog Tamer only, today). */
export function roleDescParams(config?: GameConfig | null) {
    return {
        numWerewolves: config?.dogTamer?.numWerewolves ?? 1,
        rangeDoubled: (config?.dogTamer?.range ?? 1) * 2,
    };
}

export const ROLE_SIGILS: Record<string, string> = {
    Werewolf: '🐺',
    Guardian: '🛡',
    Detective: '🔍',
    Seer: '🔮',
    Fool: '🃏',
    Dogtamer: '🐕',
    DogTamer: '🐕',
    Poacher: '🪤',
    Trailer: '🐾',
    Villager: '🌾',
    Dracula: '🧛',
    Deceiver: '🦹‍'
};

/** Sigil lookup tolerant of casing / spacing differences. */
export function roleSigil(roleName: string) {
    return ROLE_SIGILS[roleKeyBase(roleName)] ?? ROLE_SIGILS[roleName] ?? '🌾';
}
