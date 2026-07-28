import React from 'react';
import type { GameConfig } from './api';
import { useTranslation } from 'react-i18next';

const roleKeys: (keyof GameConfig['roles'])[] = [
    'guardian', 'detective', 'dogTamer', 'poacher', 'trailer', 'seer', 'fool',
];

function Flag({ on, t }: { on: boolean; t: (k: string) => string }) {
    return (
        <span className={`config__flag ${on ? 'config__flag--on' : 'config__flag--off'}`}>
            {on ? `✓ ${t('on')}` : `✕ ${t('off')}`}
        </span>
    );
}

/** Read-only mirror of the host's game configuration (live via ConfigUpdated). */
export default function GameConfigView({ config }: { config: GameConfig }) {
    const { t } = useTranslation();

    return (
        <div className="config config--readonly">
            <h3 style={{ color: 'var(--accent)', margin: '0 0 12px',
                letterSpacing: '0.12em', textTransform: 'uppercase', fontSize: '0.9rem' }}>
                {t('gameConfig')}
            </h3>

            <div className="config__section">
                <h4 className="config__section-title">{t('generalSettings')}</h4>
                <div className="config__row">
                    <span>{t('werewolfCount')}</span>
                    <span className="config__value">{config.werewolfCount}</span>
                </div>
                <div className="config__row">
                    <span>{t('turnDurationSeconds')}</span>
                    <span className="config__value">{config.turnDurationSeconds}</span>
                </div>
            </div>

            <div className="config__section">
                <h4 className="config__section-title">{t('daySettings')}</h4>
                <div className="config__row">
                    <span>{t('dayDurationSeconds')}</span>
                    <span className="config__value">{config.day.durationSeconds}</span>
                </div>
                <div className="config__row">
                    <span>{t('votesRequiredFraction')}</span>
                    <span className="config__value">{config.day.votesRequiredFraction}</span>
                </div>
                <div className="config__row">
                    <span>{t('drawResolution')}</span>
                    <span className="config__value">{t(config.day.drawResolution)}</span>
                </div>
                {/*<div className="config__row">*/}
                {/*    <span>{t('showVoteTargets')}</span>*/}
                {/*    <Flag on={config.day.showVoteTargets} t={t} />*/}
                {/*</div>*/}
            </div>

            <div className="config__section">
                <h4 className="config__section-title">{t('roleCards')}</h4>
                <div className="config__grid">
                    {roleKeys.map(key => {
                        const enabled = !!config.roles[key];
                        return (
                            <div key={key} className={`role-card ${enabled ? '' : 'role-card--off'}`}>
                                <div className="role-card__head role-card__head--static">
                                    <span>{t(key)}</span>
                                    <Flag on={enabled} t={t} />
                                </div>

                                {key === 'guardian' && enabled && (
                                    <div className="role-card__sub">
                                        <label><span>{t('canProtectSelf')}</span>
                                            <Flag on={config.guardian.canProtectSelf} t={t} /></label>
                                        <label><span>{t('canProtectSamePlayerConsecutively')}</span>
                                            <Flag on={config.guardian.canProtectSamePlayerConsecutively} t={t} /></label>
                                    </div>
                                )}
                                {key === 'detective' && enabled && (
                                    <div className="role-card__sub">
                                        <label><span>{t('canRevealWerewolves')}</span>
                                            <Flag on={config.detective.canRevealWerewolves} t={t} /></label>
                                    </div>
                                )}
                                {key === 'dogTamer' && enabled && (
                                    <div className="role-card__sub">
                                        <label><span>{t('dogTamerRange')}</span>
                                            <span className="config__value">{config.dogTamer.range}</span></label>
                                        <label><span>{t('dogTamerNumWerewolves')}</span>
                                            <span className="config__value">{config.dogTamer.numWerewolves}</span></label>
                                    </div>
                                )}
                                {key === 'poacher' && enabled && (
                                    <div className="role-card__sub">
                                        <label><span>{t('allowSelfTarget')}</span>
                                            <Flag on={config.poacher.allowSelfTarget} t={t} /></label>
                                        <label><span>{t('mustAlternate')}</span>
                                            <Flag on={config.poacher.mustAlternate} t={t} /></label>
                                    </div>
                                )}
                            </div>
                        );
                    })}
                </div>
            </div>
        </div>
    );
}
