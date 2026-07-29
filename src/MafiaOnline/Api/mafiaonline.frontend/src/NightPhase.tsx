import React, { useEffect, useImperativeHandle, useRef, useState, forwardRef } from 'react';
import type { SeatInfo, RoleResponse, NightTurnStarted, NightResolvedPayload, AnyClueResponse, GameConfig } from './api';
import { submitNightAction, skipNightTurn, getPlayerNightInfo } from './api';
import { useTranslation } from 'react-i18next';
import RoundTable, { type TableSeatState, type VoteArrow } from './RoundTable';
import RoleInfo from './RoleInfo';

interface Props {
    lobbyCode: string;
    seats: SeatInfo[];
    myPlayerId: string;
    myRole: RoleResponse;
    deadIds?: string[];
    config?: GameConfig | null;
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
    { lobbyCode, seats, myPlayerId, myRole, deadIds = [], config }, ref
) {
    const { t } = useTranslation();
    const [activeTurn, setActiveTurn] = useState<NightTurnStarted | null>(null);
    const [timeLeft, setTimeLeft] = useState(TURN_SECONDS);
    const [resolved, setResolved] = useState<NightResolvedPayload | null>(null);
    const [nightInfo, setNightInfo] = useState<AnyClueResponse | null>(null);
    const [selectedTargets, setSelectedTargets] = useState<string[]>([]);
    const [submitting, setSubmitting] = useState(false);
    const [log, setLog] = useState<string[]>([t('nightFalls')]);
    // actorId -> targetId, only ever populated for werewolf players
    const [wolfVotes, setWolfVotes] = useState<Record<string, string>>({});
    const timerRef = useRef<ReturnType<typeof setInterval> | null>(null);

    // track whether we already submitted attack/ability for this turn
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
            setLog(l => [...l, isMe ? t('yourTurn') : t('playerTurn', { nick: who?.nickname ?? '?' })]);
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
                setLog(l => [...l, t('killed', { list: payload.killed.join(', ') })]);
            else if (payload.attackBlocked)
                setLog(l => [...l, t('attackBlocked')]);
            else
                setLog(l => [...l, t('quietNight')]);
            try {
                const info = await getPlayerNightInfo(lobbyCode, myPlayerId);
                setNightInfo(info);
                if (!info) return;
                if (info.type === 'Detective') {
                    setLog(l => [...l, t('detectiveClueLong', {
                        player: info.data.SubjectNickname,
                        role: info.data.IsWerewolf ? t('werewolf') : t('innocent'),
                    })]);
                } else if (info.type === 'DogTamer') {
                    setLog(l => [...l, t('dogClue', {
                        barked: info.data.DidBark ? t('dogBarked') : t('dogQuiet'),
                    })]);
                } else if (info.type === 'Seer') {
                    setLog(l => [...l, t('seerClueLong', {
                        a: String(info.data.SubjectAName),
                        b: String(info.data.SubjectBName),
                        relation: info.clue ? t('sameTeam') : t('differentTeams'),
                    })]);
                }
            } catch { /* no clue for this role */ }
        },
        addLog(message: string) {
            setLog(l => [...l, message]);
        },
        onWerewolfAttackCast({ actorId, targetId }) {
            setWolfVotes(prev => ({ ...prev, [actorId]: targetId }));
        },
    }), [lobbyCode, myPlayerId, seats, t]);

    useEffect(() => () => { if (timerRef.current) clearInterval(timerRef.current); }, []);

    const isMyTurn = activeTurn?.activePlayerId === myPlayerId;
    const minTargets = activeTurn?.minTargets ?? 0;
    const maxTargets = activeTurn?.maxTargets ?? 1;
    const canAct = isMyTurn && !!activeTurn?.hasAbility && !submitting;
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
        if (!isMyTurn || submitting) return;
        if (selectedTargets.length === 0) return;
        setSubmitting(true);
        try {
            await submitNightAction(lobbyCode, myPlayerId, selectedTargets[0], ActionType.Attack);
            setAttackSubmitted(true);
            setWolfVotes(prev => ({ ...prev, [myPlayerId]: selectedTargets[0] }));
            setLog(l => [...l, t('attackSubmitted')]);
        } catch (e) { alert(String(e)); }
        finally { setSubmitting(false); }
    }

    async function submitAbility() {
        if (!isMyTurn || submitting) return;
        if (selectedTargets.length < minTargets) return;
        setSubmitting(true);
        try {
            await submitNightAction(lobbyCode, myPlayerId, selectedTargets, ActionType.Ability);
            setAbilitySubmitted(true);
            setLog(l => [...l, t('abilitySubmitted')]);
        } catch (e) { alert(String(e)); }
        finally { setSubmitting(false); }
    }

    async function handleSkip() {
        if (!isMyTurn || submitting) return;
        setSubmitting(true);
        try {
            await skipNightTurn(lobbyCode, myPlayerId);
            setLog(l => [...l, t('skipped')]);
        } catch (e) { alert(String(e)); }
        finally { setSubmitting(false); }
    }

    const seatStates: TableSeatState[] = seats.map(seat => {
        const isAlive = !deadIds.includes(seat.id);
        return {
            seat,
            isMe: seat.id === myPlayerId,
            isActive: seat.id === activeTurn?.activePlayerId && !resolved,
            isAlive,
            isSelected: selectedTargets.includes(seat.id),
            isTargetable: isMyTurn && !resolved && isAlive && seat.id !== myPlayerId && !submitting,
        };
    });

    const isWerewolf = myRole.teamId === 1;
    const teamClass = myRole.teamId === 1 ? 'turn-banner--wolf'
        : myRole.teamId === 2 ? 'turn-banner--neutral'
            : 'turn-banner--town';

    // Werewolves see each other's kill votes as blood-red arrows.
    const wolfArrows: VoteArrow[] = isWerewolf
        ? (Object.entries(wolfVotes) as [string, string][])
            .map(([fromId, toId]) => ({ fromId, toId, kind: 'attack' as const }))
        : [];

    return (
        <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 18 }}>
            {/* Role badge — expandable long description */}
            <RoleInfo role={myRole} config={config} />

            {/* Turn banner */}
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

            {/* Action controls */}
            {isMyTurn && !resolved && (
                <div style={{ display: 'flex', gap: 12 }}>
                    {isWerewolf && (
                        <button className="btn btn--danger"
                            disabled={attackSubmitted || selectedTargets.length === 0 || submitting}
                            onClick={submitAttack}>
                            {t('attack')}
                        </button>
                    )}

                    {activeTurn!.hasAbility && (
                        <button className="btn btn--primary"
                            disabled={abilitySubmitted || selectedTargets.length < minTargets || submitting}
                            onClick={submitAbility}>
                            {t('useAbility')}
                        </button>
                    )}

                    <button className="btn" disabled={submitting} onClick={handleSkip}>
                        {t('skip')}
                    </button>
                </div>
            )}

            {/* Resolution */}
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
                            <b style={{ color: 'var(--town-2)' }}>
                                {t('innocent')}
                            </b>
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
                </div>
            )}

            {/* Event log */}
            <div className="event-log">
                {log.slice(-6).map((entry, i) => (
                    <div key={i} className="event-log__line">— {entry}</div>
                ))}
            </div>
        </div>
    );
});

export default NightPhase;
