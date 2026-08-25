import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { roleDescKey, roleSigil, roleLabelKey } from './roleText';
import { roleTypes } from './api';


const PEEK_ROLES = roleTypes;

/**
 * First-visit explainer shown under the join form: what the game is, how a
 * round flows, the roles, and how you win. Everything is collapsible so the
 * whole thing stays out of the way on a phone.
 */
export default function Landing() {
    const { t } = useTranslation();
    const [openRoles, setOpenRoles] = useState(false);

    const steps = [
        { icon: '📨', title: t('step1Title'), body: t('step1Body') },
        { icon: '🎭', title: t('step2Title'), body: t('step2Body') },
        { icon: '🌙', title: t('step3Title'), body: t('step3Body') },
        { icon: '🌞', title: t('step4Title'), body: t('step4Body') },
    ];

    return (
        <section className="landing" aria-label={t('howToPlay')}>
            <p className="landing__lede">{t('landingLede')}</p>

            <ul className="landing__facts">
                <li><strong>4+</strong><span>{t('factPlayers')}</span></li>
                <li><strong>~10 min</strong><span>{t('factLength')}</span></li>
                <li><strong>{PEEK_ROLES.length}</strong><span>{t('factRoles')}</span></li>
            </ul>

            <h2 className="landing__heading">{t('howToPlay')}</h2>
            <ol className="steps">
                {steps.map((s, i) => (
                    <li className="step" key={i}>
                        <span className="step__icon" aria-hidden>{s.icon}</span>
                        <div className="step__text">
                            <h3>{s.title}</h3>
                            <p>{s.body}</p>
                        </div>
                    </li>
                ))}
            </ol>

            <div className="landing__teams">
                <div className="team-card team-card--town">
                    <span className="team-card__sigil" aria-hidden>🌾</span>
                    <h3>{t('Villagers')}</h3>
                    <p>{t('townGoal')}</p>
                </div>
                <div className="team-card team-card--wolf">
                    <span className="team-card__sigil" aria-hidden>🐺</span>
                    <h3>{t('Werewolves')}</h3>
                    <p>{t('wolfGoal')}</p>
                </div>
            </div>

            <details className="landing__details" open={openRoles}
                onToggle={e => setOpenRoles((e.currentTarget as HTMLDetailsElement).open)}>
                <summary>{t('meetTheRoles')}</summary>
                <ul className="role-peek">
                    {PEEK_ROLES.map(r => (
                        <li key={r}>
                            <span className="role-peek__sigil" aria-hidden>{roleSigil(r)}</span>
                            <div>
                                <strong>{t(roleLabelKey(r))}</strong>
                                <span>{t(roleDescKey(r, 'Short'), { numWerewolves: 1, rangeDoubled: 2 })}</span>
                            </div>
                        </li>
                    ))}
                </ul>
            </details>

            <p className="landing__foot">{t('landingFoot')}</p>
        </section>
    );
}
