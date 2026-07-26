import type { GameConfig } from './api';

/** "Dog Tamer" -> "DogTamer" so it matches the i18n resource keys. */
export function roleKeyBase(roleName: string) {
    return roleName.replace(/\s+/g, '');
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
    'Dog Tamer': '🐕',
    Poacher: '🪤',
    Trailer: '🐾',
    Villager: '🌾',
};
