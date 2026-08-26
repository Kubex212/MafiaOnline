import React from 'react';
import { useTranslation } from 'react-i18next';
import { isSoundOn, isVibrateOn, setSoundOn, setVibrateOn, useSoundPrefs, playSound } from './sound';

interface Props {
    /** Connection indicator text is only shown outside of an active game. */
    connected?: boolean;
    showStatus?: boolean;
    onProfile?: () => void;
    compact?: boolean;
    showRoles?: boolean;
    onToggleRoles?: () => void;
}

const LANGS = ['en', 'pl'] as const;

export default function TopBar({ connected, showStatus = true, onProfile, compact = false, showRoles, onToggleRoles }: Props) {
    const { t, i18n } = useTranslation();
    const prefs = useSoundPrefs();

    const toggleSound = () => {
        const next = !isSoundOn();
        setSoundOn(next);
        if (next) playSound('join-lobby', 0.4);
    };

    return (
        <div className={`topbar ${compact ? 'topbar--compact' : ''}`}>
            {showStatus && (
                <div className={`status-dot ${connected ? '' : 'status-dot--off'}`}>
                    {connected ? t('connected') : t('connecting')}
                </div>
            )}

            <div className="topbar__spacer" />

            <div className="lang-switch" role="group" aria-label={t('language')}>
                {LANGS.map(lng => (
                    <button key={lng}
                        type="button"
                        className={`lang-switch__btn ${i18n.resolvedLanguage?.startsWith(lng) ? 'is-active' : ''}`}
                        onClick={() => i18n.changeLanguage(lng)}
                        aria-pressed={i18n.resolvedLanguage?.startsWith(lng)}>
                        {lng.toUpperCase()}
                    </button>
                ))}
            </div>

            <button type="button"
                className={`icon-toggle ${prefs.sound ? 'is-on' : ''}`}
                onClick={toggleSound}
                title={prefs.sound ? t('soundOn') : t('soundOff')}
                aria-label={prefs.sound ? t('soundOn') : t('soundOff')}>
                {prefs.sound ? '🔊' : '🔇'}
            </button>

            <button type="button"
                className={`icon-toggle ${prefs.vibrate ? 'is-on' : ''}`}
                onClick={() => setVibrateOn(!isVibrateOn())}
                title={prefs.vibrate ? t('vibrationOn') : t('vibrationOff')}
                aria-label={prefs.vibrate ? t('vibrationOn') : t('vibrationOff')}>
                {prefs.vibrate ? '📳' : '📴'}
            </button>

            {onToggleRoles && (
                <button type="button"
                    className={`icon-toggle ${showRoles ? 'is-on' : ''}`}
                    onClick={onToggleRoles}
                    title={t('meetTheRoles')}
                    aria-pressed={showRoles}
                    aria-label={t('meetTheRoles')}>
                    📜
                </button>
            )}

            {onProfile && (
                <button className="btn btn--ghost btn--sm" onClick={onProfile}>
                    👤 {t('profile')}
                </button>
            )}
        </div>
    );
}
