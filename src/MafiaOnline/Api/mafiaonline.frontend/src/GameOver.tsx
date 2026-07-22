import React from 'react';
import type { GameOverPayload } from './api';
import { useTranslation } from 'react-i18next';

interface Props {
    payload: GameOverPayload;
    myRoleLabel: string; // localized label
    myRole: string; // raw role name for logic
}

export default function GameOver({ payload, myRoleLabel, myRole }: Props) {
    const won = (payload.winner === 'Villagers' && myRole !== 'Werewolf')
        || (payload.winner === 'Werewolves' && myRole === 'Werewolf');
    const color = payload.winner === 'Villagers' ? '#5cb85c' : '#d9534f';
    const icon = payload.winner === 'Villagers' ? '\uD83C\uDF3E' : '\uD83D\uDC3A';
    const { t } = useTranslation();

    return (
        <div style={{
            display: 'flex', flexDirection: 'column', alignItems: 'center',
            justifyContent: 'center', minHeight: '60vh', gap: 20, textAlign: 'center',
        }}>
            <div style={{ fontSize: 64 }}>{icon}</div>
            <div style={{ fontSize: 36, fontWeight: 900, color, textShadow: `0 0 40px ${color}88` }}>
                {payload.winner} win!
            </div>
            <div style={{ fontSize: 18, color: won ? '#5cb85c' : '#d9534f', fontWeight: 700 }}>
                {won ? '\uD83C\uDF89 ' + t('youWon') : '\uD83D\uDE14 ' + t('youLost')}
            </div>
            <div style={{ color: '#555', fontSize: 13 }}>
                You played as: <b style={{ color: '#aaa' }}>{myRoleLabel}</b>
            </div>
        </div>
    );
}
