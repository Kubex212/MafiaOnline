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
    /** Targetable for a role ability (e.g. Dracula enshroud) */
    isTargetableByAbility?: boolean;
    /** Targetable for an attack (werewolf kill). */
    isTargetableByAttack?: boolean;
    /** Targetable by day voting */
    isTargetable?: boolean;
    hasVoted?: boolean;
    hasSubmitted?: boolean;
    isEnshrouded?: boolean;
    isAsleep?: boolean;
    /** Fellow werewolf (or self) — only ever set for viewers who are werewolves themselves. */
    isWerewolf?: boolean;
}

export interface VoteArrow {
    fromId: string;
    toId: string;
    /** 'vote' = village banish vote (gold), 'attack' = werewolf kill vote (blood red), 'tripwire' = ranger's ability (gray). */
    kind?: 'vote' | 'attack' | 'tripwire';
}

export interface ActionInfo {
    roleName: string;
    targets: string[];
}

interface Props {
    seats: SeatInfo[];
    states: TableSeatState[];
    voteArrows?: VoteArrow[];
    centreLabel?: string;
    centreIcon?: string;
    onSeatClick?: (seat: SeatInfo) => void;
    myAction?: ActionInfo;
}

const arrowStyles = {
    attack: {
        stroke: 'var(--wolf)',
        strokeWidth: 2,
        dasharray: '5 4',
        opacity: 0.9,
        markerStart: undefined,
        markerEnd: 'url(#attackHead)',
        className: 'vote-arrow vote-arrow--attack',
    },
    vote: {
        stroke: 'var(--accent)',
        strokeWidth: 1.4,
        dasharray: '5 4',
        opacity: 0.75,
        markerStart: undefined,
        markerEnd: 'url(#voteHead)',
        className: 'vote-arrow',
    },
    tripwire: {
        stroke: 'var(--text)',
        strokeWidth: 1.8,
        dasharray: '2 3',
        opacity: 0.85,
        markerStart: undefined,
        markerEnd: undefined,
        className: 'vote-arrow vote-arrow--tripwire',
    }
} as const;

