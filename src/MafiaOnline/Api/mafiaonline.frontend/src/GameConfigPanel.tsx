import React, { useState, useEffect } from 'react';
import type { GameConfig } from './api';
import { useTranslation } from 'react-i18next';

type GameConfigPanelProps = {
    config: GameConfig;
    onChange: (config: GameConfig) => void;
};

const roleKeys: (keyof GameConfig['roles'])[] = [
    'guardian',
    'detective',
    'dogTamer',
    'poacher',
    'trailer',
    'seer',
    'fool',
];

export default function GameConfigPanel({ config, onChange }: GameConfigPanelProps) {
    const { t } = useTranslation();
    const [local, setLocal] = useState<GameConfig>(config);

    useEffect(() => {
        setLocal(config);
    }, [config]);

    function update<K extends keyof GameConfig>(key: K, value: GameConfig[K]) {
        const next = { ...local, [key]: value };
        setLocal(next);
        onChange(next);
    }

    function updateRole(key: keyof GameConfig['roles'], value: boolean) {
        const nextRoles = { ...local.roles, [key]: value };
        update('roles', nextRoles);
    }

    function updateNested<T extends object>(parentKey: keyof GameConfig, value: T) {
        update(parentKey, { ...local[parentKey], ...value } as GameConfig[keyof GameConfig]);
    }

    return (
        <div style={{ marginTop: 20, color: '#ddd' }}>
            <h3 style={{ color: '#f0c040', marginBottom: 12 }}>{t('gameConfig')}</h3>

            <div style={styles.section}>
                <h4 style={styles.sectionTitle}>{t('generalSettings')}</h4>
                <label style={styles.row}>
                    <span>{t('werewolfCount')}</span>
                    <input
                        type="number"
                        min={1}
                        max={5}
                        value={local.werewolfCount}
                        onChange={e => update('werewolfCount', Math.max(1, parseInt(e.target.value) || 1))}
                        style={styles.numberInput}
                    />
                </label>
                <label style={styles.row}>
                    <span>{t('turnDurationSeconds')}</span>
                    <input
                        type="number"
                        min={5}
                        max={300}
                        value={local.turnDurationSeconds}
                        onChange={e => update('turnDurationSeconds', Math.max(5, parseInt(e.target.value) || 5))}
                        style={styles.numberInput}
                    />
                </label>
            </div>

            <div style={styles.section}>
                <h4 style={styles.sectionTitle}>{t('daySettings')}</h4>
                <label style={styles.row}>
                    <span>{t('dayDurationSeconds')}</span>
                    <input
                        type="number"
                        min={10}
                        max={600}
                        value={local.day.durationSeconds}
                        onChange={e => updateNested('day', { durationSeconds: Math.max(10, parseInt(e.target.value) || 10) })}
                        style={styles.numberInput}
                    />
                </label>
                <label style={styles.row}>
                    <span>{t('votesRequiredFraction')}</span>
                    <input
                        type="number"
                        min={0.1}
                        max={1}
                        step={0.1}
                        value={local.day.votesRequiredFraction}
                        onChange={e => updateNested('day', { votesRequiredFraction: Math.max(0.1, Math.min(1, parseFloat(e.target.value) || 0.5)) })}
                        style={styles.numberInput}
                    />
                </label>
                <label style={styles.row}>
                    <span>{t('drawResolution')}</span>
                    <select
                        value={local.day.drawResolution}
                        onChange={e => updateNested('day', { drawResolution: e.target.value as GameConfig['day']['drawResolution'] })}
                        style={styles.select}
                    >
                        <option value="KickNone">{t('drawKickNone')}</option>
                        <option value="KickRandom">{t('drawKickRandom')}</option>
                        <option value="KickAll">{t('drawKickAll')}</option>
                    </select>
                </label>
                <label style={styles.row}>
                    <input
                        type="checkbox"
                        checked={local.day.showVoteTargets}
                        onChange={e => updateNested('day', { showVoteTargets: e.target.checked })}
                    />
                    <span>{t('showVoteTargets')}</span>
                </label>
            </div>

            <div style={styles.section}>
                <h4 style={styles.sectionTitle}>{t('roleCards')}</h4>
                <div style={styles.roleGrid}>
                    {roleKeys.map(key => (
                        <div key={key} style={styles.roleCard}>
                            <label style={styles.roleHeader}>
                                <input
                                    type="checkbox"
                                    checked={local.roles[key]}
                                    onChange={e => updateRole(key, e.target.checked)}
                                />
                                <span style={styles.roleName}>{t(key)}</span>
                            </label>
                            {key === 'guardian' && local.roles.guardian && (
                                <div style={styles.subConfig}>
                                    <label style={styles.subRow}>
                                        <input
                                            type="checkbox"
                                            checked={local.guardian.canProtectSelf}
                                            onChange={e => updateNested('guardian', { canProtectSelf: e.target.checked })}
                                        />
                                        <span>{t('canProtectSelf')}</span>
                                    </label>
                                    <label style={styles.subRow}>
                                        <input
                                            type="checkbox"
                                            checked={local.guardian.canProtectSamePlayerConsecutively}
                                            onChange={e => updateNested('guardian', { canProtectSamePlayerConsecutively: e.target.checked })}
                                        />
                                        <span>{t('canProtectSamePlayerConsecutively')}</span>
                                    </label>
                                </div>
                            )}
                            {key === 'detective' && local.roles.detective && (
                                <div style={styles.subConfig}>
                                    <label style={styles.subRow}>
                                        <input
                                            type="checkbox"
                                            checked={local.detective.canRevealWerewolves}
                                            onChange={e => updateNested('detective', { canRevealWerewolves: e.target.checked })}
                                        />
                                        <span>{t('canRevealWerewolves')}</span>
                                    </label>
                                </div>
                            )}
                            {key === 'dogTamer' && local.roles.dogTamer && (
                                <div style={styles.subConfig}>
                                    <label style={styles.subRow}>
                                        <span>{t('dogTamerRange')}</span>
                                        <input
                                            type="number"
                                            min={1}
                                            max={5}
                                            value={local.dogTamer.range}
                                            onChange={e => updateNested('dogTamer', { range: Math.max(1, parseInt(e.target.value) || 1) })}
                                            style={styles.smallNumber}
                                        />
                                    </label>
                                    <label style={styles.subRow}>
                                        <span>{t('dogTamerNumWerewolves')}</span>
                                        <input
                                            type="number"
                                            min={0}
                                            max={5}
                                            value={local.dogTamer.numWerewolves}
                                            onChange={e => updateNested('dogTamer', { numWerewolves: Math.max(0, parseInt(e.target.value) || 0) })}
                                            style={styles.smallNumber}
                                        />
                                    </label>
                                </div>
                            )}
                            {key === 'poacher' && local.roles.poacher && (
                                <div style={styles.subConfig}>
                                    <label style={styles.subRow}>
                                        <input
                                            type="checkbox"
                                            checked={local.poacher.allowSelfTarget}
                                            onChange={e => updateNested('poacher', { allowSelfTarget: e.target.checked })}
                                        />
                                        <span>{t('allowSelfTarget')}</span>
                                    </label>
                                    <label style={styles.subRow}>
                                        <input
                                            type="checkbox"
                                            checked={local.poacher.mustAlternate}
                                            onChange={e => updateNested('poacher', { mustAlternate: e.target.checked })}
                                        />
                                        <span>{t('mustAlternate')}</span>
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

const styles: Record<string, React.CSSProperties> = {
    section: {
        background: '#1a1a2e',
        border: '1px solid #333',
        borderRadius: 10,
        padding: 14,
        marginBottom: 16,
    },
    sectionTitle: {
        color: '#aaa',
        margin: '0 0 12px',
        fontSize: 14,
        textTransform: 'uppercase',
        letterSpacing: 1,
    },
    row: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        gap: 12,
        marginBottom: 10,
        fontSize: 14,
    },
    numberInput: {
        width: 70,
        background: '#1e1e2e',
        border: '1px solid #444',
        borderRadius: 6,
        color: '#ddd',
        padding: '4px 8px',
    },
    select: {
        background: '#1e1e2e',
        border: '1px solid #444',
        borderRadius: 6,
        color: '#ddd',
        padding: '4px 8px',
    },
    roleGrid: {
        display: 'grid',
        gridTemplateColumns: 'repeat(auto-fill, minmax(180px, 1fr))',
        gap: 12,
    },
    roleCard: {
        background: '#161626',
        border: '1px solid #2a2a3e',
        borderRadius: 8,
        padding: 12,
    },
    roleHeader: {
        display: 'flex',
        alignItems: 'center',
        gap: 10,
        cursor: 'pointer',
        fontWeight: 600,
    },
    roleName: {
        color: '#f0c040',
    },
    subConfig: {
        marginTop: 10,
        paddingTop: 10,
        borderTop: '1px solid #2a2a3e',
        display: 'flex',
        flexDirection: 'column',
        gap: 8,
    },
    subRow: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        gap: 8,
        fontSize: 13,
        color: '#bbb',
    },
    smallNumber: {
        width: 50,
        background: '#1e1e2e',
        border: '1px solid #444',
        borderRadius: 6,
        color: '#ddd',
        padding: '3px 6px',
    },
};
