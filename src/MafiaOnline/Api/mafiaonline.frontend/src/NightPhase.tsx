import React, { useEffect, useImperativeHandle, useRef, useState, forwardRef } from 'react';
import type { SeatInfo, RoleResponse, NightStartedPayload, NightResolvedPayload, AnyClueResponse, GameConfig, TurnFinishedPayload } from './api';
import { submitNightAction, skipNightTurn, getPlayerNightInfo } from './api';
import { useTranslation } from 'react-i18next';
import RoundTable, { type TableSeatState, type VoteArrow } from './RoundTable';
import RoleInfo from './RoleInfo';
import EventLog from './EventLog';
import { addLog as pushLog } from './gameLog';
import type { ActionInfo } from './RoundTable';
import type { TFunction } from 'i18next';
import { roleDescKey, roleDescParams, roleSigil } from './roleText';
import { useUiFeedback } from './UiFeedback';

interface Props {
    lobbyCode: string;
    seats: SeatInfo[];
    myPlayerId: string;
    myRole: RoleResponse;
    deadIds?: string[];
    config?: GameConfig | null;
    otherWerewolvesIds?: string[];
    showRoles?: boolean;
}

export interface NightPhaseHandle {
    onNightStarted(payload: NightStartedPayload): void;
    onActionSubmitted(payload: TurnFinishedPayload): void;
    onNightResolved(payload: NightResolvedPayload): void;
    addLog(message: string): void;
    onWerewolfAttackCast(vote: { actorId: string; targetId: string }): void;
    // Optional: recover concrete submitted actions after reconnect
    onRecoveredSubmittedActions?(payload: { actions: Array<{ actorId: string; actionType: number; targets: string[] }>; finished: string[] }): void;
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

const NIGHT_SECONDS = 1000;

const NightPhase = forwardRef<NightPhaseHandle, Props>(function NightPhase(
    { lobbyCode, seats, myPlayerId, myRole, deadIds = [], config, otherWerewolvesIds = [], showRoles = false }, ref
) {
    const { t } = useTranslation();
    const { toast } = useUiFeedback();
    const [nightPayload, setNightPayload] = useState<NightStartedPayload | null>(null);
    const [timeLeft, setTimeLeft] = useState(NIGHT_SECONDS);
    const [resolved, setResolved] = useState<NightResolvedPayload | null>(null);
    const [nightInfo, setNightInfo] = useState<AnyClueResponse | null>(null);
    const [selectedTargets, setSelectedTargets] = useState<string[]>([]);
    const [submitting, setSubmitting] = useState(false);
    const [wolfVotes, setWolfVotes] = useState<Record<string, string>>({});
    const [donePlayerIds, setDonePlayerIds] = useState<Set<string>>(new Set());
    const [mySubmittedActions, setMySubmittedActions] = useState<Set<string>>(new Set());
    const recoveredPartialRef = useRef<Set<string>>(new Set());
    const recoveredFinishedRef = useRef<Set<string>>(new Set());
    const timerRef = useRef<ReturnType<typeof setInterval> | null>(null);
    const setLog = (msg: string) => pushLog(msg, 'night');
    const [myAction, setMyAction] = useState<ActionInfo | null>(null);

    useImperativeHandle(ref, () => ({
        onNightStarted(payload: NightStartedPayload) {
            setNightPayload(payload);
            setTimeLeft(payload.durationSeconds ?? NIGHT_SECONDS);
            setSelectedTargets([]);
            setSubmitting(false);
            setDonePlayerIds(new Set());
            setMySubmittedActions(new Set());
            recoveredPartialRef.current = new Set();
            recoveredFinishedRef.current = new Set();
            setLog(t('nightStarted'));
            if (timerRef.current) clearInterval(timerRef.current);
            timerRef.current = setInterval(() => {
                setTimeLeft(prev => {
                    if (prev <= 1) { clearInterval(timerRef.current!); return 0; }
                    return prev - 1;
                });
            }, 1000);
        },
        onActionSubmitted(payload: TurnFinishedPayload) {
            // Server's TurnFinished is authoritative: mark actor done and log it.
            setDonePlayerIds(prev => new Set([...prev, payload.actorId]));
            setLog(t('playerSubmittedAction', { name: payload.actorName }));
        },
        async onNightResolved(payload: NightResolvedPayload) {
            if (timerRef.current) clearInterval(timerRef.current);
            setNightPayload(null);
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
                        relation: info.clue ? t('sameTeam') : t('differenceTeams'),
                    }));
                } else if (info.type === 'Trailer') {
                    setLog(t('trailerClueLong', { dir: t(info.clue) }));
                } else if (info.type === 'Peeper') {
                    setLog(t('peeperClueLong', { player: t(String(info.data.SubjectName)), is: t('peeperClue-' + info.clue) }));
                } else if (info.type === 'Spy') {
                    setLog(t('spyClueLong', { player: t(String(info.data.SubjectName)), did: t('spyClue-' + info.clue) }));
                } else if (info.type === 'Ranger') {
                    setLog(t('rangerClueLong', { times: info.clue }));
                } else if (info.type === 'Alchemist') {
                    setLog(displayAlchemistClue(info.clue, t));
                } else if (info.type === 'Sorcerer') {
                    setLog(t('sorcererClueLong', { count: info.clue }));
                } else if (info.type === 'Innkeeper') {
                    setLog(t('innkeeperClueLong', { list: info.clue.join(', ') }));
                }
            } catch { /* no clue for this role */ }
        },
        addLog(message: string) { setLog(message); },
        onWerewolfAttackCast({ actorId, targetId }) {
            setWolfVotes(prev => ({ ...prev, [actorId]: targetId }));
        },
        onRecoveredSubmittedActions(payload?: { actions: Array<{ actorId: string; actionType: number; targets: string[] }>; finished: string[] }) {
            if (!payload) return;
            const actions = payload.actions ?? [];
            const finished = new Set(payload.finished ?? []);

            // Apply actions: set wolf votes and my submitted action state, but DO NOT mark players done here
            actions.forEach(a => {
                if (a.actionType === ActionType.Attack && a.targets && a.targets.length > 0) {
                    setWolfVotes(prev => ({ ...prev, [a.actorId]: a.targets[0] }));
                }
                if (a.actorId === myPlayerId) {
                    if (a.actionType === ActionType.Attack) {
                        setMySubmittedActions(prev => new Set([...prev, 'Attack']));
                        if (a.targets && a.targets.length > 0) setWolfVotes(prev => ({ ...prev, [a.actorId]: a.targets[0] }));
                    } else if (a.actionType === ActionType.Ability) {
                        setMySubmittedActions(prev => new Set([...prev, 'Ability']));
                        setMyAction({ roleName: myRole.name, targets: a.targets });
                    }
                }
            });

            // Only mark players as done if server indicated they are fully finished this turn
            if (finished.size > 0) {
                setDonePlayerIds(prev => {
                    const next = new Set(prev);
                    finished.forEach(id => next.add(id));
                    return next;
                });
            }
            // Track recovered partial submissions: any actor with actions but not in finished list is partial
            const partial = new Set<string>();
            actions.forEach(a => { if (!finished.has(a.actorId)) partial.add(a.actorId); });
            recoveredPartialRef.current = partial;
            recoveredFinishedRef.current = finished;
         }
    }), [lobbyCode, myPlayerId, seats, t]);

    useEffect(() => () => { if (timerRef.current) clearInterval(timerRef.current); }, []);

    // Get my player's ability info
    const myPlayerInfo = nightPayload?.playerInfos.find(p => p.playerId === myPlayerId);
    const minTargets = myPlayerInfo?.minTargets ?? 0;
    const maxTargets = myPlayerInfo?.maxTargets ?? 1;
    const timeoutMax = nightPayload?.durationSeconds ?? NIGHT_SECONDS;
    const ringPct = 1 - timeLeft / timeoutMax;
    const ringLen = 2 * Math.PI * 22;
    const timeUrgent = timeLeft <= 3;

    function toggleTarget(seatId: string) {
        if (submitting) return; // Don't allow changes after submission
        setSelectedTargets(prev => {
            if (prev.includes(seatId)) return prev.filter(x => x !== seatId);
            if (prev.length >= maxTargets) return prev;
            return [...prev, seatId];
        });
    }

    async function submitAttack() {
        if (submitting || selectedTargets.length === 0) return;
        setSubmitting(true);
        try {
            await submitNightAction(lobbyCode, selectedTargets[0], ActionType.Attack);
            setWolfVotes(prev => ({ ...prev, [myPlayerId]: selectedTargets[0] }));
            setMySubmittedActions(prev => {
                const next = new Set([...prev, 'Attack']);
                return next;
            });
            setLog(t('attackSubmitted'));
            // Clear selection for next action (ability)
            setSelectedTargets([]);
        } catch (e) { toast(String(e), 'error'); }
        finally { setSubmitting(false); }
    }

    async function submitAbility() {
        if (submitting || selectedTargets.length < minTargets) return;
        setSubmitting(true);
        try {
            await submitNightAction(lobbyCode, selectedTargets, ActionType.Ability);
            setMyAction({ roleName: myRole.name, targets: selectedTargets });
            setMySubmittedActions(prev => {
                const next = new Set([...prev, 'Ability']);
                return next;
            });
            setLog(t('actionSubmitted'));
            // Clear selection after ability
            setSelectedTargets([]);
        } catch (e) { toast(String(e), 'error'); }
        finally { setSubmitting(false); }
    }

    async function handleSkip() {
        if (submitting) return;
        setSubmitting(true);
        try {
            await skipNightTurn(lobbyCode);
            setLog(t('skipped'));
        } catch (e) { toast(String(e), 'error'); }
        finally { setSubmitting(false); }
    }

    const seatStates: TableSeatState[] = seats.map(seat => {
        const isAlive = !deadIds.includes(seat.id);
        const isMe = seat.id === myPlayerId;
        const isForbidden = myPlayerInfo?.forbiddenTargets?.includes(seat.id) ?? false;
        const isDone = donePlayerIds.has(seat.id);
        const canAct = !resolved && !submitting;
        const isViewerWerewolf = myRole.teamId === 1;
        const isSeatWerewolf = otherWerewolvesIds?.includes(seat.id) || (seat.id === myPlayerId && myRole.teamId === 1);
        const isTargetableByAbility = canAct && !isForbidden && myPlayerInfo?.hasAbility;
        const isTargetableByAttack = canAct && isViewerWerewolf && isAlive && !isSeatWerewolf && !isMe;
        return {
            seat,
            isMe,
            isActive: false, // No single "active" player in simultaneous mode
            isAlive,
            isSelected: selectedTargets.includes(seat.id),
            isTargetableByAbility,
            isTargetableByAttack,
            isTargetable: isTargetableByAbility || isTargetableByAttack,
            isWerewolf: otherWerewolvesIds?.includes(seat.id) || (isMe && myRole.teamId === 1),
            hasSubmitted: isDone,
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

            {config && showRoles && (
                <div style={{ marginTop: 8, display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(160px, 1fr))', gap: 8 }}>
                    {Array.from(new Set(['Werewolf', 'Villager', ...(config.enabledRoles ?? [])])).map(roleName => (
                        <div key={roleName} className="role-card" style={{ padding: 8, borderRadius: 6 }}>
                            <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                                <div style={{ fontSize: 20 }}>{roleSigil(roleName)}</div>
                                <div style={{ fontWeight: 600 }}>{t(roleName)}</div>
                            </div>
                            <div style={{ marginTop: 6, color: 'var(--text-dim)', fontSize: 13 }}>
                                {t(roleDescKey(roleName, 'Short'), roleDescParams(config))}
                            </div>
                        </div>
                    ))}
                </div>
            )}

            {nightPayload && !resolved && (
                <div className={`turn-banner ${teamClass}`}>
                    <div>
                        <div className="turn-banner__title">
                            {t('chooseYourAction')}
                        </div>
                        <div className="turn-banner__hint">
                            {myPlayerInfo?.hasAbility
                                ? (minTargets > 1 ? t('pickTargetsConfirmOrSkip') : t('pickTargetConfirmOrSkip'))
                                : t('noAbilitySkip')}
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
                myAction={myAction}
            />

            {nightPayload && !resolved && (
                <div className="phase-actions">
                    {isWerewolf && !donePlayerIds.has(myPlayerId) && (
                        <button className="btn btn--danger"
                            disabled={!selectedAllAttackable || submitting || mySubmittedActions.has('Attack')}
                            onClick={submitAttack}>
                            {t('attack')}
                        </button>
                    )}
                    {myPlayerInfo?.hasAbility && !donePlayerIds.has(myPlayerId) && (
                        <button className="btn btn--primary"
                            disabled={!selectedAllAbility || submitting || mySubmittedActions.has('Ability')}
                            onClick={submitAbility}>
                            {t('useAbility')}
                        </button>
                    )}
                    {!donePlayerIds.has(myPlayerId) && (
                        <button className="btn" disabled={submitting} onClick={handleSkip}>
                            {t('skip')}
                        </button>
                    )}
                    {donePlayerIds.has(myPlayerId) && (
                        <div style={{ padding: '10px 20px', color: 'var(--text-dim)', fontSize: '14px', fontStyle: 'italic' }}>
                            ✔ {t('yourActionSubmitted')}
                        </div>
                    )}
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
                        <div style={{ color: 'var(--town-2)', marginTop: 4 }}>⚔️ {t('attackBlocked')}</div>
                    )}
                    {nightInfo?.type === 'Detective' && (
                        <div className="resolution__clue">
                            🔍 {t('detectiveClueShort')} <b>{nightInfo.clue}</b> {t('is')}{' '}
                            <b style={{ color: 'var(--town-2)' }}>{t('innocent')}</b>
                        </div>
                    )}
                    {nightInfo?.type === 'DogTamer' && (
                        <div className="resolution__clue">
                            🐕 {t('dogClueShort')} {nightInfo.clue ? t('dogBarked') : t('dogQuiet')}
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
                            🐾 {t('trailerClueLong', { dir: t(nightInfo.clue) })}
                        </div>
                    )}
                    {nightInfo?.type === 'Peeper' && (
                        <div className="resolution__clue">
                            👀 {t('peeperClueLong', { player: t(nightInfo.data.SubjectName), is: t('peeperClue-' + nightInfo.clue) })}
                        </div>
                    )}
                    {nightInfo?.type === 'Spy' && (
                        <div className="resolution__clue">
                            🕵️‍ {t('spyClueLong', { player: t(nightInfo.data.SubjectName), did: t('spyClue-' + nightInfo.clue) })}
                        </div>
                    )}
                    {nightInfo?.type === 'Ranger' && (
                        <div className="resolution__clue">
                            🧶 {t('rangerClueLong', { times: nightInfo.clue })}
                        </div>
                    )}
                    {nightInfo?.type === 'Alchemist' && (
                        <div className="resolution__clue">
                            🧪 {displayAlchemistClue(nightInfo.clue, t)}
                        </div>
                    )}
                    {nightInfo?.type === 'Sorcerer' && (
                        <div className="resolution__clue">
                            🕯 {t('sorcererClueLong', { count: nightInfo.clue })}
                        </div>
                    )}
                    {nightInfo?.type === 'Innkeeper' && (
                        <div className="resolution__clue">
                            🍺 {t('innkeeperClueLong', { list: nightInfo.clue.join(', ') })}
                        </div>
                    )}
                </div>
            )}

            <EventLog />
        </div>
    );
});

export default NightPhase;

function displayAlchemistClue(clueData: any, t: TFunction): string {
    const resource = clueData.clueBase;

    switch (clueData.nightsPassed) {
        case 1:
            return t(resource, { num: clueData.data });
        case 2:
        case 3:
        case 4:
            return t(resource, { pool: clueData.data.map(s => t(s)).join(", ") });
        default:
            return t(resource, { werewolves: clueData.data.join(", ") });
    }
}
