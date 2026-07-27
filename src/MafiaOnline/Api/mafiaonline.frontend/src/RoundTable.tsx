/**
 * Shared round-table SVG used by the Night and Day phases.
 * All colours come from CSS custom properties so the table automatically
 * retunes for the current phase (see [data-phase] in index.css).
 */
import React from 'react';
import type { SeatInfo } from './api';
import { useTranslation } from 'react-i18next';

export interface TableSeatState {
    seat: SeatInfo;
    isMe: boolean;
    isActive?: boolean;
    isAlive?: boolean;
    isSelected?: boolean;
    isTargetable?: boolean;
    hasVoted?: boolean;
    isEnshrouded?: boolean;
}

export interface VoteArrow {
    fromId: string;
    toId: string;
}

interface Props {
    seats: SeatInfo[];
    states: TableSeatState[];
    voteArrows?: VoteArrow[];
    centreLabel?: string;
    centreIcon?: string;
    onSeatClick?: (seat: SeatInfo) => void;
}

export default function RoundTable({
    seats, states, voteArrows = [], centreLabel, centreIcon, onSeatClick,
}: Props) {
    const { t } = useTranslation();
    const cx = 250, cy = 250, r = 170;
    const count = seats.length;

    const seatPos = (i: number) => {
        const angle = (2 * Math.PI * i) / count - Math.PI / 2;
        return { x: cx + r * Math.cos(angle), y: cy + r * Math.sin(angle) };
    };

    const stateFor = (id: string) => states.find(s => s.seat.id === id);

    return (
        <svg viewBox="0 0 500 500" className="table-svg" aria-label={t('mafiaTable')}>
            <defs>
                <radialGradient id="tableFill" cx="50%" cy="45%" r="60%">
                    <stop offset="0%" stopColor="var(--surface-2)" />
                    <stop offset="100%" stopColor="var(--surface)" />
                </radialGradient>
                <marker id="voteHead" markerWidth="7" markerHeight="7" refX="6" refY="3.5" orient="auto">
                    <path d="M0,0 L7,3.5 L0,7 Z" fill="var(--accent)" />
                </marker>
            </defs>

            <circle cx={cx} cy={cy} r={110} fill="url(#tableFill)"
                stroke="var(--border)" strokeWidth={4} />
            <circle cx={cx} cy={cy} r={92} fill="none"
                stroke="var(--border-soft)" strokeWidth={1} strokeDasharray="2 6" />
            {centreLabel && (
                <text x={cx} y={cy + 4} textAnchor="middle"
                    fill="var(--accent-2)" fontSize={14} fontWeight={700}
                    style={{ fontFamily: 'var(--font-display)', letterSpacing: '0.2em', textTransform: 'uppercase' }}>
                    {centreIcon ? `${centreIcon} ` : ''}{centreLabel}
                </text>
            )}

            {voteArrows.map((v, idx) => {
                const fi = seats.findIndex(s => s.id === v.fromId);
                const ti = seats.findIndex(s => s.id === v.toId);
                if (fi < 0 || ti < 0) return null;
                const p = seatPos(fi), q = seatPos(ti);
                const dx = q.x - p.x, dy = q.y - p.y;
                const d = Math.sqrt(dx * dx + dy * dy);
                const ux = dx / d, uy = dy / d;
                return (
                    <line key={`v${idx}`}
                        x1={p.x + ux * 36} y1={p.y + uy * 36}
                        x2={q.x - ux * 40} y2={q.y - uy * 40}
                        stroke="var(--accent)" strokeWidth={1.4}
                        strokeDasharray="5 4" opacity={0.75}
                        markerEnd="url(#voteHead)" className="vote-arrow" />
                );
            })}

            {seats.map((seat, i) => {
                const pos = seatPos(i);
                const st = stateFor(seat.id);
                const isMe = !!st?.isMe;
                const isActive = !!st?.isActive;
                const isAlive = st?.isAlive ?? true;
                const isSelected = !!st?.isSelected;
                const isTargetable = !!st?.isTargetable;
                const hasVoted = !!st?.hasVoted;
                const isEnshrouded = !!st?.isEnshrouded;

                const stroke = isSelected ? 'var(--accent)'
                    : isActive ? 'var(--accent-2)'
                    : isMe ? 'var(--town-2)'
                    : isAlive ? 'var(--border)' : 'var(--border-soft)';
                const fill = !isAlive ? 'rgba(0,0,0,0.4)'
                    : isMe ? 'color-mix(in oklab, var(--town) 25%, var(--surface))'
                    : 'var(--surface-2)';
                const label = seat.nickname.length > 10 ? seat.nickname.slice(0, 9) + '…' : seat.nickname;
                const textFill = !isAlive ? 'var(--text-dim)'
                    : isMe ? 'var(--accent-2)'
                    : 'var(--text)';

                return (
                    <g key={seat.id} transform={`translate(${pos.x},${pos.y})`}
                        className={isAlive ? undefined : 'seat--dead'}
                        style={{ cursor: isTargetable ? 'pointer' : 'default' }}
                        onClick={() => { if (isTargetable && onSeatClick) onSeatClick(seat); }}
                    >
                        <title>{seat.nickname}</title>
                        {isActive && (
                            <circle r={44} fill="var(--accent)" opacity={0.25}
                                className="seat-active-halo" />
                        )}
                        <circle r={34} fill={fill} stroke={stroke}
                            strokeWidth={isMe || isSelected || isActive ? 3 : 1.5}
                            opacity={isAlive ? 1 : 0.5} />
                        {isAlive && (
                            <>
                                <circle cx={22} cy={-22} r={10}
                                    fill="rgba(0,0,0,0.5)" stroke={stroke} strokeWidth={1} />
                                <text x={22} y={-18} textAnchor="middle"
                                    fill="var(--accent-2)" fontSize={10} fontWeight={700}>
                                    {seat.seatIndex + 1}
                                </text>
                            </>
                        )}
                        {!isAlive && (
                            <text y={6} textAnchor="middle" fontSize={18} opacity={0.7}>💀</text>
                        )}
                        {isAlive && (
                            <text y={4} textAnchor="middle" fill={textFill}
                                fontSize={11} fontWeight={isMe || isActive ? 700 : 500}
                                style={{ fontFamily: 'var(--font-body)' }}>
                                {label}
                            </text>
                        )}
                        {isMe && isAlive && (
                            <text y={17} textAnchor="middle" fill="var(--accent-2)"
                                fontSize={9} fontStyle="italic">({t('you')})</text>
                        )}
                        {isSelected && (
                            <text y={17} textAnchor="middle" fill="var(--accent)"
                                fontSize={9} fontWeight={700}>▸ {t('target')}</text>
                        )}
                        {hasVoted && isAlive && (
                            <text x={-22} y={-22} textAnchor="middle" fontSize={13}>✔️</text>
                        )}

                        {/* TODO: make it look better, now it's just a faint circle, make it actual shadow or mist */}
                        {isEnshrouded && isAlive && (
                            <circle r={40} fill="none" stroke="var(--wolf)" strokeWidth={2} opacity={0.12} />
                        )}
                    </g>
                );
            })}
        </svg>
    );
}