export default function RoundTable({
    seats, states, voteArrows = [], centreLabel, centreIcon, onSeatClick, myAction
}: Props) {
    const { t } = useTranslation();
    const cx = 250, cy = 250, r = 170;
    const count = seats.length;

    const seatPos = (i: number) => {
        const angle = (2 * Math.PI * i) / count - Math.PI / 2;
        return { x: cx + r * Math.cos(angle), y: cy + r * Math.sin(angle) };
    };

    if (myAction) {
        // process visual effects for submitted action - currently only the Ranger's tripwire ability is visualized
        if (myAction.roleName == 'Ranger' && myAction.targets.length === 2) {
            voteArrows.push({
                fromId: myAction.targets[0],
                toId: myAction.targets[1],
                kind: 'tripwire'
            });
        }
    }

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
                <marker id="attackHead" markerWidth="7" markerHeight="7" refX="6" refY="3.5" orient="auto">
                    <path d="M0,0 L7,3.5 L0,7 Z" fill="var(--wolf)" />
                </marker>
                {/* Creeping mist used to mark an enshrouded player. */}
                <radialGradient id="mistFill" cx="50%" cy="50%" r="50%">
                    <stop offset="0%" stopColor="#0b0f14" stopOpacity="0.85" />
                    <stop offset="55%" stopColor="#2a2233" stopOpacity="0.5" />
                    <stop offset="100%" stopColor="#2a2233" stopOpacity="0" />
                </radialGradient>
                <filter id="mistBlur" x="-60%" y="-60%" width="220%" height="220%">
                    <feTurbulence type="fractalNoise" baseFrequency="0.035" numOctaves={3} seed={7} result="noise" />
                    <feDisplacementMap in="SourceGraphic" in2="noise" scale={12} xChannelSelector="R" yChannelSelector="G" />
                    <feGaussianBlur stdDeviation={3.5} />
                </filter>
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

                const p = seatPos(fi);
                const q = seatPos(ti);
                const dx = q.x - p.x;
                const dy = q.y - p.y;
                const d = Math.sqrt(dx * dx + dy * dy);
                const ux = dx / d;
                const uy = dy / d;
                const style = arrowStyles[v.kind ?? 'vote'];

                return (
                    <line
                        key={`v${idx}`}
                        x1={p.x + ux * 36}
                        y1={p.y + uy * 36}
                        x2={q.x - ux * 40}
                        y2={q.y - uy * 40}
                        stroke={style.stroke}
                        strokeWidth={style.strokeWidth}
                        strokeDasharray={style.dasharray}
                        opacity={style.opacity}
                        markerStart={style.markerStart}
                        markerEnd={style.markerEnd}
                        className={style.className}
                    />
                );
            })}

            {seats.map((seat, i) => {
                const pos = seatPos(i);
                const st = stateFor(seat.id);
                const isMe = !!st?.isMe;
                const isActive = !!st?.isActive;
                const isAlive = st?.isAlive ?? true;
                const isSelected = !!st?.isSelected;
                const isTargetable = !!st?.isTargetableByAbility || !!st?.isTargetableByAttack || !!st?.isTargetable;
                const hasVoted = !!st?.hasVoted;
                const hasSubmitted = !!st?.hasSubmitted;
                const isEnshrouded = !!st?.isEnshrouded;
                const isAsleep = !!st?.isAsleep;
                const displayAsWerewolf = !!st?.isWerewolf;

                const stroke = isSelected ? 'var(--accent)'
                    : isActive ? 'var(--accent-2)'
                    : displayAsWerewolf && isAlive ? 'var(--wolf)'
                    : isMe ? 'var(--town-2)'
                    : isAlive ? 'var(--border)' : 'var(--border-soft)';
                const fill = !isAlive ? 'rgba(0,0,0,0.4)'
                    : displayAsWerewolf ? 'color-mix(in oklab, var(--wolf) 28%, var(--surface))'
                    : isMe ? 'color-mix(in oklab, var(--town) 25%, var(--surface))'
                    : 'var(--surface-2)';
                const label = seat.nickname.length > 10 ? seat.nickname.slice(0, 9) + '…' : seat.nickname;
                const textFill = !isAlive ? 'var(--text-dim)'
                    : displayAsWerewolf && !isMe ? 'var(--danger-2)'
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
                            strokeWidth={isMe || isSelected || isActive || displayAsWerewolf ? 3 : 1.5}
                            opacity={isAlive ? 1 : 0.5} />
                        {displayAsWerewolf && isAlive && (
                            <>
                                <circle r={40} fill="none" stroke="var(--wolf)" strokeWidth={1.5}
                                    opacity={0.5} strokeDasharray="4 6" className="seat-ally-ring" />
                                <text x={-22} y={22} textAnchor="middle" fontSize={12}
                                    aria-hidden>🐺</text>
                            </>
                        )}
                        {isAlive && (
                            <>
                                <circle cx={-21} cy={-21} r={10}
                                    fill="rgba(0,0,0,0.6)" stroke={stroke} strokeWidth={1} />
                                <text x={-21} y={-17} textAnchor="middle"
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
                            <text y={-10} textAnchor="middle" fill="var(--accent-2)"
                                fontSize={9} fontStyle="italic">({t('you')})</text>
                        )}
                        {isSelected && (
                            <text y={17} textAnchor="middle" fill="var(--accent)"
                                fontSize={9} fontWeight={700}>▸ {t('target')}</text>
                        )}
                        {(hasVoted || hasSubmitted) && isAlive && (
                            <text x={-22} y={-22} textAnchor="middle" fontSize={13}>✔️</text>
                        )}
                        {isEnshrouded && isAlive && (
                            <g className="seat-mist" pointerEvents="none" aria-hidden>
                                <g filter="url(#mistBlur)">
                                    <circle r={44} fill="url(#mistFill)" />
                                    <circle cx={-10} cy={8} r={26} fill="url(#mistFill)" opacity={0.8} />
                                    <circle cx={12} cy={-6} r={22} fill="url(#mistFill)" opacity={0.7} />
                                </g>
                                <circle r={40} fill="none" stroke="var(--wolf)"
                                    strokeWidth={1} opacity={0.18} strokeDasharray="3 7" />
                            </g>
                        )}
                        {isAsleep && isAlive && (
                            <g className="seat-sleep" pointerEvents="none" aria-hidden>
                                <text x={24} y={-25} fontSize={14} fontWeight={700}>Z</text>
                                <text x={35} y={-35} fontSize={10} fontWeight={700}>z</text>
                                <text x={43} y={-43} fontSize={7} fontWeight={700}>z</text>
                            </g>
                        )}
                    </g>
                );
            })}
        </svg>
    );
}
