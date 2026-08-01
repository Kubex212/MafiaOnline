import { useSyncExternalStore } from 'react';

export type LogPhase = 'night' | 'day' | 'system';
export type LogEntry = { id: number; text: string; phase: LogPhase };

let entries: LogEntry[] = [];
let nextId = 0;
const subscribers = new Set<() => void>();

const emit = () => subscribers.forEach(fn => fn());

/** Append a line to the single, game-long chronicle. */
export function addLog(text: string, phase: LogPhase = 'system') {
    if (!text) return;
    const last = entries[entries.length - 1];
    // Guard against accidental duplicates from re-delivered hub events.
    if (last && last.text === text && last.phase === phase) return;
    entries = [...entries, { id: nextId++, text, phase }];
    emit();
}

/** Wipe the chronicle — called once when a new game starts. */
export function clearLog() {
    entries = [];
    emit();
}

export function useGameLog(): LogEntry[] {
    return useSyncExternalStore(
        cb => { subscribers.add(cb); return () => { subscribers.delete(cb); }; },
        () => entries,
        () => entries,
    );
}
