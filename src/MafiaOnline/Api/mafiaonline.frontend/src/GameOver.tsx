import React from 'react';
import type { GameOverPayload } from './api';
import { useTranslation } from 'react-i18next';

interface Props {
    payload: GameOverPayload;
    myRoleLabel: string;
    myRole: string;
}

export default function GameOver({ payload, myRoleLabel, myRole }: Props) {
    const { t } = useTranslation();

    let won = false;
    if (payload.winner === 'Fool') {
        won = myRole === 'Fool';
    } else {
        won = (payload.winner === 'Villagers' && myRole !== 'Werewolf')
            || (payload.winner === 'Werewolves' && myRole === 'Werewolf');
    }

    const icon = payload.winner === 'Villagers' ? '🌾'
        : payload.winner === 'Werewolves' ? '🐺'
        : '🃏';

    const verdictClass = won ? 'gameover__verdict--won' : 'gameover__verdict--lost';
    const color = payload.winner === 'Villagers' ? 'var(--town-2)'
        : payload.winner === 'Werewolves' ? 'var(--danger-2)'
        : 'var(--accent-2)';

    return (
        <div className="gameover">
            <div className="gameover__icon" style={{ color }}>{icon}</div>
            <div className="gameover__title" style={{ color, textShadow: `0 0 40px ${color}` }}>
                {t(payload.winner)} {t('teamWins')}
            </div>
            <div className={`gameover__verdict ${verdictClass}`}>
                {won ? '🎉 ' + t('youWon') : t('youLost')}
            </div>
            <div style={{ color: 'var(--text-muted)', fontStyle: 'italic' }}>
                {t('youPlayedAs')} <b style={{ color: 'var(--accent-2)' }}>{myRoleLabel}</b>
            </div>
            <button className="btn btn--primary" onClick={() => window.location.reload()} style={{ marginTop: 16 }}>
                {t('playAgain')}
            </button>
        </div>
    );
}
