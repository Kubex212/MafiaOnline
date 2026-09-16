import React, { useEffect, useState } from 'react';
import type { GameConfig, WerewolfConfig, RoleType } from './api';
import { nonStandardRoleTypes } from './api';
import { DEFAULT_WEREWOLF_CONFIG } from './api';
import { useTranslation } from 'react-i18next';
import RoleHint from './RoleHint';

type GameConfigPanelProps = {
    config: GameConfig;
    onChange: (config: GameConfig) => void;
    section?: 'settings' | 'roles';
};

export default function GameConfigPanel({ config, onChange, section }: GameConfigPanelProps) {
    const { t } = useTranslation();
    const [local, setLocal] = useState<GameConfig>(config);

    useEffect(() => { setLocal(config); }, [config]);

    function update<K extends keyof GameConfig>(key: K, value: GameConfig[K]) {
        const next = { ...local, [key]: value };
        setLocal(next);
        onChange(next);
    }

    function updateRole(key: RoleType, value: boolean) {
        update('enabledRoles', value
            ? [...(local.enabledRoles ?? []), key]
            : (local.enabledRoles ?? []).filter(x => x !== key)
        );
        console.log(local.enabledRoles);
    }

    function updateNested<T extends object>(parentKey: keyof GameConfig, value: T) {
        update(parentKey, { ...(local[parentKey] as any ?? {}), ...value } as GameConfig[keyof GameConfig]);
    }

    const werewolf: WerewolfConfig = { ...DEFAULT_WEREWOLF_CONFIG, ...(local.werewolf ?? {}) };
    function updateWerewolf(value: Partial<WerewolfConfig>) {
        update('werewolf', { ...werewolf, ...value });
    }

    const showSettings = !section || section === 'settings';
    const showRoles = !section || section === 'roles';

    return (
        <div className="config">
            {showSettings && (
                <>
                    <div className="config__section">
                        <h4 className="config__section-title">{t('generalSettings')}</h4>
                        <label className="config__row">
                            <span>{t('werewolfCount')}</span>
                            <input type="number" min={1} max={5}
                                className="config__num"
                                value={local.werewolfCount ?? 1}
                                onChange={e => update('werewolfCount', Math.max(1, parseInt(e.target.value) || 1))} />
                        </label>
                        <label className="config__row">
                            <span>{t('turnDurationSeconds')}</span>
                            <input type="number" min={5} max={300}
                                className="config__num"
                                value={local.turnDurationSeconds ?? 30}
                                onChange={e => update('turnDurationSeconds', Math.max(5, parseInt(e.target.value) || 5))} />
                        </label>
                    </div>

                    <div className="config__section">
                        <h4 className="config__section-title">{t('daySettings')}</h4>
                        <label className="config__row">
                            <span>{t('dayDurationSeconds')}</span>
                            <input type="number" min={10} max={600}
                                className="config__num"
                                value={local.day?.durationSeconds ?? 60}
                                onChange={e => updateNested('day', { durationSeconds: Math.max(10, parseInt(e.target.value) || 10) })} />
                        </label>
                        <label className="config__row">
                            <span>{t('votesRequiredFraction')}</span>
                            <input type="number" min={0.1} max={1} step={0.1}
                                className="config__num"
                                value={local.day?.votesRequiredFraction ?? 0.5}
                                onChange={e => updateNested('day', {
                                    votesRequiredFraction: Math.max(0.1, Math.min(1, parseFloat(e.target.value) || 0.5))
                                })} />
                        </label>
                        <label className="config__row">
                            <span>{t('drawResolution')}</span>
                            <select className="config__select"
                                value={local.day?.drawResolution ?? 'KickNone'}
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
                                value={local.day?.showVoteTargets ?? 'Always'}
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
                                checked={werewolf.showAlibi}
                                onChange={e => updateWerewolf({ showAlibi: e.target.checked })} />
                        </label>
                    </div>

                    <div className="config__section">
                        <h4 className="config__section-title">{t('drunkSettings')}</h4>
                        <label className="config__row">
                            <span>{t('drunkOccuranceChance')}</span>
                            <input type="number" min={0} max={100} className="config__num"
                                value={local.drunk?.drunkOccuranceChance ?? 100}
                                onChange={e => updateNested('drunk', { drunkOccuranceChance: Math.max(0, Math.min(100, parseInt(e.target.value) || 0)) })} />
                        </label>
                        <label className="config__row">
                            <span>{t('drunkEffectChance')}</span>
                            <input type="number" min={0} max={100} className="config__num"
                                value={local.drunk?.drunkEffectChance ?? 100}
                                onChange={e => updateNested('drunk', { drunkEffectChance: Math.max(0, Math.min(100, parseInt(e.target.value) || 0)) })} />
                        </label>
                        <label className="config__row">
                            <span>{t('announceDrunkOccurance')}</span>
                            <input type="checkbox" checked={local.drunk?.announceDrunkOccurance ?? false}
                                onChange={e => updateNested('drunk', { announceDrunkOccurance: e.target.checked })} />
                        </label>
                    </div>
                </>
            )}

            {showRoles && (
                <div className="config__grid">
                    {nonStandardRoleTypes.map(key => (
                        <div key={key} className="role-card">
                            <div className="role-card__headrow">
                                <label className="role-card__head">
                                    <input type="checkbox"
                                        checked={!!(local.enabledRoles?.includes(key))}
                                        onChange={e => updateRole(key, e.target.checked)} />
                                    <span>{t(key)}</span>
                                </label>
                                <RoleHint role={key} config={local} />
                            </div>
                            {key === 'Guardian' && local.enabledRoles?.includes('Guardian') && (
                                <div className="role-card__sub">
                                    <label>
                                        <span>{t('canProtectSelf')}</span>
                                        <input type="checkbox"
                                            checked={local.guardian?.canProtectSelf ?? false}
                                            onChange={e => updateNested('guardian', { canProtectSelf: e.target.checked })} />
                                    </label>
                                    <label>
                                        <span>{t('mustAlternate')}</span>
                                        <input type="checkbox"
                                            checked={local.guardian?.mustAlternate ?? false}
                                            onChange={e => updateNested('guardian', { mustAlternate: e.target.checked })} />
                                    </label>
                                    <label>
                                        <span>{t('announceProtection')}</span>
                                        <input type="checkbox"
                                            checked={local.guardian?.announceProtection ?? false}
                                            onChange={e => updateNested('guardian', { announceProtection: e.target.checked })} />
                                    </label>
                                </div>
                            )}
                            {key === 'DogTamer' && local.enabledRoles?.includes('DogTamer') && (
                                <div className="role-card__sub">
                                    <label>
                                        <span>{t('dogTamerRange')}</span>
                                        <input type="number" min={1} max={5}
                                            className="config__num" style={{ width: 60 }}
                                            value={local.dogTamer?.range ?? 1}
                                            onChange={e => updateNested('dogTamer', { range: Math.max(1, parseInt(e.target.value) || 1) })} />
                                    </label>
                                    <label>
                                        <span>{t('dogTamerNumWerewolves')}</span>
                                        <input type="number" min={0} max={5}
                                            className="config__num" style={{ width: 60 }}
                                            value={local.dogTamer?.numWerewolves ?? 0}
                                            onChange={e => updateNested('dogTamer', { numWerewolves: Math.max(0, parseInt(e.target.value) || 0) })} />
                                    </label>
                                </div>
                            )}
                            {key === 'Poacher' && local.enabledRoles?.includes('Poacher') && (
                                <div className="role-card__sub">
                                    <label>
                                        <span>{t('allowSelfTarget')}</span>
                                        <input type="checkbox"
                                            checked={local.poacher?.allowSelfTarget ?? false}
                                            onChange={e => updateNested('poacher', { allowSelfTarget: e.target.checked })} />
                                    </label>
                                    <label>
                                        <span>{t('mustAlternate')}</span>
                                        <input type="checkbox"
                                            checked={local.poacher?.mustAlternate ?? false}
                                            onChange={e => updateNested('poacher', { mustAlternate: e.target.checked })} />
                                    </label>
                                </div>
                            )}
                            {key === 'Ghost' && local.enabledRoles?.includes('Ghost') && (
                                <div className="role-card__sub">
                                    <label>
                                        <span>{t('shouldGetAlibi')}</span>
                                        <input type="checkbox"
                                            checked={local.ghost?.shouldGetAlibi ?? false}
                                            onChange={e => updateNested('ghost', { shouldGetAlibi: e.target.checked })} />
                                    </label>
                                </div>
                            )}
                            {key === 'Innkeeper' && local.enabledRoles?.includes('Innkeeper') && (
                                <div className="role-card__sub">
                                    <label>
                                        <span>{t('innkeeperSuspectCount')}</span>
                                        <input type="number" min={1} max={10} className="config__num"
                                            value={local.innkeeper?.supsectCount ?? 2}
                                            onChange={e => updateNested('innkeeper', { supsectCount: Math.max(1, Math.min(10, parseInt(e.target.value) || 3)) })} />
                                    </label>
                                </div>
                            )}
                        </div>
                    ))}
                </div>
            )}
        </div>
    );
}
