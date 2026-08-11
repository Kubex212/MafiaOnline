import React, { useEffect, useImperativeHandle, useRef, useState, forwardRef } from 'react';
import type { SeatInfo, RoleResponse, NightTurnStarted, NightResolvedPayload, AnyClueResponse, GameConfig } from './api';
import { submitNightAction, skipNightTurn, getPlayerNightInfo } from './api';
import { useTranslation } from 'react-i18next';
import RoundTable, { type TableSeatState, type VoteArrow } from './RoundTable';
import RoleInfo from './RoleInfo';
import EventLog from './EventLog';
import { addLog as pushLog } from './gameLog';

interface Props {
    lobbyCode: string;
    seats: SeatInfo[];
    myPlayerId: string;
    myRole: RoleResponse;
    deadIds?: string[];
    config?: GameConfig | null;
    otherWerewolvesIds?: string[];
}

export interface NightPhaseHandle {
    onTurnStarted(turn: NightTurnStarted): void;
    onNightResolved(payload: NightResolvedPayload): void;
    addLog(message: string): void;
    onWerewolfAttackCast(vote: { actorId: string; targetId: string }): void;
}

export interface ActionData {
    type: ActionType;
    targets: string[];
    attack: string[];
}

export enum ActionType {
    Attack = 0,
    Ability = 1,
    Other = 2,
}

const TURN_SECONDS = 1000;

