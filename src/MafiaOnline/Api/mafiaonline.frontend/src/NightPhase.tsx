import React, { useEffect, useImperativeHandle, useRef, useState, forwardRef } from 'react';
import type { SeatInfo, RoleResponse, NightTurnStarted, NightResolvedPayload, PlayerNightInfoResponse } from './api';
import { submitNightAction, skipNightTurn, getPlayerNightInfo } from './api';
import { useTranslation } from 'react-i18next';
import RoundTable, { type TableSeatState } from './RoundTable';

interface Props {
    lobbyCode: string;
    seats: SeatInfo[];
    myPlayerId: string;
    myRole: RoleResponse;
}

export interface NightPhaseHandle {
    onTurnStarted(turn: NightTurnStarted): void;
    onNightResolved(payload: NightResolvedPayload): void;
}

const TURN_SECONDS = 1000;

const NightPhase = forwardRef<NightPhaseHandle, Props>(function NightPhase(
    { lobbyCode, seats, myPlayerId, myRole }, ref
) {
    const { t } = useTranslation();
    const [activeTurn, setActiveTurn] = useState<NightTurnStarted | null>(null);
    const [timeLeft, setTimeLeft] = useState(TURN_SECONDS);
    const [resolved, setResolved] = useState<NightResolvedPayload | null>(null);
    const [nightInfo, setNightInfo] = useState<PlayerNightInfoResponse | null>(null);
    const [selectedTargets, setSelectedTargets] = useState<string[]>([]);
    const [submitting, setSubmitting] = useState(false);
    const [log, setLog] = useState<string[]>([t('nightFalls')]);
    const timerRef = useRef<ReturnType<typeof setInterval> | null>(null);

    useImperativeHandle(ref, () => ({
        onTurnStarted(turn: NightTurnStarted) {
            setActiveTurn(turn);
            setTimeLeft(turn.timeoutSeconds ?? TURN_SECONDS);
            setSelectedTargets([]);
            setSubmitting(false);
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
            if (payload.killed.length > 0)
                setLog(l => [...l, t('killed', { list: payload.killed.join(', ') })]);
            else if (payload.attackBlocked)
                setLog(l => [...l, t('attackBlocked')]);
            else
                setLog(l => [...l, t('quietNight')]);
            try {
                const info = await getPlayerNightInfo(lobbyCode, myPlayerId);
                setNightInfo(info);
                if (info?.type === 'DetectiveClue') {
                    setLog(l => [...l, t('detectiveClueLong', {
                        player: info.data.SubjectNickname,
                        role: info.data.IsWerewolf ? t('werewolf') : t('innocent'),
                    })]);
                } else if (info?.type === 'DogTamerClue') {
                    setLog(l => [...l, t('dogClue', {
                        barked: info.data.DidBark ? t('dogBarked') : t('dogQuiet'),
                    })]);
                } else if (info?.type === 'SeerClue') {
                    const same = String(info.data.SameTeam) === 'true';
                    setLog(l => [...l, t('seerClueLong', {
                        a: String(info.data.SubjectAName),
                        b: String(info.data.SubjectBName),
                        relation: same ? t('sameTeam') : t('differentTeams'),
                    })]);
                }
            } catch { /* no clue for this role */ }
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

    async function handleAction() {
        if (selectedTargets.length < minTargets || !isMyTurn) return;
        setSubmitting(true);
        try {
            await submitNightAction(lobbyCode, myPlayerId, selectedTargets);
            setLog(l => [...l, t('actionSubmitted')]);
        } catch (e) { alert(String(e)); setSubmitting(false); }
    }

    async function handleSkip() {
        if (!isMyTurn || submitting) return;
        setSubmitting(true);
        try {
            await skipNightTurn(lobbyCode, myPlayerId);
            setLog(l => [...l, t('skipped')]);
        } catch (e) { alert(String(e)); setSubmitting(false); }
    }

    const seatStates: TableSeatState[] = seats.map(seat => ({
        seat,
        isMe: seat.id === myPlayerId,
        isActive: seat.id === activeTurn?.activePlayerId && !resolved,
        isAlive: true,
        isSelected: selectedTargets.includes(seat.id),
        isTargetable: canAct && seat.id !== myPlayerId && !resolved,
    }));

    const actionLabel = myRole.name === 'Werewolf' ? t('attack')
        : myRole.name === 'Guardian' ? t('protect')
        : t('useAbility');

    return (
        <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 18 }}>
            {/* Role badge */}
            <div className="role-badge" style={{ borderColor: `var(--${myRole.name === 'Werewolf' ? 'wolf' : 'town-2'})` }}>
                <span className="role-badge__label">{t('yourRole')}</span>
                <span className="role-badge__name"
                    style={{ color: `var(--${myRole.name === 'Werewolf' ? 'wolf' : 'town-2'})` }}>
                    {t(myRole.name)}
                </span>
            </div>

            {/* Turn banner */}
            {activeTurn && !resolved && (
                <div className={`turn-banner ${isMyTurn ? 'turn-banner--me' : ''}`}>
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
                                stroke={timeUrgent ? 'var(--danger-2)' : 'var(--town-2)'}
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
                centreLabel={t('night')}
                centreIcon="🌙"
                onSeatClick={s => toggleTarget(s.id)}
            />

            {/* Action controls */}
            {isMyTurn && !resolved && (
                <div style={{ display: 'flex', gap: 12 }}>
                    {activeTurn!.hasAbility && (
                        <button className="btn btn--danger"
                            disabled={selectedTargets.length < minTargets || submitting}
                            onClick={handleAction}>
                            {actionLabel}
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
                    {nightInfo?.type === 'DetectiveClue' && (
                        <div className="resolution__clue">
                            🔍 {t('detectiveClueShort')} <b>{String(nightInfo.data.SubjectNickname)}</b> {t('is')}{' '}
                            <b style={{ color: nightInfo.data.IsWerewolf ? 'var(--danger-2)' : 'var(--town-2)' }}>
                                {nightInfo.data.IsWerewolf ? t('werewolf') : t('innocent')}
                            </b>
                        </div>
                    )}
                    {nightInfo?.type === 'DogTamerClue' && (
                        <div className="resolution__clue">
                            🐕 {t('dogClueShort')} {nightInfo.data.DidBark ? t('dogBarked') : t('dogQuiet')}.
                        </div>
                    )}
                    {nightInfo?.type === 'SeerClue' && (
                        <div className="resolution__clue">
                            🔮 {t('seerClueShort')} {String(nightInfo.data.SubjectAName)} &amp;{' '}
                            {String(nightInfo.data.SubjectBName)} —{' '}
                            {String(nightInfo.data.SameTeam) === 'true' ? t('sameTeam') : t('differentTeams')}
                        </div>
                    )}
                    {nightInfo?.type === 'TrailerClue' && (
                        <div className="resolution__clue">
                            🐾 {t('trailerClueLong', { dir: t(nightInfo.data.Direction) })}.
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
