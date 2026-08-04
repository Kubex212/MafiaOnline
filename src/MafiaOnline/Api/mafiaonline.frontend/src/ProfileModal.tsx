import { useEffect, useState } from 'react';
import { getMe, updateMe, getMyStats, getMyHistory } from './api';
import type { UserStatisticsResponse, RoleStatisticsResponse, GameHistoryItemResponse } from './api';
import { useTranslation } from 'react-i18next';

type Props = {
    open: boolean;
    onClose: () => void;
};

export default function ProfileModal({ open, onClose }: Props) {
    const { t } = useTranslation();

    const [me, setMe] = useState<any>(null);
    const [nickname, setNickname] = useState('');
    const [saving, setSaving] = useState(false);
    const [stats, setStats] = useState<UserStatisticsResponse | null>(null);
    const [history, setHistory] = useState<GameHistoryItemResponse[]>([]);

    useEffect(() => {
        if (!open)
            return;

        let cancelled = false;

        async function load() {
            const me = await getMe();
            if (cancelled) return;
            setNickname(me.preferredNickname ?? '');

            if (!me.isAnonymous) {
                const [statsResponse, historyResponse] = await Promise.all([
                    getMyStats(),
                    getMyHistory()
                ]);

                if (cancelled) return;

                setStats(statsResponse);
                setHistory(historyResponse);
            }
        }

        load().catch(err => { console.error('Failed to load profile', err); });

        return () => {
            cancelled = true;
        };
    }, [open]);

    if (!open)
        return null;

    async function save() {
        try {
            setSaving(true);
            await updateMe({ preferredNickname: nickname.trim() });
            onClose();
        }
        catch (e) {
            alert(String(e));
        }
        finally {
            setSaving(false);
        }
    }

    return (
        <div className="profile-modal__backdrop" onClick={onClose}>
            <div className="profile-modal" onClick={e => e.stopPropagation()}>
                <header className="profile-modal__header">
                    <h2>{t('profile')}</h2>
                    <button className="profile-modal__icon-btn" onClick={onClose}>✕</button>
                </header>

                {!me ? (
                    <p>{t('loading')}...</p>
                ) : (
                    <>
                        <label className="profile-modal__field">
                            <span className="profile-modal__label">
                                {t('preferredNickname')}
                            </span>
                            <input className="profile-modal__input" value={nickname} maxLength={16} onChange={e => setNickname(e.target.value)}
                            />
                        </label>

                        <section>
                            <div className="profile-modal__label"> {t('account')} </div>

                            {me.isAnonymous
                                ? <a className="profile-modal__btn profile-modal__btn--discord" href="/api/auth/discord"> {t('loginWithDiscord')} </a>
                                : me.linkedProviders.map((x: string) =>
                                    <div key={x}>✅ {x}</div>
                                )
                            }
                        </section>

                        <footer style={{ marginTop: 20, display: 'flex', gap: 10 }}>
                            <button className="profile-modal__btn" onClick={onClose}>
                                {t('cancel')}
                            </button>

                            <button className="profile-modal__btn profile-modal__btn--primary" disabled={saving} onClick={save}>
                                {saving ? t('saving') : t('save')}
                            </button>
                        </footer>
                    </>
                )}

                {stats && (
                    <div style={{ marginTop: 24 }}>
                        <h3>{t('statistics')}</h3>

                        <div>
                            🎮 {stats.gamesPlayed} games
                        </div>

                        <div>
                            🏆 {stats.wins} wins
                        </div>

                        <div>
                            📈 {stats.winRate.toFixed(1)}% win rate
                        </div>

                        <div>
                            ❤️ {stats.survivalRate.toFixed(1)}% survived
                        </div>

                        <h4 style={{ marginTop: 16 }}>
                            Roles
                        </h4>

                        {(Object.entries(stats.roles) as [string, RoleStatisticsResponse][])
                            .map(([role, data]) => (
                                <div key={role}>
                                    {role}: {data.games} games ({data.winRate.toFixed(0)}% win)
                                </div>
                            ))}
                    </div>
                )}
            </div>
        </div>
    );
}