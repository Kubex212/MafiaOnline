import React from 'react';
import type { GameConfig } from './api';
import { useTranslation } from 'react-i18next';

export default function GameConfigView({ config }: { config: GameConfig }) {
    const { t } = useTranslation();

    return (
        <div style={{ marginTop: 20, color: '#ddd' }}>
            <h3 style={{ color: '#f0c040', marginBottom: 12 }}>{t('gameConfig')}</h3>

            <div style={styles.section}>
                <h4 style={styles.sectionTitle}>{t('generalSettings')}</h4>
                <div style={styles.row}><span>{t('werewolfCount')}</span><strong>{config.werewolfCount}</strong></div>
                <div style={styles.row}><span>{t('turnDurationSeconds')}</span><strong>{config.turnDurationSeconds}</strong></div>
            </div>

            <div style={styles.section}>
                <h4 style={styles.sectionTitle}>{t('daySettings')}</h4>
                <div style={styles.row}><span>{t('dayDurationSeconds')}</span><strong>{config.day.durationSeconds}</strong></div>
                <div style={styles.row}><span>{t('votesRequiredFraction')}</span><strong>{config.day.votesRequiredFraction}</strong></div>
                <div style={styles.row}><span>{t('drawResolution')}</span><strong>{t(config.day.drawResolution)}</strong></div>
                <div style={styles.row}>
                    <span>{t('showVoteTargets')}</span>
                    <input type="checkbox" disabled checked={config.day.showVoteTargets} />
                </div>
            </div>

            <div style={styles.section}>
                <h4 style={styles.sectionTitle}>{t('roleCards')}</h4>
                <div style={styles.roleGrid}>
                    {Object.entries(config.roles).map(([key, enabled]) => (
                        <div key={key} style={styles.roleCard}>
                            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                                <div style={{ fontWeight: 700, color: '#f0c040' }}>{t(key)}</div>
                                <input type="checkbox" disabled checked={!!enabled} />
                            </div>
                            {key === 'guardian' && enabled && (
                                <div style={styles.subConfig}>
                                    <div style={styles.subRow}>
                                        <span>{t('canProtectSelf')}</span>
                                        <input type="checkbox" disabled checked={config.guardian.canProtectSelf} />
                                    </div>
                                    <div style={styles.subRow}>
                                        <span>{t('canProtectSamePlayerConsecutively')}</span>
                                        <input type="checkbox" disabled checked={config.guardian.canProtectSamePlayerConsecutively} />
                                    </div>
                                </div>
                            )}
                            {key === 'detective' && enabled && (
                                <div style={styles.subConfig}>
                                    <div style={styles.subRow}>
                                        <span>{t('canRevealWerewolves')}</span>
                                        <input type="checkbox" disabled checked={config.detective.canRevealWerewolves} />
                                    </div>
                                </div>
                            )}
                            {key === 'dogTamer' && enabled && (
                                <div style={styles.subConfig}>
                                    <div style={styles.subRow}><span>{t('dogTamerRange')}</span><strong>{config.dogTamer.range}</strong></div>
                                    <div style={styles.subRow}><span>{t('dogTamerNumWerewolves')}</span><strong>{config.dogTamer.numWerewolves}</strong></div>
                                </div>
                            )}
                            {key === 'poacher' && enabled && (
                                <div style={styles.subConfig}>
                                    <div style={styles.subRow}>
                                        <span>{t('allowSelfTarget')}</span>
                                        <input type="checkbox" disabled checked={config.poacher.allowSelfTarget} />
                                    </div>
                                    <div style={styles.subRow}>
                                        <span>{t('mustAlternate')}</span>
                                        <input type="checkbox" disabled checked={config.poacher.mustAlternate} />
                                    </div>
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
};
