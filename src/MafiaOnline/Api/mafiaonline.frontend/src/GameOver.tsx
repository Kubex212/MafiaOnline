import React from 'react';
import type { GameOverPayload } from './api';

interface Props {
  payload: GameOverPayload;
  myRole: string;
}

export default function GameOver({ payload, myRole }: Props) {
  const won = (payload.winner === 'Villagers' && myRole !== 'Werewolf')
    || (payload.winner === 'Werewolves' && myRole === 'Werewolf');
  const color = payload.winner === 'Villagers' ? '#5cb85c' : '#d9534f';
  const icon = payload.winner === 'Villagers' ? '\uD83C\uDF3E' : '\uD83D\uDC3A';

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
        {won ? '\uD83C\uDF89 You are on the winning side!' : '\uD83D\uDE14 Your side lost this time.'}
      </div>
      <div style={{ color: '#555', fontSize: 13 }}>You played as: <b style={{ color: '#aaa' }}>{myRole}</b></div>
    </div>
  );
}