const NightPhase = forwardRef<NightPhaseHandle, Props>(function NightPhase(
    { lobbyCode, seats, myPlayerId, myRole, deadIds = [], config, otherWerewolvesIds = [] }, ref
) {
    const { t } = useTranslation();
    const [activeTurn, setActiveTurn] = useState<NightTurnStarted | null>(null);
    const [timeLeft, setTimeLeft] = useState(TURN_SECONDS);
    const [resolved, setResolved] = useState<NightResolvedPayload | null>(null);
    const [nightInfo, setNightInfo] = useState<AnyClueResponse | null>(null);
    const [selectedTargets, setSelectedTargets] = useState<string[]>([]);
    const [submitting, setSubmitting] = useState(false);
    const [wolfVotes, setWolfVotes] = useState<Record<string, string>>({});
    const timerRef = useRef<ReturnType<typeof setInterval> | null>(null);
    const setLog = (msg: string) => pushLog(msg, 'night');
    const [attackSubmitted, setAttackSubmitted] = useState(false);
    const [abilitySubmitted, setAbilitySubmitted] = useState(false);

    useImperativeHandle(ref, () => ({
        onTurnStarted(turn: NightTurnStarted) {
            setActiveTurn(turn);
            setTimeLeft(turn.timeoutSeconds ?? TURN_SECONDS);
            setSelectedTargets([]);
            setSubmitting(false);
            setAttackSubmitted(false);
            setAbilitySubmitted(false);
            const who = seats.find(s => s.id === turn.activePlayerId);
            const isMe = turn.activePlayerId === myPlayerId;
            setLog(isMe ? t('yourTurn') : t('playerTurn', { nick: who?.nickname ?? '?' }));
            if (timerRef.current) clearInterval(timerRef.current);
            timerRef.current = setInterval(() => {
                setTimeLeft(prev => {
                    if (prev <= 1) { clearInterval(timerRef.current!); return 0; }
                    return prev - 1;
                });
            }, 1000);
        },
        async onNightResolved(payload: NightResolvedPayload) {
            if (timerRef.current) clearInterval(timerRef.current);
            setActiveTurn(null);
            setResolved(payload);
            setWolfVotes({});
            if (payload.killed.length > 0)
                setLog(t('killed', { list: payload.killed.join(', ') }));
            else if (payload.attackBlocked)
                setLog(t('attackBlocked'));
            else
                setLog(t('quietNight'));
            try {
                const info = await getPlayerNightInfo(lobbyCode);
                setNightInfo(info);
                if (!info) return;
                if (info.type === 'Detective') {
                    setLog(t('detectiveClueLong', { player: info.clue, role: t('innocent') }));
                } else if (info.type === 'DogTamer') {
                    setLog(t('dogClue', { barked: info.clue ? t('dogBarked') : t('dogQuiet') }));
                } else if (info.type === 'Seer') {
                    setLog(t('seerClueLong', {
                        a: String(info.data.SubjectAName),
                        b: String(info.data.SubjectBName),
                        relation: info.clue ? t('sameTeam') : t('differentTeams'),
                    }));
                } else if (info.type === 'Trailer') {
                    setLog(t('trailerClueLong', { dir: t(info.clue) }));
                } else if (info.type === 'Peeper') {
                    setLog(t('peeperClueLong', { player: t(String(info.data.PlayerName)), is: t('peeperClue-' + info.clue) }));
                }
            } catch { /* no clue for this role */ }
        },
        addLog(message: string) { setLog(message); },
        onWerewolfAttackCast({ actorId, targetId }) {
            setWolfVotes(prev => ({ ...prev, [actorId]: targetId }));
        },
    }), [lobbyCode, myPlayerId, seats, t]);

    useEffect(() => () => { if (timerRef.current) clearInterval(timerRef.current); }, []);

    const isMyTurn = activeTurn?.activePlayerId === myPlayerId;
    const minTargets = activeTurn?.minTargets ?? 0;
    const maxTargets = activeTurn?.maxTargets ?? 1;
    const timeoutMax = activeTurn?.timeoutSeconds ?? TURN_SECONDS;
    const ringPct = 1 - timeLeft / timeoutMax;
    const ringLen = 2 * Math.PI * 22;
    const timeUrgent = timeLeft <= 3;

    function toggleTarget(seatId: string) {
        setSelectedTargets(prev => {
            if (prev.includes(seatId)) return prev.filter(x => x !== seatId);
            if (prev.length >= maxTargets) return prev;
            return [...prev, seatId];
        });
    }

    async function submitAttack() {
        if (!isMyTurn || submitting || selectedTargets.length === 0) return;
        setSubmitting(true);
        try {
            await submitNightAction(lobbyCode, selectedTargets[0], ActionType.Attack);
            setAttackSubmitted(true);
            setWolfVotes(prev => ({ ...prev, [myPlayerId]: selectedTargets[0] }));
            setLog(t('attackSubmitted'));
        } catch (e) { alert(String(e)); }
        finally { setSubmitting(false); }
    }

    async function submitAbility() {
        if (!isMyTurn || submitting || selectedTargets.length < minTargets) return;
        setSubmitting(true);
        try {
            await submitNightAction(lobbyCode, selectedTargets, ActionType.Ability);
            setAbilitySubmitted(true);
            setLog(t('actionSubmitted'));
        } catch (e) { alert(String(e)); }
        finally { setSubmitting(false); }
    }

    async function handleSkip() {
        if (!isMyTurn || submitting) return;
        setSubmitting(true);
        try {
            await skipNightTurn(lobbyCode);
            setLog(t('skipped'));
        } catch (e) { alert(String(e)); }
        finally { setSubmitting(false); }
    }

    const seatStates: TableSeatState[] = seats.map(seat => {
        const isAlive = !deadIds.includes(seat.id);
        const isMe = seat.id === myPlayerId;
        const isForbidden = activeTurn?.forbiddenTargets?.includes(seat.id) ?? false;
        const canAct = isMyTurn && !resolved && !submitting;
        const isViewerWerewolf = myRole.teamId === 1;
        const isSeatWerewolf = otherWerewolvesIds?.includes(seat.id) || (seat.id === myPlayerId && myRole.teamId === 1);
        const isTargetableByAbility = canAct && !isForbidden;
        const isTargetableByAttack = canAct && isViewerWerewolf && isAlive && !isSeatWerewolf && !isMe;
        return {
            seat,
            isMe,
            isActive: seat.id === activeTurn?.activePlayerId && !resolved,
            isAlive,
            isSelected: selectedTargets.includes(seat.id),
            // ability-specific and attack-specific flags
            isTargetableByAbility,
            isTargetableByAttack,
            // keep old single flag for compatibility
            isTargetable: isTargetableByAbility || isTargetableByAttack,
            isWerewolf: otherWerewolvesIds?.includes(seat.id) || (isMe && myRole.teamId === 1),
        };
    });

    const isWerewolf = myRole.teamId === 1;
    const selectedAllAttackable = selectedTargets.length > 0 && selectedTargets.every(id => seatStates.find(s => s.seat.id === id)?.isTargetableByAttack);
    const selectedAllAbility = selectedTargets.length >= minTargets && selectedTargets.every(id => seatStates.find(s => s.seat.id === id)?.isTargetableByAbility);
    const teamClass = myRole.teamId === 1 ? 'turn-banner--wolf'
        : myRole.teamId === 2 ? 'turn-banner--neutral'
        : 'turn-banner--town';

    const wolfArrows: VoteArrow[] = isWerewolf
        ? (Object.entries(wolfVotes) as [string, string][])
            .map(([fromId, toId]) => ({ fromId, toId, kind: 'attack' as const }))
        : [];

    return (
        <div className="phase-stack">
            <RoleInfo role={myRole} config={config} />

            {activeTurn && !resolved && (
                <div className={`turn-banner ${teamClass} ${isMyTurn ? 'turn-banner--me' : ''}`}>
                    <div>
                        <div className="turn-banner__title">
                            {isMyTurn
                                ? t('yourTurnBanner')
                                : <>{t('waitingFor')} <b style={{ color: 'var(--accent-2)' }}>
                                    {seats.find(s => s.id === activeTurn.activePlayerId)?.nickname ?? '?'}
                                </b>…</>}
                        </div>
                        <div className="turn-banner__hint">
                            {isMyTurn && (activeTurn.hasAbility
                                ? (minTargets > 1 ? t('pickTargetsConfirmOrSkip') : t('pickTargetConfirmOrSkip'))
                                : t('noAbilitySkip'))}
                        </div>
                    </div>
                    <div className="ring" aria-label={`${timeLeft}s`}>
                        <svg width={52} height={52} viewBox="0 0 52 52">
                            <circle cx={26} cy={26} r={22} fill="none"
                                stroke="rgba(255,255,255,0.08)" strokeWidth={4} />
                            <circle cx={26} cy={26} r={22} fill="none"
                                stroke={timeUrgent ? 'var(--danger-2)' : 'var(--team-color)'}
                                strokeWidth={4} strokeLinecap="round"
                                strokeDasharray={ringLen}
                                strokeDashoffset={ringLen * ringPct}
                                style={{ transition: 'stroke-dashoffset 0.9s linear, stroke 0.3s' }} />
                        </svg>
                        <span className="ring__num" style={{ color: timeUrgent ? 'var(--danger-2)' : 'var(--text)' }}>
                            {timeLeft}
                        </span>
                    </div>
                </div>
            )}

            <RoundTable
                seats={seats}
                states={seatStates}
                voteArrows={wolfArrows}
                centreLabel={t('night')}
                centreIcon="🌙"
                onSeatClick={s => toggleTarget(s.id)}
            />

            {isMyTurn && !resolved && (
                <div className="phase-actions">
                    {isWerewolf && (
                        <button className="btn btn--danger"
                            disabled={attackSubmitted || !selectedAllAttackable || submitting}
                            onClick={submitAttack}>
                            {t('attack')}
                        </button>
                    )}
                    {activeTurn!.hasAbility && (
                        <button className="btn btn--primary"
                            disabled={abilitySubmitted || !selectedAllAbility || submitting}
                            onClick={submitAbility}>
                            {t('useAbility')}
                        </button>
                    )}
                    <button className="btn" disabled={submitting} onClick={handleSkip}>
                        {t('skip')}
                    </button>
                </div>
            )}

            {resolved && (
                <div className="resolution">
                    <div className="resolution__title">🌅 {t('dawn')}</div>
                    {resolved.killed.length > 0
                        ? resolved.killed.map(n => (
                            <div key={n} className="resolution__killed">☠ {t('wasKilled', { name: n })}</div>
                        ))
                        : <div className="resolution__saved">🕊 {t('everyoneSurvived')}</div>
                    }
                    {resolved.attackBlocked && (
                        <div style={{ color: 'var(--town-2)', marginTop: 4 }}>🛡 {t('attackBlocked')}</div>
                    )}
                    {nightInfo?.type === 'Detective' && (
                        <div className="resolution__clue">
                            🔍 {t('detectiveClueShort')} <b>{nightInfo.clue}</b> {t('is')}{' '}
                            <b style={{ color: 'var(--town-2)' }}>{t('innocent')}</b>
                        </div>
                    )}
                    {nightInfo?.type === 'DogTamer' && (
                        <div className="resolution__clue">
                            🐕 {t('dogClueShort')} {nightInfo.clue ? t('dogBarked') : t('dogQuiet')}.
                        </div>
                    )}
                    {nightInfo?.type === 'Seer' && (
                        <div className="resolution__clue">
                            🔮 {t('seerClueShort')} {String(nightInfo.data.SubjectAName)} &amp;{' '}
                            {String(nightInfo.data.SubjectBName)} —{' '}
                            {nightInfo.clue ? t('sameTeam') : t('differentTeams')}
                        </div>
                    )}
                    {nightInfo?.type === 'Trailer' && (
                        <div className="resolution__clue">
                            🐾 {t('trailerClueLong', { dir: t(nightInfo.clue) })}.
                        </div>
                    )}
                    {nightInfo?.type === 'Peeper' && (
                        <div className="resolution__clue">
                            👀 {t('peeperClueLong', { player: t(nightInfo.data.SubjectName), is: t('peeperClue-' + nightInfo.clue) })}.
                        </div>
                    )}
                </div>
            )}

            <EventLog />
        </div>
    );
});

export default NightPhase;
