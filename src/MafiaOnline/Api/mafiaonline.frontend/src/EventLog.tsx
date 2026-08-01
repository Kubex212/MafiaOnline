import React, { useEffect, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import { useGameLog } from './gameLog';

/**
 * One chronicle for the whole game — night clues stay readable during the day
 * and vice versa. Auto-scrolls to the newest line.
 */
export default function EventLog() {
    const { t } = useTranslation();
    const entries = useGameLog();
    const scrollRef = useRef<HTMLDivElement | null>(null);

    useEffect(() => {
        const el = scrollRef.current;
        if (el) el.scrollTop = el.scrollHeight;
    }, [entries.length]);

    return (
        <div className="event-log">
            <div className="event-log__head">
                <span>📜 {t('chronicle')}</span>
                <span className="event-log__count">{entries.length}</span>
            </div>
            <div className="event-log__scroll" ref={scrollRef}>
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
            </div>
        </div>
    );
}
