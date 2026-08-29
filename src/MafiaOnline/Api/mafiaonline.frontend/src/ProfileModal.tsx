import { useEffect, useState } from 'react';
import { getMe, updateMe, getMyStats, getMyHistory } from './api';
import type { MeResponse, UserStatisticsResponse, RoleStatisticsResponse, GameHistoryItemResponse } from './api';
import { useTranslation } from 'react-i18next';
import { roleSigil } from './roleText';

type Props = {
    open: boolean;
    onClose: () => void;
    onSaved?: (nickname: string) => void;
};

type Tab = 'profile' | 'stats';

function fmtDuration(d: string) {
    // "00:12:34" / "00:12:34.567" → "12m 34s"
    const m = /^(\d+):(\d{2}):(\d{2})/.exec(d);
    if (!m) return d;
    const h = Number(m[1]), mi = Number(m[2]), s = Number(m[3]);
    return h > 0 ? `${h}h ${mi}m` : `${mi}m ${s}s`;
}

export default function ProfileModal({ open, onClose, onSaved }: Props) {
    const { t, i18n } = useTranslation();

    const [me, setMe] = useState<MeResponse | null>(null);
    const [nickname, setNickname] = useState('');
    const [saving, setSaving] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const [stats, setStats] = useState<UserStatisticsResponse | null>(null);
    const [history, setHistory] = useState<GameHistoryItemResponse[]>([]);
    const [tab, setTab] = useState<Tab>('profile');

    useEffect(() => {
        if (!open) return;
        const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') onClose(); };
        window.addEventListener('keydown', onKey);
        return () => window.removeEventListener('keydown', onKey);
    }, [open, onClose]);

    useEffect(() => {
        if (!open) return;

        let cancelled = false;
        setMe(null);
        setStats(null);
        setHistory([]);
        setError(null);
        setTab('profile');

        (async () => {
            const profile = await getMe();
            if (cancelled) return;
            setMe(profile);
            setNickname(profile.preferredNickname ?? '');

            if (!profile.isAnonymous) {
                const [s, h] = await Promise.all([getMyStats(), getMyHistory()]);
                if (cancelled) return;
                setStats(s);
                setHistory(h);
            }
        })().catch(err => {
            if (!cancelled) setError(String(err?.message ?? err));
        });

        return () => { cancelled = true; };
    }, [open]);

    if (!open) return null;

    async function save() {
        try {
            setSaving(true);
            setError(null);
            const value = nickname.trim();
            await updateMe({ preferredNickname: value });
            onSaved?.(value);
            onClose();
        } catch (e: any) {
            setError(String(e?.message ?? e));
        } finally {
            setSaving(false);
        }
    }

    const roleRows = stats
        ? (Object.entries(stats.roles) as [string, RoleStatisticsResponse][])
            .sort((a, b) => b[1].games - a[1].games)
        : [];

    return (
        <div className="modal__backdrop" onClick={onClose} role="presentation">
            <div className="modal profile-modal" role="dialog" aria-modal="true"
                aria-label={t('profile')} onClick={e => e.stopPropagation()}>

                <header className="modal__header">
                    <h2 className="modal__title">{t('profile')}</h2>
                    <button className="modal__close" onClick={onClose} aria-label={t('close')}>✕</button>
                </header>

                {!me && !error && <p className="modal__loading">{t('loading')}</p>}
                {error && <p className="modal__error">{error}</p>}

                {me && (
                    <>
                        {!me.isAnonymous && (
                            <div className="tabs" role="tablist">
                                <button role="tab" aria-selected={tab === 'profile'}
                                    className={`tabs__tab ${tab === 'profile' ? 'is-active' : ''}`}
                                    onClick={() => setTab('profile')}>{t('profile')}</button>
                                <button role="tab" aria-selected={tab === 'stats'}
                                    className={`tabs__tab ${tab === 'stats' ? 'is-active' : ''}`}
                                    onClick={() => setTab('stats')}>{t('statistics')}</button>
                            </div>
                        )}

                        {tab === 'profile' && (
                            <div className="modal__body">
                                <label className="field">
                                    <span className="field__label">{t('preferredNickname')}</span>
                                    <input className="input" value={nickname} maxLength={16}
                                        placeholder={t('nickname')}
                                        onChange={e => setNickname(e.target.value)} />
                                    <span className="field__hint">{t('nicknameHint')}</span>
                                </label>

                                <section className="profile-card">
                                    <div className="field__label">{t('account')}</div>
                                    {me.isAnonymous ? (
                                        <>
                                            <p className="profile-card__hint">{t('guestHint')}</p>
                                            <a className="btn btn--discord btn--block" href="/api/auth/discord">
                                                {t('loginWithDiscord')}
                                            </a>
                                        </>
                                    ) : (
                                        <ul className="profile-providers">
                                            {me.linkedProviders.map(p => (
                                                <li key={p}><span aria-hidden>✓</span> {p}</li>
                                            ))}
                                        </ul>
                                    )}
                                </section>

                                <footer className="modal__actions">
                                    <button className="btn btn--ghost" onClick={onClose}>{t('cancel')}</button>
                                    <button className="btn btn--primary" disabled={saving || !nickname.trim()}
                                        onClick={save}>
                                        {saving ? t('saving') : t('save')}
                                    </button>
                                </footer>
                            </div>
                        )}

                        {tab === 'stats' && (
                            <div className="modal__body">
                                {!stats ? <p className="modal__loading">{t('loading')}</p> : (
                                    <>
                                        <div className="stat-grid">
                                            <div className="stat"><strong>{stats.gamesPlayed}</strong><span>{t('gamesPlayed')}</span></div>
                                            <div className="stat"><strong>{stats.wins}</strong><span>{t('wins')}</span></div>
                                            <div className="stat"><strong>{stats.winRate.toFixed(0)}%</strong><span>{t('winRate')}</span></div>
                                            <div className="stat"><strong>{stats.survivalRate.toFixed(0)}%</strong><span>{t('survivalRate')}</span></div>
                                        </div>

                                        {roleRows.length > 0 && (
                                            <section>
                                                <div className="field__label">{t('roleBreakdown')}</div>
                                                <ul className="role-stats">
                                                    {roleRows.map(([role, d]) => (
                                                        <li className="role-row" key={role}>
                                                            <span className="role-stats__sigil" aria-hidden>{roleSigil(role)}</span>
                                                            <span className="role-stats__name">{t(role)}</span>
                                                            <span className="role-stats__bar">
                                                                <i style={{ width: `${Math.max(0, d.winRate)}%` }} />
                                                            </span>
                                                            <span className="role-stats__num">
                                                                {d.games}× · {d.winRate.toFixed(0)}%
                                                            </span>
                                                        </li>
                                                    ))}
                                                </ul>
                                            </section>
                                        )}

                                        <section>
                                            <div className="field__label">{t('recentGames')}</div>
                                            {history.length === 0
                                                ? <p className="profile-card__hint">{t('noGames')}</p>
                                                : (
                                                    <ul className="history">
                                                        {history.slice(0, 10).map((g, i) => (
                                                            <li key={i} className={g.won ? 'history__item is-won' : 'history__item'}>
                                                                <span className="history__sigil" aria-hidden>{roleSigil(g.role)}</span>
                                                                <span className="history__role">{t(g.role)}</span>
                                                                <span className="history__meta">
                                                                    {new Date(g.playedAtUtc).toLocaleDateString(i18n.language)} · {g.playerCount}p · {fmtDuration(g.duration)}
                                                                </span>
                                                                <span className="history__badges">
                                                                    <em className={g.won ? 'tag tag--win' : 'tag tag--loss'}>
                                                                        {g.won ? t('won') : t('lost')}
                                                                    </em>
                                                                    {!g.survived && <em className="tag" title={t('died')}>💀</em>}
                                                                </span>
                                                            </li>
                                                        ))}
                                                    </ul>
                                                )}
                                        </section>
                                    </>
                                )}
                            </div>
                        )}
                    </>
                )}
            </div>
        </div>
    );
}
