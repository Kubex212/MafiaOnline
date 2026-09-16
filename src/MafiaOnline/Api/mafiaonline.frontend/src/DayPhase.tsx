import React, { useEffect, useImperativeHandle, useRef, useState, forwardRef } from 'react';
import type { SeatInfo, RoleResponse, DayStartedPayload, VoteCastPayload, DayResolvedPayload, GameConfig, ShowVotes } from './api';
import { castVote } from './api';
import { useTranslation } from 'react-i18next';
import RoundTable, { type TableSeatState, type VoteArrow } from './RoundTable';
import RoleInfo from './RoleInfo';
import EventLog from './EventLog';
import { addLog as pushLog } from './gameLog';
import { useUiFeedback } from './UiFeedback';

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
    deadIds?: string[];
    config?: GameConfig | null;
    /** Ids of fellow werewolves — empty unless the viewer is a werewolf. */
    otherWerewolvesIds?: string[];
}

const DayPhase = forwardRef<DayPhaseHandle, Props>(function DayPhase(
    { lobbyCode, allSeats, myPlayerId, myRole, deadIds = [], config, otherWerewolvesIds = [] }, ref
) {
    const { t } = useTranslation();
    const { toast } = useUiFeedback();
    const [alivePlayers, setAlivePlayers] = useState<SeatInfo[]>([]);
    const [duration, setDuration] = useState(180);
    const [showVoteTargets, setShowVoteTargets] = useState<ShowVotes>('Never');
    const [timeLeft, setTimeLeft] = useState(180);
    const [votes, setVotes] = useState<Record<string, string | null>>({});
    const [myVote, setMyVote] = useState<string | null | undefined>(undefined); // undefined = not voted yet, null = skipped, string = candidate id
    const [resolved, setResolved] = useState<DayResolvedPayload | null>(null);
    const [submitting, setSubmitting] = useState(false);
    const [selectedTarget, setSelectedTarget] = useState<string | null>(null);
    const [enshrouded, setEnshrouded] = useState<Set<string>>(new Set());
    const [asleep, setAsleep] = useState<Set<string>>(new Set());
    const timerRef = useRef<ReturnType<typeof setInterval> | null>(null);

    const setLog = (msg: string) => pushLog(msg, 'day');

    useImperativeHandle(ref, () => ({
        onDayStarted(payload: DayStartedPayload) {
            setAlivePlayers([...payload.alivePlayers].sort((a, b) => a.seatIndex - b.seatIndex));
            setDuration(payload.durationSeconds);
            setShowVoteTargets((payload.showVoteTargets ?? 'Never') as ShowVotes);
            setTimeLeft(payload.durationSeconds);
            setVotes({});
            setMyVote(undefined);
            setSelectedTarget(null);
            setResolved(null);
            setSubmitting(false);
            setEnshrouded(new Set((payload.enshrouded ?? []).map(id => id)));
            setAsleep(new Set((payload.asleep ?? []).map(id => id)));

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
            setLog(t('playerVoted', { nick: voter?.nickname ?? '?' }));
        },
        onDayResolved(payload: DayResolvedPayload) {
            if (timerRef.current) clearInterval(timerRef.current);
            setResolved(payload);
            if (payload.banished.length > 0)
                setLog(t('banished', { list: payload.banished.join(', ') }));
            else
                setLog(payload.thresholdMet ? t('drawNoOneBanished') : t('notEnoughVotes'));
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
                if (Object.prototype.hasOwnProperty.call(state.votes, myPlayerId))
                    setMyVote(state.votes[myPlayerId] as string | null);
                else
                    setMyVote(undefined);
            }
        },
    }), [allSeats, t, myPlayerId]);

    useEffect(() => () => { if (timerRef.current) clearInterval(timerRef.current); }, []);

    const amAlive = alivePlayers.some(p => p.id === myPlayerId);
    const hasVoted = myVote !== undefined;
    const pct = duration > 0 ? timeLeft / duration : 0;
    const barColor = pct > 0.5 ? 'var(--town-2)' : pct > 0.2 ? '#c9a24c' : 'var(--danger-2)';

    async function handleVote() {
        if (!selectedTarget || hasVoted || !amAlive || submitting) return;
        setSubmitting(true);
        try {
            await castVote(lobbyCode, selectedTarget);
            setMyVote(selectedTarget);
            setLog(t('yourVoteCast'));
        } catch (e) { toast(String(e), 'error'); }
        finally { setSubmitting(false); }
    }

    async function handleSkip() {
        if (hasVoted || !amAlive || submitting) return;
        setSubmitting(true);
        try {
            await castVote(lobbyCode, null);
            setMyVote(null);
            setLog(t('yourVoteCast'));
        } catch (e) { toast(String(e), 'error'); }
        finally { setSubmitting(false); }
    }

    const seatStates: TableSeatState[] = allSeats.map(seat => {
        const isAlive = alivePlayers.length > 0
            ? alivePlayers.some(p => p.id === seat.id) && !deadIds.includes(seat.id)
            : !deadIds.includes(seat.id);
        const isMe = seat.id === myPlayerId;
        const isEnshrouded = enshrouded.has(seat.id);
        const isAsleep = asleep.has(seat.id);
        return {
            seat,
            isMe,
            isAlive,
            isSelected: seat.id === selectedTarget,
            isTargetable: !resolved && amAlive && !hasVoted && !isMe && isAlive && !submitting && !isEnshrouded,
            hasVoted: votes[seat.id] !== undefined,
            isEnshrouded,
            isAsleep,
            isWerewolf: otherWerewolvesIds?.includes(seat.id) || (isMe && myRole.teamId === 1),
        };
    });

    const votingOver = !!resolved
        || (alivePlayers.length > 0 && Object.keys(votes).length >= alivePlayers.length);
    const revealTargets = showVoteTargets === 'Always'
        || (showVoteTargets === 'AfterVotingEnds' && votingOver);

    const voteArrows: VoteArrow[] = revealTargets
        ? Object.entries(votes)
            .filter(([, to]) => !!to)
            .map(([from, to]) => ({ kind: 'vote', fromId: from, toId: to as string }))
        : [];

    const chosenName = selectedTarget
        ? (allSeats.find(s => s.id === selectedTarget)?.nickname ?? '?')
        : '—';

    return (
        <div className="phase-stack">
            <RoleInfo role={myRole} config={config} />

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
                <div className="phase-actions">
                    <button className="btn btn--danger"
                        disabled={!selectedTarget || submitting}
                        onClick={handleVote}>
                        🔥 {t('banish')} <span className="btn__ellipsis">{chosenName}</span>
                    </button>
                    <button className="btn" disabled={submitting} onClick={handleSkip}>
                        ➖ {t('skipVote')}
                    </button>
                </div>
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

            <EventLog />
        </div>
    );
});

export default DayPhase;
