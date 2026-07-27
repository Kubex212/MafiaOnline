import React, { useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { GameConfig, RoleResponse } from './api';
import { ROLE_SIGILS, roleDescKey, roleDescParams } from './roleText';

interface Props {
    role: RoleResponse;
    config?: GameConfig | null;
}

/**
 * The "Your role: X" pill. Tap / hover to expand the long role description —
 * works on touch as well as with a keyboard.
 */
export default function RoleInfo({ role, config }: Props) {
    const { t } = useTranslation();
    const [open, setOpen] = useState(false);

    const colorVar = role.teamId === 1 ? 'var(--wolf)'
        : role.teamId === 2 ? 'var(--neutral)'
        : 'var(--town-2)';
    const sigil = ROLE_SIGILS[role.name] ?? '🌾';
    const long = t(roleDescKey(role.name, 'Long'), roleDescParams(config));

    return (
        <div className="role-info"
            onMouseEnter={() => setOpen(true)}
            onMouseLeave={() => setOpen(false)}>
            <button type="button"
                className="role-info__badge"
                style={{ borderColor: colorVar }}
                aria-expanded={open}
                onClick={() => setOpen(o => !o)}>
                <span aria-hidden style={{ fontSize: '1.1rem' }}>{sigil}</span>
                <span className="role-info__label">{t('yourRole')}</span>
                <span className="role-info__name" style={{ color: colorVar }}>{t(role.name)}</span>
                <span className="role-info__chev" aria-hidden>▼</span>
            </button>
            {open && <div className="role-info__panel">{long}</div>}
        </div>
    );
}
