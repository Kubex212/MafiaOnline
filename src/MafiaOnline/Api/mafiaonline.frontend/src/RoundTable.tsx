import React from 'react';
import type { SeatInfo, RoleResponse } from './api';

interface Props {
  seats: SeatInfo[];          // all players in seat order
  myPlayerId: string;
  myRole: RoleResponse | null;
}

const ROLE_COLORS: Record<string, { bg: string; border: string }> = {
  Werewolf: { bg: '#5c1a1a', border: '#d9534f' },
  Guardian:  { bg: '#1a3a5c', border: '#5bc0de' },
  Detective: { bg: '#1a3a2a', border: '#5cb85c' },
  Villager:  { bg: '#2a2a2a', border: '#aaa' },
};

const TEAM_COLORS: Record<string, string> = {
  Werewolf: '#d9534f',
  Town: '#5cb85c',
};

export default function RoundTable({ seats, myPlayerId, myRole }: Props) {
  const count = seats.length;
  // Radius of the circle (in px). The SVG viewBox is 500×500, centre at 250,250.
  const cx = 250, cy = 250, r = 170;

  return (
    <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 24 }}>
      {/* Role card for this player */}
      {myRole && (
        <div style={{
          padding: '12px 32px',
          borderRadius: 12,
          border: `2px solid ${TEAM_COLORS[myRole.team] ?? '#aaa'}`,
          background: '#1a1a2e',
          textAlign: 'center',
        }}>
          <div style={{ fontSize: 12, color: '#aaa', letterSpacing: 2, textTransform: 'uppercase' }}>Your role</div>
          <div style={{ fontSize: 28, fontWeight: 700, color: TEAM_COLORS[myRole.team] ?? '#fff', marginTop: 4 }}>
            {myRole.name}
          </div>
          <div style={{ fontSize: 13, color: '#ccc', marginTop: 2 }}>Team: {myRole.team}</div>
        </div>
      )}

      {/* Round table SVG */}
      <svg
        viewBox="0 0 500 500"
        style={{ width: '100%', maxWidth: 480, userSelect: 'none' }}
        aria-label="Round table"
      >
        {/* Table surface */}
        <circle cx={cx} cy={cy} r={100} fill="#2c1a0e" stroke="#7a4a1e" strokeWidth={6} />
        <text x={cx} y={cy + 6} textAnchor="middle" fill="#7a4a1e" fontSize={14} fontWeight={600}>
          Mafia Table
        </text>

        {seats.map((seat, i) => {
          const angle = (2 * Math.PI * i) / count - Math.PI / 2;
          const px = cx + r * Math.cos(angle);
          const py = cy + r * Math.sin(angle);
          const isMe = seat.id === myPlayerId;
          const colors = isMe && myRole
            ? (ROLE_COLORS[myRole.name] ?? ROLE_COLORS['Villager'])
            : { bg: '#1e1e2e', border: isMe ? '#f0c040' : '#555' };

          // Shorten nickname if needed
          const label = seat.nickname.length > 10 ? seat.nickname.slice(0, 9) + '…' : seat.nickname;

          return (
            <g key={seat.id} transform={`translate(${px},${py})`}>
              {/* Seat circle */}
              <circle
                r={34}
                fill={colors.bg}
                stroke={colors.border}
                strokeWidth={isMe ? 3 : 2}
              />
              {/* Seat number badge */}
              <circle cx={22} cy={-22} r={10} fill="#333" stroke={colors.border} strokeWidth={1} />
              <text x={22} y={-18} textAnchor="middle" fill="#ccc" fontSize={10} fontWeight={600}>
                {seat.seatIndex + 1}
              </text>
              {/* Nickname */}
              <text
                y={5}
                textAnchor="middle"
                fill={isMe ? '#f0c040' : '#ddd'}
                fontSize={isMe ? 11 : 10}
                fontWeight={isMe ? 700 : 400}
              >
                {label}
              </text>
              {/* "You" label */}
              {isMe && (
                <text y={18} textAnchor="middle" fill="#f0c040" fontSize={9} fontStyle="italic">
                  (you)
                </text>
              )}
            </g>
          );
        })}
      </svg>
    </div>
  );
}
