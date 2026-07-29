import React, { useEffect, useState } from 'react';
import type { GameConfig, WerewolfConfig } from './api';
import { DEFAULT_WEREWOLF_CONFIG } from './api';
import { useTranslation } from 'react-i18next';

type GameConfigPanelProps = {
    config: GameConfig;
    onChange: (config: GameConfig) => void;
};

const roleKeys: (keyof GameConfig['roles'])[] = [
    'guardian', 'detective', 'dogTamer', 'poacher', 'trailer', 'seer', 'fool', 'dracula'
];

export default function GameConfigPanel({ config, onChange }: GameConfigPanelProps) {
    const { t } = useTranslation();
    const [local, setLocal] = useState<GameConfig>(config);

    useEffect(() => { setLocal(config); }, [config]);

    function update<K extends keyof GameConfig>(key: K, value: GameConfig[K]) {
        const next = { ...local, [key]: value };
        setLocal(next);
        onChange(next);
    }

    function updateRole(key: keyof GameConfig['roles'], value: boolean) {
        update('roles', { ...local.roles, [key]: value });
    }

    function updateNested<T extends object>(parentKey: keyof GameConfig, value: T) {
        update(parentKey, { ...local[parentKey], ...value } as GameConfig[keyof GameConfig]);
    }

    const werewolf: WerewolfConfig = { ...DEFAULT_WEREWOLF_CONFIG, ...(local.werewolf ?? {}) };
    function updateWerewolf(value: Partial<WerewolfConfig>) {
        update('werewolf', { ...werewolf, ...value });
    }

    return (
        <div className="config">
            <h3 style={{ color: 'var(--accent)', margin: '0 0 12px',
                letterSpacing: '0.12em', textTransform: 'uppercase', fontSize: '0.9rem' }}>
                {t('gameConfig')}
            </h3>

            <div className="config__section">
                <h4 className="config__section-title">{t('generalSettings')}</h4>
                <label className="config__row">
                    <span>{t('werewolfCount')}</span>
                    <input type="number" min={1} max={5}
                        className="config__num"
                        value={local.werewolfCount}
                        onChange={e => update('werewolfCount', Math.max(1, parseInt(e.target.value) || 1))} />
                </label>
                <label className="config__row">
                    <span>{t('turnDurationSeconds')}</span>
                    <input type="number" min={5} max={300}
                        className="config__num"
                        value={local.turnDurationSeconds}
                        onChange={e => update('turnDurationSeconds', Math.max(5, parseInt(e.target.value) || 5))} />
                </label>
            </div>

            <div className="config__section">
                <h4 className="config__section-title">{t('daySettings')}</h4>
                <label className="config__row">
                    <span>{t('dayDurationSeconds')}</span>
                    <input type="number" min={10} max={600}
                        className="config__num"
                        value={local.day.durationSeconds}
                        onChange={e => updateNested('day', { durationSeconds: Math.max(10, parseInt(e.target.value) || 10) })} />
                </label>
                <label className="config__row">
                    <span>{t('votesRequiredFraction')}</span>
                    <input type="number" min={0.1} max={1} step={0.1}
                        className="config__num"
                        value={local.day.votesRequiredFraction}
                        onChange={e => updateNested('day', {
                            votesRequiredFraction: Math.max(0.1, Math.min(1, parseFloat(e.target.value) || 0.5))
                        })} />
                </label>
                <label className="config__row">
                    <span>{t('drawResolution')}</span>
                    <select className="config__select"
                        value={local.day.drawResolution}
                        onChange={e => updateNested('day', {
                            drawResolution: e.target.value as GameConfig['day']['drawResolution']
                        })}>
                        <option value="KickNone">{t('drawKickNone')}</option>
                        <option value="KickRandom">{t('drawKickRandom')}</option>
                        <option value="KickAll">{t('drawKickAll')}</option>
                    </select>
                </label>
                <label className="config__row">
                    <span>{t('showVoteTargets')}</span>
                    <select className="config__select config__select--wide"
                        value={local.day.showVoteTargets ?? 'Always'}
                        onChange={e => updateNested('day', {
                            showVoteTargets: e.target.value as GameConfig['day']['showVoteTargets']
                        })}>
                        <option value="Never">{t('votesNever')}</option>
                        <option value="AfterVotingEnds">{t('votesAfterVotingEnds')}</option>
                        <option value="Always">{t('votesAlways')}</option>
                    </select>
                </label>
            </div>

            <div className="config__section">
                <h4 className="config__section-title">{t('werewolfSettings')}</h4>
                <label className="config__row">
                    <span>{t('werewolfDrawResolution')}</span>
                    <select className="config__select"
                        value={werewolf.drawResolution}
                        onChange={e => updateWerewolf({
                            drawResolution: e.target.value as WerewolfConfig['drawResolution']
                        })}>
                        <option value="KickNone">{t('drawKickNone')}</option>
                        <option value="KickRandom">{t('drawKickRandom')}</option>
                        <option value="KickAll">{t('drawKickAll')}</option>
                    </select>
                </label>
                <label className="config__row">
                    <span>{t('werewolfShowVoteTargets')}</span>
                    <select className="config__select config__select--wide"
                        value={werewolf.showVoteTargets}
                        onChange={e => updateWerewolf({
                            showVoteTargets: e.target.value as WerewolfConfig['showVoteTargets']
                        })}>
                        <option value="Never">{t('votesNever')}</option>
                        <option value="AfterVotingEnds">{t('votesAfterVotingEnds')}</option>
                        <option value="Always">{t('votesAlways')}</option>
                    </select>
                </label>
                <label className="config__row">
                    <span>{t('werewolfAlibi')}</span>
                    <input type="checkbox"
                        checked={werewolf.alibi}
                        onChange={e => updateWerewolf({ alibi: e.target.checked })} />
                </label>
            </div>

            <div className="config__section">
                <h4 className="config__section-title">{t('roleCards')}</h4>
                <div className="config__grid">
                    {roleKeys.map(key => (
                        <div key={key} className="role-card">
                            <label className="role-card__head">
                                <input type="checkbox"
                                    checked={local.roles[key]}
                                    onChange={e => updateRole(key, e.target.checked)} />
                                <span>{t(key)}</span>
                            </label>
                            {key === 'guardian' && local.roles.guardian && (
                                <div className="role-card__sub">
                                    <label>
                                        <span>{t('canProtectSelf')}</span>
                                        <input type="checkbox"
                                            checked={local.guardian.canProtectSelf}
                                            onChange={e => updateNested('guardian', { canProtectSelf: e.target.checked })} />
                                    </label>
                                    <label>
                                        <span>{t('canProtectSamePlayerConsecutively')}</span>
                                        <input type="checkbox"
                                            checked={local.guardian.canProtectSamePlayerConsecutively}
                                            onChange={e => updateNested('guardian', { canProtectSamePlayerConsecutively: e.target.checked })} />
                                    </label>
                                </div>
                            )}
                            {key === 'detective' && local.roles.detective && (
                                <div className="role-card__sub">
                                    <label>
                                        <span>{t('canRevealWerewolves')}</span>
                                        <input type="checkbox"
                                            checked={local.detective.canRevealWerewolves}
                                            onChange={e => updateNested('detective', { canRevealWerewolves: e.target.checked })} />
                                    </label>
                                </div>
                            )}
                            {key === 'dogTamer' && local.roles.dogTamer && (
                                <div className="role-card__sub">
                                    <label>
                                        <span>{t('dogTamerRange')}</span>
                                        <input type="number" min={1} max={5}
                                            className="config__num" style={{ width: 60 }}
                                            value={local.dogTamer.range}
                                            onChange={e => updateNested('dogTamer', { range: Math.max(1, parseInt(e.target.value) || 1) })} />
                                    </label>
                                    <label>
                                        <span>{t('dogTamerNumWerewolves')}</span>
                                        <input type="number" min={0} max={5}
                                            className="config__num" style={{ width: 60 }}
                                            value={local.dogTamer.numWerewolves}
                                            onChange={e => updateNested('dogTamer', { numWerewolves: Math.max(0, parseInt(e.target.value) || 0) })} />
                                    </label>
                                </div>
                            )}
                            {key === 'poacher' && local.roles.poacher && (
                                <div className="role-card__sub">
                                    <label>
                                        <span>{t('allowSelfTarget')}</span>
                                        <input type="checkbox"
                                            checked={local.poacher.allowSelfTarget}
                                            onChange={e => updateNested('poacher', { allowSelfTarget: e.target.checked })} />
                                    </label>
                                    <label>
                                        <span>{t('mustAlternate')}</span>
                                        <input type="checkbox"
                                            checked={local.poacher.mustAlternate}
                                            onChange={e => updateNested('poacher', { mustAlternate: e.target.checked })} />
                                    </label>
                                </div>
                            )}
                        </div>
                    ))}
                </div>
            </div>
        </div>
    );
}
