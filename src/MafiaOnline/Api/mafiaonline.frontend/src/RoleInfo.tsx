import React, { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { GameConfig, RoleResponse } from './api';
import { ROLE_SIGILS, roleDescKey, roleDescParams } from './roleText';

interface Props {
    role: RoleResponse;
    config?: GameConfig | null;
}

/** True when the viewport is roomy enough to keep the description always open. */
function useRoomy() {
    const query = '(min-width: 700px) and (min-height: 760px)';
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
 * The "Your role: X" pill. On roomy screens the long description is always
 * visible; on small screens it collapses into a tap-to-expand panel so the
 * whole phase still fits on one mobile screen.
 */
export default function RoleInfo({ role, config }: Props) {
    const { t } = useTranslation();
    const [open, setOpen] = useState(false);
    const roomy = useRoomy();

    const colorVar = role.teamId === 1 ? 'var(--wolf)'
        : role.teamId === 2 ? 'var(--neutral)'
        : 'var(--town-2)';
    const sigil = ROLE_SIGILS[role.name] ?? '🌾';
    const long = t(roleDescKey(role.name, 'Long'), roleDescParams(config));
    const shown = roomy || open;

    return (
        <div className={`role-info ${roomy ? 'role-info--static' : ''}`}>
            <button type="button"
                className="role-info__badge"
                style={{ borderColor: colorVar }}
                aria-expanded={shown}
                disabled={roomy}
                onClick={() => setOpen(o => !o)}>
                <span aria-hidden style={{ fontSize: '1.1rem' }}>{sigil}</span>
                <span className="role-info__label">{t('yourRole')}</span>
                <span className="role-info__name" style={{ color: colorVar }}>{t(role.name)}</span>
                {role.alibi && (
                    <span className="role-info__alibi">{t('alibiShort')}: {t(role.alibi)}</span>
                )}
                {!roomy && <span className="role-info__chev" aria-hidden>▼</span>}
            </button>
            {shown && <div className="role-info__panel" style={{ borderColor: colorVar }}>{long}</div>}
        </div>
    );
}
