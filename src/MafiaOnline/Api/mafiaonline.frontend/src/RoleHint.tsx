import React, { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { GameConfig } from './api';
import { roleDescKey, roleDescParams, roleSigil } from './roleText';

interface Props {
    /** Role identifier — config key ("dogTamer") or backend name ("Dog Tamer"). */
    role: string;
    config?: GameConfig | null;
}

/**
 * Little ⓘ next to a role name. Opens on hover/focus on pointer devices and on
 * tap everywhere, so it works the same on mobile. Closes on outside tap or Esc.
 */
export default function RoleHint({ role, config }: Props) {
    const { t } = useTranslation();
    const [open, setOpen] = useState(false);
    const wrapRef = useRef<HTMLSpanElement | null>(null);

    useEffect(() => {
        if (!open) return;
        const onDown = (e: PointerEvent) => {
            if (!wrapRef.current?.contains(e.target as Node)) setOpen(false);
        };
        const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') setOpen(false); };
        document.addEventListener('pointerdown', onDown);
        document.addEventListener('keydown', onKey);
        return () => {
            document.removeEventListener('pointerdown', onDown);
            document.removeEventListener('keydown', onKey);
        };
    }, [open]);

    const text = t(roleDescKey(role, 'Long'), roleDescParams(config));

    return (
        <span className={`role-hint ${open ? 'role-hint--open' : ''}`} ref={wrapRef}>
            <button type="button"
                className="role-hint__btn"
                aria-label={t('roleDetails', { role: t(role) })}
                aria-expanded={open}
                onPointerEnter={e => { if (e.pointerType === 'mouse') setOpen(true); }}
                onPointerLeave={e => { if (e.pointerType === 'mouse') setOpen(false); }}
                onFocus={() => setOpen(true)}
                onBlur={() => setOpen(false)}
                onClick={e => { e.preventDefault(); e.stopPropagation(); setOpen(o => !o); }}>
                ⓘ
            </button>
            <span className="role-hint__pop" role="tooltip">
                <span className="role-hint__title">
                    <span aria-hidden>{roleSigil(role)}</span> {t(role)}
                </span>
                <span className="role-hint__body">{text}</span>
            </span>
        </span>
    );
}
