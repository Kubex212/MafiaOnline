import React, { useEffect, useImperativeHandle, useRef, useState, forwardRef } from 'react';
import type { SeatInfo, RoleResponse, DayStartedPayload, VoteCastPayload, DayResolvedPayload } from './api';
import { castVote } from './api';

export interface DayPhaseHandle {
    onDayStarted(payload: DayStartedPayload): void;
    onVoteCast(payload: VoteCastPayload): void;
    onDayResolved(payload: DayResolvedPayload): void;
}

interface Props {
    lobbyCode: string;
    allSeats: SeatInfo[];       // all seats including dead players (for layout)
    myPlayerId: string;
    myRole: RoleResponse;
}

const TEAM_COLORS: Record<string, string> = { Werewolf: '#d9534f', Town: '#5cb85c' };

const DayPhase = forwardRef<DayPhaseHandle, Props>(function DayPhase(
    { lobbyCode, allSeats, myPlayerId, myRole }, ref
) {
    const [alivePlayers, setAlivePlayers] = useState<SeatInfo[]>([]);
    const [duration, setDuration] = useState(180);
    const [showVoteTargets, setShowVoteTargets] = useState(false);
    const [timeLeft, setTimeLeft] = useState(180);
    const [votes, setVotes] = useState<Record<string, string | null>>({}); // voterId -> candidateId | null
    const [myVote, setMyVote] = useState<string | null>(null);
    const [resolved, setResolved] = useState<DayResolvedPayload | null>(null);
    const [submitting, setSubmitting] = useState(false);
    const [selectedTarget, setSelectedTarget] = useState<string | null>(null);
    const [log, setLog] = useState<string[]>(["\u2600\uFE0F Day breaks\u2026 discuss and vote! :)"]);
    const timerRef = useRef<ReturnType<typeof setInterval> | null>(null);

    useImperativeHandle(ref, () => ({
        onDayStarted(payload: DayStartedPayload) {
            setAlivePlayers(payload.alivePlayers.sort((a, b) => a.seatIndex - b.seatIndex));
            setDuration(payload.durationSeconds);
            setShowVoteTargets(payload.showVoteTargets);
            setTimeLeft(payload.durationSeconds);
            setVotes({});
            setMyVote(null);
            setSelectedTarget(null);
            setResolved(null);
            setSubmitting(false);
            setLog(["\u2600\uFE0F Day breaks\u2026 discuss and vote! :))"]);

            if (timerRef.current) clearInterval(timerRef.current);
            timerRef.current = setInterval(() => {
                setTimeLeft(t => {
                    if (t <= 1) { clearInterval(timerRef.current!); return 0; }
                    return t - 1;
                });
            }, 1000);
        },

        onVoteCast(payload: VoteCastPayload) {
            setVotes(prev => ({ ...prev, [payload.voterId]: payload.candidateId }));
            const voter = allSeats.find(s => s.id === payload.voterId);
            setLog(l => [...l, `\u2714\uFE0F ${voter?.nickname ?? '?'} has voted`]);
        },

        onDayResolved(payload: DayResolvedPayload) {
            if (timerRef.current) clearInterval(timerRef.current);
            setResolved(payload);
            if (payload.banished.length > 0)
                setLog(l => [...l, `\uD83D\uDEAB Banished: ${payload.banished.join(', ')}`]);
            else
                setLog(l => [...l, payload.thresholdMet
                    ? '\uD83E\uDD1D It was a draw \u2014 no one was banished.'
                    : '\uD83E\uDD37 Not enough votes \u2014 no one was banished.']);
        },
    }), [allSeats]);

    useEffect(() => () => { if (timerRef.current) clearInterval(timerRef.current); }, []);

    const amAlive = alivePlayers.some(p => p.id === myPlayerId);
    const hasVoted = myVote !== null;

    async function handleVote() {
        if (!selectedTarget || hasVoted || !amAlive || submitting) return;
        setSubmitting(true);
        try {
            await castVote(lobbyCode, myPlayerId, selectedTarget);
            setMyVote(selectedTarget);
            setLog(l => [...l, '\u2714\uFE0F Your vote was cast']);
        } catch (e) { alert(String(e)); }
        finally { setSubmitting(false); }
    }

    // ?? SVG table ?????????????????????????????????????????????????????????????

    const cx = 250, cy = 250, r = 170;
    const seats = allSeats; // use all seats for stable layout; dead players shown greyed

    const pct = timeLeft / duration;
    const barColor = pct > 0.5 ? '#5cb85c' : pct > 0.2 ? '#f0ad4e' : '#d9534f';

    return (
        <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 14 }}>

            {/* Role badge */}
            <div style={{
                padding: '6px 24px', borderRadius: 10,
                border: `2px solid ${TEAM_COLORS[myRole.team] ?? '#aaa'}`,
                background: '#1a1a2e', textAlign: 'center',
            }}>
                <span style={{ fontSize: 11, color: '#aaa', textTransform: 'uppercase', letterSpacing: 2 }}>Your role </span>
                <span style={{ fontSize: 18, fontWeight: 700, color: TEAM_COLORS[myRole.team] ?? '#fff' }}>{myRole.name}</span>
            </div>

            {/* Timer bar */}
            {!resolved && (
                <div style={{ width: '100%', maxWidth: 460 }}>
                    <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 12, color: '#aaa', marginBottom: 4 }}>
                        <span>{'\u2600\uFE0F'} Day phase</span>
                        <span style={{ color: barColor, fontWeight: 700 }}>{timeLeft}s</span>
                    </div>
                    <div style={{ height: 6, background: '#222', borderRadius: 3, overflow: 'hidden' }}>
                        <div style={{
                            height: '100%', borderRadius: 3, background: barColor,
                            width: `${pct * 100}%`, transition: 'width 0.9s linear, background 0.3s'
                        }} />
                    </div>
                    <div style={{ fontSize: 12, color: '#555', marginTop: 4 }}>
                        {votes ? `${Object.keys(votes).length} / ${alivePlayers.length} voted` : ''}
                        {hasVoted && <span style={{ color: '#5cb85c', marginLeft: 8 }}>{'\u2714\uFE0F'} You voted</span>}
                    </div>
                </div>
            )}

            {/* Round table */}
            <svg viewBox="0 0 500 500" style={{ width: '100%', maxWidth: 460, userSelect: 'none' }}>
                {/* Table surface */}
                <circle cx={cx} cy={cy} r={100} fill="#1e2e1e" stroke="#3a5a3a" strokeWidth={6} />
                <text x={cx} y={cy + 6} textAnchor="middle" fill="#3a5a3a" fontSize={13} fontWeight={600}>
                    {'\u2600\uFE0F'} Day test!!!
                </text>

                {seats.map((seat, i) => {
                    const angle = (2 * Math.PI * i) / seats.length - Math.PI / 2;
                    const px = cx + r * Math.cos(angle);
                    const py = cy + r * Math.sin(angle);

                    const isAlive = alivePlayers.some(p => p.id === seat.id);
                    const isMe = seat.id === myPlayerId;
                    const isSelected = seat.id === selectedTarget;
                    const votedForMe = showVoteTargets && Object.entries(votes).some(([, c]) => c === seat.id);
                    const iVotedFor = myVote === seat.id;
                    const hasThisVoted = votes[seat.id] !== undefined;
                    const isTargetable = !resolved && amAlive && !hasVoted && !isMe && isAlive && !submitting;

                    const bg = !isAlive ? '#111' : isMe ? '#1a3a1a' : '#1e1e2e';
                    const border = isSelected ? '#f0c040'
                        : iVotedFor ? '#5cb85c'
                            : isMe ? '#5cb85c'
                                : isAlive ? '#555' : '#2a2a2a';
                    const textFill = !isAlive ? '#333' : isMe ? '#f0c040' : '#ddd';
                    const label = seat.nickname.length > 10 ? seat.nickname.slice(0, 9) + '\u2026' : seat.nickname;

                    // Arrow from voter to their target (visible only when showVoteTargets)
                    const myVoteArrow = showVoteTargets && votes[seat.id] && isAlive
                        ? (() => {
                            const targetSeat = seats.find(s => s.id === votes[seat.id]);
                            if (!targetSeat) return null;
                            const ti = seats.indexOf(targetSeat);
                            const ta = (2 * Math.PI * ti) / seats.length - Math.PI / 2;
                            const tx = cx + r * Math.cos(ta);
                            const ty = cy + r * Math.sin(ta);
                            // Draw arrow from voter toward target, offset slightly to the player radius
                            const dx = tx - px, dy = ty - py;
                            const dist = Math.sqrt(dx * dx + dy * dy);
                            const ux = dx / dist, uy = dy / dist;
                            const x1 = px + ux * 36, y1 = py + uy * 36;
                            const x2 = tx - ux * 36, y2 = ty - uy * 36;
                            return <line key={`arrow-${seat.id}`} x1={x1} y1={y1} x2={x2} y2={y2}
                                stroke="#f0c040" strokeWidth={1.5} strokeDasharray="4 3"
                                markerEnd="url(#arrowhead)" opacity={0.7} />;
                        })()
                        : null;

                    return (
                        <React.Fragment key={seat.id}>
                            {myVoteArrow}
                            <g transform={`translate(${px},${py})`}
                                style={{ cursor: isTargetable ? 'pointer' : 'default' }}
                                onClick={() => { if (isTargetable) setSelectedTarget(seat.id === selectedTarget ? null : seat.id); }}>
                                <circle r={34} fill={bg} stroke={border} strokeWidth={isMe || isSelected ? 3 : 2} opacity={isAlive ? 1 : 0.4} />
                                {/* Has-voted checkmark */}
                                {hasThisVoted && isAlive && (
                                    <text x={0} y={-24} textAnchor="middle" fontSize={14}>{'\u2714\uFE0F'}</text>
                                )}
                                {/* Dead marker */}
                                {!isAlive && <text y={6} textAnchor="middle" fontSize={16} opacity={0.5}>{'\uD83D\uDC80'}</text>}
                                {/* Seat number */}
                                {isAlive && <circle cx={22} cy={-22} r={10} fill="#333" stroke={border} strokeWidth={1} />}
                                {isAlive && <text x={22} y={-18} textAnchor="middle" fill="#ccc" fontSize={10} fontWeight={600}>{seat.seatIndex + 1}</text>}
                                {/* Nickname */}
                                {isAlive && (
                                    <text y={4} textAnchor="middle" fill={textFill}
                                        fontSize={isMe ? 11 : 10} fontWeight={isMe ? 700 : 400}>{label}</text>
                                )}
                                {isMe && isAlive && <text y={17} textAnchor="middle" fill="#f0c040" fontSize={9} fontStyle="italic">(you)</text>}
                                {isSelected && <text y={17} textAnchor="middle" fill="#f0c040" fontSize={9}>{'\u25CE'} target</text>}
                            </g>
                        </React.Fragment>
                    );
                })}

                {/* Arrow marker definition */}
                <defs>
                    <marker id="arrowhead" markerWidth="6" markerHeight="6" refX="3" refY="3" orient="auto">
                        <path d="M0,0 L6,3 L0,6 Z" fill="#f0c040" opacity={0.8} />
                    </marker>
                </defs>
            </svg>

            {/* Vote button */}
            {!resolved && amAlive && !hasVoted && (
                <button
                    disabled={!selectedTarget || submitting}
                    onClick={handleVote}
                    style={{
                        border: 'none', borderRadius: 8, padding: '10px 32px',
                        background: selectedTarget ? '#c0392b' : '#333',
                        color: '#fff', fontWeight: 700, fontSize: 14,
                        cursor: selectedTarget ? 'pointer' : 'default',
                        opacity: (!selectedTarget || submitting) ? 0.6 : 1,
                    }}>
                    {'\uD83D\uDEAB'} Banish {selectedTarget ? (allSeats.find(s => s.id === selectedTarget)?.nickname ?? '?') : '\u2014'}
                </button>
            )}

            {/* Resolution panel */}
            {resolved && (
                <div style={{ maxWidth: 460, width: '100%', borderRadius: 10, padding: '14px 20px', background: '#1a1a2e', border: '2px solid #333' }}>
                    <div style={{ fontWeight: 700, color: '#f0c040', marginBottom: 8 }}>{'\uD83C\uDF06'} Votes are in!</div>
                    {resolved.banished.length > 0
                        ? resolved.banished.map(n => (
                            <div key={n} style={{ color: '#d9534f' }}>{'\uD83D\uDEAB'} <b>{n}</b> has been banished from the village.</div>
                        ))
                        : <div style={{ color: '#aaa' }}>{resolved.thresholdMet ? '\uD83E\uDD1D It was a draw \u2014 no one banished.' : '\uD83E\uDD37 Not enough votes \u2014 no one banished.'}</div>
                    }
                    {resolved.tally.length > 0 && (
                        <div style={{ marginTop: 10 }}>
                            <div style={{ fontSize: 12, color: '#666', marginBottom: 4 }}>Vote tally:</div>
                            {resolved.tally
                                .sort((a, b) => b.votes - a.votes)
                                .map(t => {
                                    const name = allSeats.find(s => s.id === t.playerId)?.nickname ?? '?';
                                    return (
                                        <div key={t.playerId} style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 3 }}>
                                            <span style={{ color: '#ddd', width: 100, fontSize: 13 }}>{name}</span>
                                            <div style={{ flex: 1, height: 8, background: '#222', borderRadius: 4, overflow: 'hidden' }}>
                                                <div style={{ height: '100%', background: '#c0392b', borderRadius: 4, width: `${(t.votes / alivePlayers.length) * 100}%` }} />
                                            </div>
                                            <span style={{ color: '#aaa', fontSize: 12, width: 24, textAlign: 'right' }}>{t.votes}</span>
                                        </div>
                                    );
                                })}
                        </div>
                    )}
                    <div style={{ color: '#555', fontSize: 12, marginTop: 10 }}>{'\uD83C\uDF19'} Night falls again soon{'\u2026'}</div>
                </div>
            )}

            {/* Event log */}
            <div style={{ maxWidth: 460, width: '100%', borderRadius: 8, background: '#111', padding: '8px 14px' }}>
                {log.slice(-5).map((entry, i) => (
                    <div key={i} style={{ fontSize: 12, color: '#888', lineHeight: 1.6 }}>{entry}</div>
                ))}
            </div>
        </div>
    );
});

export default DayPhase;
