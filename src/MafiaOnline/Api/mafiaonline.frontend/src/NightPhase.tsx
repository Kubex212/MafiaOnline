import React, { useEffect, useImperativeHandle, useRef, useState, forwardRef } from 'react';
import type { SeatInfo, RoleResponse, NightTurnStarted, NightResolvedPayload, PlayerNightInfoResponse } from './api';
import { submitNightAction, skipNightTurn, getPlayerNightInfo } from './api';
import { useTranslation } from 'react-i18next';

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

const ROLE_COLORS: Record<string, { bg: string; border: string }> = {
    Werewolf: { bg: '#5c1a1a', border: '#d9534f' },
    Guardian: { bg: '#1a3a5c', border: '#5bc0de' },
    Detective: { bg: '#1a3a2a', border: '#5cb85c' },
    Villager: { bg: '#2a2a2a', border: '#888' },
};

const TEAM_COLORS: Record<string, string> = {
    Werewolf: '#d9534f',
    Town: '#5cb85c',
};

const NightPhase = forwardRef<NightPhaseHandle, Props>(function NightPhase(
    { lobbyCode, seats, myPlayerId, myRole }, ref
) {
    const { t } = useTranslation();
    const [activeTurn, setActiveTurn] = useState<NightTurnStarted | null>(null);
    const [timeLeft, setTimeLeft] = useState(TURN_SECONDS);
    const [resolved, setResolved] = useState<NightResolvedPayload | null>(null);
    const [nightInfo, setNightInfo] = useState<PlayerNightInfoResponse | null>(null);
    const [selectedTarget, setSelectedTarget] = useState<string | null>(null);
    const [submitting, setSubmitting] = useState(false);
    const [log, setLog] = useState<string[]>([t('nightFalls')]);
    const timerRef = useRef<ReturnType<typeof setInterval> | null>(null);

    useImperativeHandle(ref, () => ({
        onTurnStarted(turn: NightTurnStarted) {
            setActiveTurn(turn);
            setTimeLeft(TURN_SECONDS);
            setSelectedTarget(null);
            setSubmitting(false);
            const who = seats.find(s => s.id === turn.activePlayerId);
            const isMe = turn.activePlayerId === myPlayerId;
            setLog(l => [...l, isMe ? t('yourTurn') : t('playerTurn', { nick: who?.nickname ?? '?' })]);
            if (timerRef.current) clearInterval(timerRef.current);
            timerRef.current = setInterval(() => {
                setTimeLeft(t => {
                    if (t <= 1) { clearInterval(timerRef.current!); return 0; }
                    return t - 1;
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

                if (info?.type === "DetectiveClue") {
                    setLog(l => [...l, t('detectiveClueLong', { player: info.data.SubjectNickname, role: info.data.IsWerewolf ? t('werewolf') : t('innocent') })]);
                }
                else if (info?.type === "DogTamerClue") {
                    setLog(l => [...l, t('dogClue', { barked: info.data.DidBark ? t('dogBarked') : t('dogQuiet') })]);
                }
            } catch { /* no night info */ }
        },
    }), [lobbyCode, myPlayerId, seats, t]);

    useEffect(() => () => { if (timerRef.current) clearInterval(timerRef.current); }, []);

    const isMyTurn = activeTurn?.activePlayerId === myPlayerId;
    const canAct = isMyTurn && !!activeTurn?.hasAbility && !submitting;

    async function handleAction() {
        if (!selectedTarget || !isMyTurn) return;
        setSubmitting(true);
        try {
            await submitNightAction(lobbyCode, myPlayerId, selectedTarget);
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

    const cx = 250, cy = 250, r = 170;

    return (
        <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 16 }}>

            {/* Role badge */}
            <div style={{
                padding: '8px 28px', borderRadius: 10,
                border: `2px solid ${TEAM_COLORS[myRole.team] ?? '#aaa'}`,
                background: '#1a1a2e', textAlign: 'center',
            }}>
                <span style={{ fontSize: 12, color: '#aaa', textTransform: 'uppercase', letterSpacing: 2 }}>{t('yourRole')}</span>
                <span style={{ fontSize: 20, fontWeight: 700, color: TEAM_COLORS[myRole.team] ?? '#fff' }}>{myRole.name}</span>
            </div>

            {/* Active turn banner */}
            {activeTurn && !resolved && (
                <div style={{
                    width: '100%', maxWidth: 460, borderRadius: 10, padding: '10px 20px',
                    background: isMyTurn ? '#1a2e1a' : '#1a1a2e',
                    border: `2px solid ${isMyTurn ? '#5cb85c' : '#555'}`,
                    display: 'flex', alignItems: 'center', justifyContent: 'space-between',
                }}>
                    <div>
                        {isMyTurn
                            ? <span style={{ color: '#5cb85c', fontWeight: 700 }}>{t('yourTurnBanner')}</span>
                            : <span style={{ color: '#aaa' }}>
                                {t('waitingFor')} <b style={{ color: '#f0c040' }}>
                                    {seats.find(s => s.id === activeTurn.activePlayerId)?.nickname ?? '?'}
                                </b>…
                            </span>
                        }
                        <div style={{ fontSize: 12, color: '#aaa', marginTop: 2 }}>
                            {isMyTurn && (activeTurn.hasAbility
                                ? t('pickTargetConfirmOrSkip')
                                : t('noAbilitySkip'))}
                        </div>
                    </div>
                    {/* Countdown ring */}
                    <div style={{ position: 'relative', width: 44, height: 44, flexShrink: 0 }}>
                        <svg viewBox="0 0 44 44" style={{ transform: 'rotate(-90deg)' }}>
                            <circle cx={22} cy={22} r={18} fill="none" stroke="#333" strokeWidth={4} />
                            <circle cx={22} cy={22} r={18} fill="none"
                                stroke={timeLeft <= 3 ? '#d9534f' : '#5cb85c'} strokeWidth={4}
                                strokeDasharray={`${2 * Math.PI * 18}`}
                                strokeDashoffset={`${2 * Math.PI * 18 * (1 - timeLeft / TURN_SECONDS)}`}
                                style={{ transition: 'stroke-dashoffset 0.9s linear, stroke 0.3s' }}
                            />
                        </svg>
                        <span style={{
                            position: 'absolute', inset: 0, display: 'flex',
                            alignItems: 'center', justifyContent: 'center',
                            fontSize: 13, fontWeight: 700,
                            color: timeLeft <= 3 ? '#d9534f' : '#ccc',
                        }}>{timeLeft}</span>
                    </div>
                </div>
            )}

            {/* Round table */}
            <svg viewBox="0 0 500 500" style={{ width: '100%', maxWidth: 460, userSelect: 'none' }}>
                <circle cx={cx} cy={cy} r={100} fill="#2c1a0e" stroke="#7a4a1e" strokeWidth={6} />
                <text x={cx} y={cy + 6} textAnchor="middle" fill="#7a4a1e" fontSize={14} fontWeight={600}>🌙 {t('night')}</text>

                {seats.map((seat, i) => {
                    const angle = (2 * Math.PI * i) / seats.length - Math.PI / 2;
                    const px = cx + r * Math.cos(angle);
                    const py = cy + r * Math.sin(angle);
                    const isMe = seat.id === myPlayerId;
                    const isActive = seat.id === activeTurn?.activePlayerId && !resolved;
                    const isSelected = seat.id === selectedTarget;
                    const isTargetable = canAct && !isMe && !resolved;
                    const colors = isMe ? (ROLE_COLORS[myRole.name] ?? ROLE_COLORS['Villager']) : { bg: '#1e1e2e', border: '#555' };
                    const borderColor = isSelected ? '#f0c040' : isActive ? '#fff' : colors.border;
                    const label = seat.nickname.length > 10 ? seat.nickname.slice(0, 9) + '…' : seat.nickname;

                    return (
                        <g key={seat.id} transform={`translate(${px},${py})`}
                            style={{ cursor: isTargetable ? 'pointer' : 'default' }}
                            onClick={() => { if (isTargetable) setSelectedTarget(seat.id); }}
                        >
                            {isActive && <circle r={40} fill="none" stroke="#fff" strokeWidth={2} opacity={0.2} />}
                            <circle r={34} fill={colors.bg} stroke={borderColor} strokeWidth={isMe || isSelected || isActive ? 3 : 2} />
                            <circle cx={22} cy={-22} r={10} fill="#333" stroke={borderColor} strokeWidth={1} />
                            <text x={22} y={-18} textAnchor="middle" fill="#ccc" fontSize={10} fontWeight={600}>{seat.seatIndex + 1}</text>
                            <text y={4} textAnchor="middle"
                                fill={isMe ? '#f0c040' : isActive ? '#fff' : '#ddd'}
                                fontSize={isMe ? 11 : 10} fontWeight={isMe || isActive ? 700 : 400}>{label}</text>
                            {isMe && <text y={17} textAnchor="middle" fill="#f0c040" fontSize={9} fontStyle="italic">({t('you')})</text>}
                            {isSelected && <text y={17} textAnchor="middle" fill="#f0c040" fontSize={9}>{t('target')}</text>}
                        </g>
                    );
                })}
            </svg>

            {/* Action controls */}
            {isMyTurn && !resolved && (
                <div style={{ display: 'flex', gap: 12 }}>
                    {activeTurn!.hasAbility && (
                        <button disabled={!selectedTarget || submitting} onClick={handleAction}
                            style={{ ...btnStyle, background: selectedTarget ? '#c0392b' : '#555', opacity: (!selectedTarget || submitting) ? 0.6 : 1 }}>
                            {myRole.name === 'Werewolf' ? t('attack') : myRole.name === 'Guardian' ? t('protect') : t('useAbility')}
                        </button>
                    )}
                    <button disabled={submitting} onClick={handleSkip}
                        style={{ ...btnStyle, background: '#444', opacity: submitting ? 0.6 : 1 }}>
                        {t('skip')}
                    </button>
                </div>
            )}

            {/* Resolution panel */}
            {resolved && (
                <div style={{ maxWidth: 460, width: '100%', borderRadius: 10, padding: '14px 20px', background: '#1a1a2e', border: '2px solid #333' }}>
                    <div style={{ fontWeight: 700, color: '#f0c040', marginBottom: 8 }}>{t('dawn')}</div>
                    {resolved.killed.length > 0
                        ? resolved.killed.map(n => <div key={n} style={{ color: '#d9534f' }}>{t('wasKilled', { name: n })}</div>)
                        : <div style={{ color: '#5cb85c' }}>{t('everyoneSurvived')}</div>
                    }
                    {resolved.attackBlocked && (
                        <div style={{ color: '#5bc0de', marginTop: 4 }}>{t('attackBlocked')}</div>
                    )}
                    {nightInfo?.type === "DetectiveClue" && (
                        <div style={{ marginTop: 8, color: '#aaa', borderTop: '1px solid #2a2a3e', paddingTop: 8 }}>
                            {t('detectiveClueShort')} <b style={{ color: '#ddd' }}>{String(nightInfo.data.SubjectNickname)}</b> {t('is')} <b style={{ color: nightInfo.data.IsWerewolf ? '#d9534f' : '#5cb85c' }}>{nightInfo.data.IsWerewolf ? t('werewolf') : t('innocent')}</b>
                        </div>
                    )}

                    {nightInfo?.type === "DogTamerClue" && (
                        <div style={{ marginTop: 8, color: '#aaa', borderTop: '1px solid #2a2a3e', paddingTop: 8 }}>
                            {t('dogClueShort')} {nightInfo.data.DidBark ? t('dogBarked') : t('dogQuiet')}.
                        </div>
                    )}
                </div>
            )}

            {/* Event log */}
            <div style={{ maxWidth: 460, width: '100%', borderRadius: 8, background: '#111', padding: '8px 14px' }}>
                {log.slice(-6).map((entry, i) => (
                    <div key={i} style={{ fontSize: 12, color: '#888', lineHeight: 1.6 }}>{entry}</div>
                ))}
            </div>
        </div>
    );
});

export default NightPhase;

const btnStyle: React.CSSProperties = {
    border: 'none', borderRadius: 8, padding: '9px 22px',
    cursor: 'pointer', fontSize: 14, fontWeight: 700, color: '#fff',
};
