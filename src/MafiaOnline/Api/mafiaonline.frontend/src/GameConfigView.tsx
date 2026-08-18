import React from 'react';
import type { GameConfig, WerewolfConfig } from './api';
import { DEFAULT_WEREWOLF_CONFIG } from './api';
import { useTranslation } from 'react-i18next';
import RoleHint from './RoleHint';

const roleKeys: (keyof GameConfig['roles'])[] = [
    'guardian', 'detective', 'dogTamer', 'poacher', 'trailer', 'seer', 'peeper', 'spy', 'ranger', 'fool', 'dracula', 'deceiver', 'ghost', 'charmer'
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
    const werewolf: WerewolfConfig = { ...DEFAULT_WEREWOLF_CONFIG, ...(config.werewolf ?? {}) };
    const votesLabel = (v: string) =>
        v === 'Never' ? t('votesNever')
            : v === 'AfterVotingEnds' ? t('votesAfterVotingEnds')
            : t('votesAlways');

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
                    <span className="config__value">{config.werewolfCount ?? 0}</span>
                </div>
                <div className="config__row">
                    <span>{t('turnDurationSeconds')}</span>
                    <span className="config__value">{config.turnDurationSeconds ?? 0}</span>
                </div>
            </div>

            <div className="config__section">
                <h4 className="config__section-title">{t('daySettings')}</h4>
                <div className="config__row">
                    <span>{t('dayDurationSeconds')}</span>
                    <span className="config__value">{config.day?.durationSeconds ?? 0}</span>
                </div>
                <div className="config__row">
                    <span>{t('votesRequiredFraction')}</span>
                    <span className="config__value">{config.day?.votesRequiredFraction ?? 0}</span>
                </div>
                <div className="config__row">
                    <span>{t('drawResolution')}</span>
                    <span className="config__value">{t('draw' + (config.day?.drawResolution ?? ''))}</span>
                </div>
                <div className="config__row">
                    <span>{t('showVoteTargets')}</span>
                    <span className="config__value">{votesLabel(config.day?.showVoteTargets ?? 'Always')}</span>
                </div>
            </div>

            <div className="config__section">
                <h4 className="config__section-title">{t('werewolfSettings')}</h4>
                <div className="config__row">
                    <span>{t('werewolfDrawResolution')}</span>
                    <span className="config__value">{t('draw' + (werewolf.drawResolution ?? DEFAULT_WEREWOLF_CONFIG.drawResolution))}</span>
                </div>
                <div className="config__row">
                    <span>{t('werewolfShowVoteTargets')}</span>
                    <span className="config__value">{votesLabel(werewolf.showVoteTargets)}</span>
                </div>
                <div className="config__row">
                    <span>{t('werewolfAlibi')}</span>
                    <Flag on={werewolf.alibi ?? DEFAULT_WEREWOLF_CONFIG.alibi} t={t} />
                </div>
            </div>

            <div className="config__section">
                <h4 className="config__section-title">{t('roleCards')}</h4>
                <div className="config__grid">
                    {roleKeys.map(key => {
                        const enabled = !!config.roles?.[key];
                        return (
                            <div key={key} className={`role-card ${enabled ? '' : 'role-card--off'}`}>
                                <div className="role-card__head role-card__head--static">
                                    <span>{t(key)}</span>
                                    <RoleHint role={key} config={config} />
                                    <span className="role-card__flag"><Flag on={enabled} t={t} /></span>
                                </div>

                                {key === 'guardian' && enabled && (
                                    <div className="role-card__sub">
                                        <label><span>{t('canProtectSelf')}</span>
                                            <Flag on={config.guardian?.canProtectSelf ?? false} t={t} /></label>
                                        <label><span>{t('mustAlternate')}</span>
                                            <Flag on={config.guardian?.mustAlternate ?? false} t={t} /></label>
                                        <label><span>{t('announceProtection')}</span>
                                            <Flag on={config.guardian?.announceProtection ?? false} t={t} /></label>
                                    </div>
                                )}
                                {key === 'detective' && enabled && (
                                    <div className="role-card__sub">
                                        <label><span>{t('canRevealWerewolves')}</span>
                                            <Flag on={config.detective?.canRevealWerewolves ?? false} t={t} /></label>
                                    </div>
                                )}
                                {key === 'dogTamer' && enabled && (
                                    <div className="role-card__sub">
                                        <label><span>{t('dogTamerRange')}</span>
                                            <span className="config__value">{config.dogTamer?.range ?? 0}</span></label>
                                        <label><span>{t('dogTamerNumWerewolves')}</span>
                                            <span className="config__value">{config.dogTamer?.numWerewolves ?? 0}</span></label>
                                    </div>
                                )}
                                {key === 'poacher' && enabled && (
                                    <div className="role-card__sub">
                                        <label><span>{t('allowSelfTarget')}</span>
                                            <Flag on={config.poacher?.allowSelfTarget ?? false} t={t} /></label>
                                        <label><span>{t('mustAlternate')}</span>
                                            <Flag on={config.poacher?.mustAlternate ?? false} t={t} /></label>
                                    </div>
                                )}
                                {key === 'ghost' && enabled && (
                                    <div className="role-card__sub">
                                        <label><span>{t('shouldGetAlibi')}</span>
                                            <Flag on={config.ghost?.shouldGetAlibi ?? false} t={t} /></label>
                                    </div>
                                )}
                            </div>
                        );
                    })}
                </div>
            </div>

            <div className="config__section">
                <h4 className="config__section-title">{t('drunkSettings')}</h4>
                <div className="config__row">
                    <span>{t('drunkOccuranceChance')}</span>
                    <span className="config__value">{config.drunk?.drunkOccuranceChance ?? 100}%</span>
                </div>
                <div className="config__row">
                    <span>{t('drunkEffectChance')}</span>
                    <span className="config__value">{config.drunk?.drunkEffectChance ?? 100}%</span>
                </div>
                <div className="config__row">
                    <span>{t('announceDrunkOccurance')}</span>
                    <span className="config__value">{config.drunk?.announceDrunkOccurance ? t('on') : t('off')}</span>
                </div>
            </div>
        </div>
    );
}
