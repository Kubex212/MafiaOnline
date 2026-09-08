import React from 'react';
import type { GameOverPayload } from './api';
import { useTranslation } from 'react-i18next';

interface Props {
    payload: GameOverPayload;
    myRoleLabel: string;
    myRole: string;
    myTeam: number;
    onCreateRematch?: () => Promise<void>;
    onJoinRematch?: () => Promise<void>;
}

export default function GameOver({ payload, myRoleLabel, myRole, myTeam, onCreateRematch, onJoinRematch }: Props) {
    const { t } = useTranslation();

    let won = false;
    if (payload.winner === 'Fool') {
        won = myRole === 'Fool';
    } else {
        won = (payload.winner === 'Villagers' && myTeam === 0)
            || (payload.winner === 'Werewolves' && myTeam === 1);
    }

    const icon = payload.winner === 'Villagers' ? '🌾'
        : payload.winner === 'Werewolves' ? '🐺'
        : '🃏';

    const verdictClass = won ? 'gameover__verdict--won' : 'gameover__verdict--lost';
    const color = payload.winner === 'Villagers' ? 'var(--town-2)'
        : payload.winner === 'Werewolves' ? 'var(--accent-2)'
        : 'var(--accent-2)';

    function goHome() {
        sessionStorage.removeItem('lobbyCode');
        window.location.href = '/';
    }

    return (
        <div className="gameover">
            <div className="gameover__icon" style={{ color }}>{icon}</div>
            <div className="gameover__title" style={{ color, textShadow: `0 0 40px ${color}` }}>
                {t(payload.winner)} {payload.winner === 'Fool' ? t('neutralWins') : t('teamWins')}
            </div>
            <div className={`gameover__verdict ${verdictClass}`}>
                {won ? '🎉 ' + t('youWon') : t('youLost')}
            </div>
            <div style={{ color: 'var(--text-muted)', fontStyle: 'italic' }}>
                {t('youPlayedAs')} <b style={{ color: 'var(--accent-2)' }}>{myRoleLabel}</b>
            </div>
            {payload.drunkName && (
                <div style={{ marginTop: 8, color: 'var(--text-dim)' }}>
                    {t('drunkRevealed', { name: payload.drunkName })}
                </div>
            )}
            <div style={{ display: 'flex', gap: 8, marginTop: 16 }}>
                <button className="btn" onClick={goHome}>
                    {t('backToMenu')}
                </button>
                <button className="btn btn--primary" onClick={onJoinRematch}>
                    {t('playAgain')}
                </button>
            </div>
        </div>
    );
}
