import { useEffect, useState } from 'react';

/**
 * Tiny audio + haptics layer.
 *
 * Drop .mp3 files into src/assets/audio/ named after the cues below and they
 * are picked up automatically at build time (import.meta.glob) — missing files
 * simply stay silent, so nothing breaks before you add them.
 */
export type SoundCue =
    | 'create-lobby'
    | 'join-lobby'
    | 'game-start'
    | 'night-start'
    | 'day-start'
    | 'day-resolved'
    | 'night-resolved'
    | 'your-turn';

const files = import.meta.glob('./assets/audio/*.mp3', {
    eager: true,
    query: '?url',
    import: 'default',
}) as Record<string, string>;

const url = (name: string): string | undefined => {
    const hit = Object.entries(files).find(([path]) =>
        path.toLowerCase().endsWith(`/${name}.mp3`));
    return hit?.[1];
};

/** Cue -> file basename in src/assets/audio (first match wins). */
const CUE_FILES: Record<SoundCue, string[]> = {
    'create-lobby': ['create-lobby', 'menu-select'],
    'join-lobby': ['join-lobby', 'menu-select'],
    'game-start': ['game-start', 'among-us-start-sound'],
    'night-start': ['night-start'],
    'day-start': ['day-start'],
    'day-resolved': ['day-resolved'],
    'night-resolved': ['night-resolved'],
    'your-turn': ['your-turn'],
};

// ── Preferences (persisted) ─────────────────────────────────────────────────

const SOUND_KEY = 'prefs.sound';
const VIBRATE_KEY = 'prefs.vibrate';

const read = (key: string) => {
    try { return localStorage.getItem(key) !== 'off'; } catch { return true; }
};

let soundOn = read(SOUND_KEY);
let vibrateOn = read(VIBRATE_KEY);

const listeners = new Set<() => void>();
const emit = () => listeners.forEach(l => l());

export const isSoundOn = () => soundOn;
export const isVibrateOn = () => vibrateOn;

export function setSoundOn(on: boolean) {
    soundOn = on;
    try { localStorage.setItem(SOUND_KEY, on ? 'on' : 'off'); } catch { /* ignore */ }
    emit();
}

export function setVibrateOn(on: boolean) {
    vibrateOn = on;
    try { localStorage.setItem(VIBRATE_KEY, on ? 'on' : 'off'); } catch { /* ignore */ }
    emit();
}

/** Subscribe to preference changes (for React components). */
export function useSoundPrefs() {
    const [state, setState] = useState({ sound: soundOn, vibrate: vibrateOn });
    useEffect(() => {
        const l = () => setState({ sound: soundOn, vibrate: vibrateOn });
        listeners.add(l);
        l();
        return () => { listeners.delete(l); };
    }, []);
    return state;
}

// ── Playback ───────────────────────────────────────────────────────────────

const cache = new Map<string, HTMLAudioElement>();

export function playSound(cue: SoundCue, volume = 0.6) {
    if (!soundOn) return;
    const src = CUE_FILES[cue].map(url).find(Boolean);
    if (!src) return; // no file for this cue yet — stay silent
    try {
        let el = cache.get(src);
        if (!el) { el = new Audio(src); cache.set(src, el); }
        el.currentTime = 0;
        el.volume = volume;
        void el.play().catch(() => { /* autoplay blocked */ });
    } catch { /* ignore */ }
}

export function vibrate(pattern: number | number[] = [70, 60, 140]) {
    if (!vibrateOn) return;
    try { navigator.vibrate?.(pattern); } catch { /* ignore */ }
}

/** Convenience: "it's your turn" — sound + haptics together. */
export function cueYourTurn() {
    playSound('your-turn');
    vibrate([80, 70, 160]);
}
