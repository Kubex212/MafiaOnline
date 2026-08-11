import React, { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useGameLog } from './gameLog';

/** Roomy viewports keep the chronicle open; cramped ones collapse it to a strip. */
function useRoomy() {
    const query = '(min-width: 700px) and (min-height: 820px)';
    const [roomy, setRoomy] = useState(() =>
        typeof window !== 'undefined' ? window.matchMedia(query).matches : true);
    useEffect(() => {
        const mq = window.matchMedia(query);
        const onChange = () => setRoomy(mq.matches);
        mq.addEventListener('change', onChange);
        return () => mq.removeEventListener('change', onChange);
    }, []);
    return roomy;
}

/**
 * One chronicle for the whole game — night clues stay readable during the day
 * and vice versa. Auto-scrolls to the newest line. Collapses into a summary
 * strip on small/short screens so a phase still fits one mobile screen.
 */
export default function EventLog() {
    const { t } = useTranslation();
    const entries = useGameLog();
    const roomy = useRoomy();
    const [open, setOpen] = useState(false);
    const scrollRef = useRef<HTMLDivElement | null>(null);
    const expanded = roomy || open;

    useEffect(() => {
        const el = scrollRef.current;
        if (el) el.scrollTop = el.scrollHeight;
    }, [entries.length, expanded]);

    const last = entries[entries.length - 1];

    return (
        <div className={`event-log ${expanded ? '' : 'event-log--collapsed'}`}>
            <button type="button"
                className="event-log__head"
                aria-expanded={expanded}
                disabled={roomy}
                onClick={() => setOpen(o => !o)}>
                <span>📜 {t('chronicle')}</span>
                {!expanded && last && (
                    <span className="event-log__peek">{last.text}</span>
                )}
                <span className="event-log__count">{entries.length}</span>
                {!roomy && <span className="event-log__chev" aria-hidden>▼</span>}
            </button>
            {expanded && <div className="event-log__scroll" ref={scrollRef}>
                {entries.length === 0 && (
                    <div className="event-log__line event-log__line--system">— {t('chronicleEmpty')}</div>
                )}
                {entries.map(e => (
                    <div key={e.id} className={`event-log__line event-log__line--${e.phase}`}>
                        <span className="event-log__mark" aria-hidden>
                            {e.phase === 'night' ? '🌙' : e.phase === 'day' ? '🌞' : '•'}
                        </span>
                        {e.text}
                    </div>
                ))}
            </div>}
        </div>
    );
}
