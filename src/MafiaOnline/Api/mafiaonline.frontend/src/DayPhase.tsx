import React, { useEffect, useImperativeHandle, useRef, useState, forwardRef } from 'react';
import type { SeatInfo, RoleResponse, DayStartedPayload, VoteCastPayload, DayResolvedPayload } from './api';
import { castVote } from './api';
import { useTranslation } from 'react-i18next';
import RoundTable, { type TableSeatState, type VoteArrow } from './RoundTable';

export interface DayPhaseHandle {
    onDayStarted(payload: DayStartedPayload): void;
    onVoteCast(payload: VoteCastPayload): void;
    onDayResolved(payload: DayResolvedPayload): void;
    onRestoreState?(state: { remainingSeconds: number; votes?: Record<string, string | null> }): void;
}

interface Props {
    lobbyCode: string;
    allSeats: SeatInfo[];
    myPlayerId: string;
    myRole: RoleResponse;
}

const DayPhase = forwardRef<DayPhaseHandle, Props>(function DayPhase(
    { lobbyCode, allSeats, myPlayerId, myRole }, ref
) {
    const { t } = useTranslation();
    const [alivePlayers, setAlivePlayers] = useState<SeatInfo[]>([]);
    const [duration, setDuration] = useState(180);
    const [showVoteTargets, setShowVoteTargets] = useState(false);
    const [timeLeft, setTimeLeft] = useState(180);
    const [votes, setVotes] = useState<Record<string, string | null>>({});
    const [myVote, setMyVote] = useState<string | null>(null);
    const [resolved, setResolved] = useState<DayResolvedPayload | null>(null);
    const [submitting, setSubmitting] = useState(false);
    const [selectedTarget, setSelectedTarget] = useState<string | null>(null);
    const [log, setLog] = useState<string[]>([t('dayBreaks')]);
    const timerRef = useRef<ReturnType<typeof setInterval> | null>(null);

    useImperativeHandle(ref, () => ({
        onDayStarted(payload: DayStartedPayload) {
            setAlivePlayers([...payload.alivePlayers].sort((a, b) => a.seatIndex - b.seatIndex));
            setDuration(payload.durationSeconds);
            setShowVoteTargets(payload.showVoteTargets);
            setTimeLeft(payload.durationSeconds);
            setVotes({});
            setMyVote(null);
            setSelectedTarget(null);
            setResolved(null);
            setSubmitting(false);
            setLog([t('dayBreaks')]);

            if (timerRef.current) clearInterval(timerRef.current);
            timerRef.current = setInterval(() => {
                setTimeLeft(prev => {
                    if (prev <= 1) { clearInterval(timerRef.current!); return 0; }
                    return prev - 1;
                });
            }, 1000);
        },
        onVoteCast(payload: VoteCastPayload) {
            setVotes(prev => ({ ...prev, [payload.voterId]: payload.candidateId }));
            const voter = allSeats.find(s => s.id === payload.voterId);
            setLog(l => [...l, t('playerVoted', { nick: voter?.nickname ?? '?' })]);
        },
        onDayResolved(payload: DayResolvedPayload) {
            if (timerRef.current) clearInterval(timerRef.current);
            setResolved(payload);
            if (payload.banished.length > 0)
                setLog(l => [...l, t('banished', { list: payload.banished.join(', ') })]);
            else
                setLog(l => [...l, payload.thresholdMet ? t('drawNoOneBanished') : t('notEnoughVotes')]);
        },
        onRestoreState(state) {
            if (timerRef.current) clearInterval(timerRef.current);
            setTimeLeft(state.remainingSeconds);
            timerRef.current = setInterval(() => {
                setTimeLeft(prev => {
                    if (prev <= 1) { clearInterval(timerRef.current!); return 0; }
                    return prev - 1;
                });
            }, 1000);
            if (state.votes) {
                setVotes(state.votes);
                if (state.votes[myPlayerId] !== undefined) setMyVote(state.votes[myPlayerId] as string | null);
            }
        },
    }), [allSeats, t, myPlayerId]);

    useEffect(() => () => { if (timerRef.current) clearInterval(timerRef.current); }, []);

    const amAlive = alivePlayers.some(p => p.id === myPlayerId);
    const hasVoted = myVote !== null;
    const pct = duration > 0 ? timeLeft / duration : 0;
    const barColor = pct > 0.5 ? 'var(--town-2)' : pct > 0.2 ? '#c9a24c' : 'var(--danger-2)';

    async function handleVote() {
        if (!selectedTarget || hasVoted || !amAlive || submitting) return;
        setSubmitting(true);
        try {
            await castVote(lobbyCode, myPlayerId, selectedTarget);
            setMyVote(selectedTarget);
            setLog(l => [...l, t('yourVoteCast')]);
        } catch (e) { alert(String(e)); }
        finally { setSubmitting(false); }
    }

    const seatStates: TableSeatState[] = allSeats.map(seat => {
        const isAlive = alivePlayers.some(p => p.id === seat.id);
        const isMe = seat.id === myPlayerId;
        return {
            seat,
            isMe,
            isAlive,
            isSelected: seat.id === selectedTarget,
            isTargetable: !resolved && amAlive && !hasVoted && !isMe && isAlive && !submitting,
            hasVoted: votes[seat.id] !== undefined,
        };
    });

    const voteArrows: VoteArrow[] = showVoteTargets
        ? Object.entries(votes)
            .filter(([, to]) => !!to)
            .map(([from, to]) => ({ fromId: from, toId: to as string }))
        : [];

    const chosenName = selectedTarget
        ? (allSeats.find(s => s.id === selectedTarget)?.nickname ?? '?')
        : '—';

    return (
        <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 16 }}>
            <div className="role-badge" style={{ borderColor: `var(--${myRole.name === 'Werewolf' ? 'wolf' : 'town'})` }}>
                <span className="role-badge__label">{t('yourRole')}</span>
                <span className="role-badge__name"
                    style={{ color: `var(--${myRole.name === 'Werewolf' ? 'wolf' : 'town'})` }}>
                    {t(myRole.name)}
                </span>
            </div>

            {!resolved && (
                <div className="timer">
                    <div className="timer__head">
                        <span>{t('dayPhase')}</span>
                        <span style={{ color: barColor, fontWeight: 700, fontFamily: 'var(--font-mono)' }}>
                            {timeLeft}s
                        </span>
                    </div>
                    <div className="timer__bar">
                        <div className="timer__fill"
                            style={{ width: `${pct * 100}%`, background: barColor }} />
                    </div>
                    <div className="timer__meta">
                        {Object.keys(votes).length} / {alivePlayers.length} {t('voted')}
                        {hasVoted && <em>✓ {t('youVoted')}</em>}
                    </div>
                </div>
            )}

            <RoundTable
                seats={allSeats}
                states={seatStates}
                voteArrows={voteArrows}
                centreLabel={t('day')}
                centreIcon="☀"
                onSeatClick={s => setSelectedTarget(prev => prev === s.id ? null : s.id)}
            />

            {!resolved && amAlive && !hasVoted && (
                <button className="btn btn--danger"
                    disabled={!selectedTarget || submitting}
                    onClick={handleVote}>
                    🔥 {t('banish')} {chosenName}
                </button>
            )}

            {resolved && (
                <div className="resolution">
                    <div className="resolution__title">⚖ {t('votesIn')}</div>
                    {resolved.banished.length > 0
                        ? resolved.banished.map(n => (
                            <div key={n} className="resolution__killed">
                                🔥 <b>{n}</b> {t('hasBeenBanished')}
                            </div>
                        ))
                        : <div style={{ color: 'var(--text-muted)' }}>
                            {resolved.thresholdMet ? t('drawNoOneBanished') : t('notEnoughVotes')}
                        </div>
                    }
                    {resolved.tally.length > 0 && (
                        <div className="tally">
                            <div style={{ fontSize: 12, color: 'var(--text-dim)', marginBottom: 6,
                                letterSpacing: '0.15em', textTransform: 'uppercase' }}>
                                {t('voteTally')}
                            </div>
                            {[...resolved.tally].sort((a, b) => b.votes - a.votes).map(row => {
                                const name = allSeats.find(s => s.id === row.playerId)?.nickname ?? '?';
                                const width = alivePlayers.length ? (row.votes / alivePlayers.length) * 100 : 0;
                                return (
                                    <div key={row.playerId} className="tally__row">
                                        <span>{name}</span>
                                        <div className="tally__bar-outer">
                                            <div className="tally__bar-inner" style={{ width: `${width}%` }} />
                                        </div>
                                        <span className="tally__count">{row.votes}</span>
                                    </div>
                                );
                            })}
                        </div>
                    )}
                    <div style={{ color: 'var(--text-dim)', fontSize: 12, marginTop: 12, fontStyle: 'italic' }}>
                        {t('nightFallsSoon')}
                    </div>
                </div>
            )}

            <div className="event-log">
                {log.slice(-5).map((entry, i) => (
                    <div key={i} className="event-log__line">— {entry}</div>
                ))}
            </div>
        </div>
    );
});

export default DayPhase;
