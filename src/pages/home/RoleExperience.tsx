import { motion, useReducedMotion } from 'framer-motion'
import { useRef, useState, type KeyboardEvent } from 'react'
import { SECTIONS } from '../../app/routes'
import { BotanicalCorner, BotanicalDecoration } from '../../components/brand/Botanical'
import { FoodLoopMark } from '../../components/brand/FoodLoopMark'
import { Reveal } from '../../components/motion/Reveal'
import { SectionEyebrow } from '../../components/ui/SectionEyebrow'
import { DarkPanel } from '../../components/ui/Surface'
import { ease, spring } from '../../lib/motion'
import { ROLES, type RoleId } from './content'

export function RoleExperience() {
  const [index, setIndex] = useState(0)
  const tabs = useRef<(HTMLButtonElement | null)[]>([])
  const reduced = useReducedMotion()
  const role = ROLES[index]

  // WAI-ARIA tabs, automatic activation: arrows / Home / End move selection and focus together.
  function onKeyDown(e: KeyboardEvent<HTMLDivElement>) {
    const last = ROLES.length - 1
    const next =
      e.key === 'ArrowRight' || e.key === 'ArrowDown' ? (index === last ? 0 : index + 1)
      : e.key === 'ArrowLeft' || e.key === 'ArrowUp' ? (index === 0 ? last : index - 1)
      : e.key === 'Home' ? 0
      : e.key === 'End' ? last
      : null
    if (next === null) return
    e.preventDefault()
    setIndex(next)
    tabs.current[next]?.focus()
  }

  const enter = (delay: number) => ({
    initial: reduced ? false : { opacity: 0, y: 14 },
    animate: { opacity: 1, y: 0 },
    transition: { duration: 0.42, ease: ease.out, delay },
  })

  return (
    <section id={SECTIONS.roles} tabIndex={-1} className="roles" aria-labelledby="roles-title">
      <BotanicalDecoration>
        <BotanicalCorner position="bottom-left" className="roles__corner" />
      </BotanicalDecoration>

      <div className="container">
        <Reveal className="roles__head">
          <SectionEyebrow index="04">Role experience</SectionEyebrow>
          <h2 id="roles-title">
            One platform, <em>four</em> ways in.
          </h2>
        </Reveal>

        <Reveal delay={0.08}>
          <div role="tablist" aria-label="FoodLoop roles" className="role-tabs" onKeyDown={onKeyDown}>
            {ROLES.map((r, i) => (
              <button
                key={r.id}
                ref={(el) => {
                  tabs.current[i] = el
                }}
                type="button"
                role="tab"
                id={`role-tab-${r.id}`}
                aria-selected={i === index}
                aria-controls="role-panel"
                tabIndex={i === index ? 0 : -1}
                className="role-tab"
                onClick={() => setIndex(i)}
              >
                <span className="role-tab__index">{String(i + 1).padStart(2, '0')}</span>
                <span className="role-tab__name">{r.name}</span>
                {i === index && (
                  <motion.span layoutId="role-indicator" className="role-tab__indicator" transition={spring.indicator} />
                )}
              </button>
            ))}
          </div>
        </Reveal>

        <div role="tabpanel" id="role-panel" aria-labelledby={`role-tab-${role.id}`} tabIndex={0} className="role-panel">
          <div key={role.id} className="role-panel__grid">
            <div className="role-panel__copy">
              <motion.p className="t-label role-panel__kicker" {...enter(0)}>
                {role.name}
              </motion.p>
              <motion.h3 className="role-panel__purpose" {...enter(0.05)}>
                {role.purpose}
              </motion.h3>
              <motion.ul role="list" className="role-caps" {...enter(0.12)}>
                {role.capabilities.map((c) => (
                  <li key={c}>
                    <span className="role-caps__mark" aria-hidden="true" />
                    {c}
                  </li>
                ))}
              </motion.ul>
            </div>
            <motion.div
              className="role-panel__visual"
              initial={reduced ? false : { opacity: 0, scale: 0.97 }}
              animate={{ opacity: 1, scale: 1 }}
              transition={{ duration: 0.5, ease: ease.out }}
            >
              <DarkPanel className="role-preview" glow>
                <RolePreview id={role.id} />
              </DarkPanel>
            </motion.div>
          </div>
        </div>
      </div>
    </section>
  )
}

// Visual centre of each drawing, so every role sits on the same focal point at the same scale.
const FOCUS: Record<RoleId, [number, number]> = {
  donor: [282, 177],
  beneficiary: [240, 164],
  courier: [258, 167],
  admin: [240, 180],
}

/* Abstract previews — shapes, not screenshots. Decorative; the copy beside them carries the meaning. */
function RolePreview({ id }: { id: RoleId }) {
  const [fx, fy] = FOCUS[id]
  return (
    <svg viewBox="0 0 480 360" preserveAspectRatio="xMidYMid slice" className="role-preview__svg" aria-hidden="true">
      {/* Shared stage: the loop's orbit behind every role */}
      <g className="rp-stage">
        {[70, 120, 175, 235, 300].map((r) => (
          <circle key={r} cx="240" cy="180" r={r} />
        ))}
        <circle cx="240" cy="180" r="150" className="rp-stage__orbit" />
      </g>
      <g transform={`translate(240 180) scale(1.04) translate(${-fx} ${-fy})`}>
        {id === 'donor' && (
          <g>
            <rect x="150" y="34" width="220" height="250" rx="22" className="rp-card rp-card--ghost" />
            <rect x="126" y="50" width="220" height="250" rx="22" className="rp-card rp-card--ghost" />
            <g transform="translate(96 66)">
              <rect width="228" height="254" rx="22" className="rp-card" />
              <rect x="14" y="14" width="200" height="104" rx="14" className="rp-media" />
              <ellipse cx="96" cy="72" rx="42" ry="24" className="rp-loaf" />
              <path d="M70 66c14-8 38-8 52 0M72 80c14-6 36-6 50 0" className="rp-score" />
              <rect x="18" y="136" width="130" height="12" rx="6" className="rp-bar rp-bar--strong" />
              <rect x="18" y="158" width="90" height="9" rx="4.5" className="rp-bar" />
              <rect x="18" y="200" width="112" height="36" rx="18" className="rp-pill" />
              <path d="M36 218l8 8 14-14" className="rp-check" />
              <text x="68" y="223" className="rp-text">Listed</text>
            </g>
            <path d="M380 96c14-30 50-38 68-24-8 30-44 40-68 24zM380 96c-6 10-10 22-10 36" className="rp-line" />
          </g>
        )}

        {id === 'beneficiary' && (
          <g>
            {Array.from({ length: 9 }, (_, r) =>
              Array.from({ length: 14 }, (_, c) => (
                <circle key={`${r}-${c}`} cx={30 + c * 32} cy={28 + r * 32} r="1.6" className="rp-dot" />
              )),
            )}
            <path d="M40 250C120 200 150 120 250 130S400 80 450 40" className="rp-road" />
            {[
              [120, 190, false],
              [250, 130, true],
              [372, 96, false],
            ].map(([x, y, hot]) => (
              <g key={`${x}`} transform={`translate(${x} ${y})`}>
                {hot && <circle r="30" className="rp-pulse" />}
                <path d="M0 0c-12-14-18-22-18-30a18 18 0 0 1 36 0c0 8-6 16-18 30z" className={hot ? 'rp-pin rp-pin--hot' : 'rp-pin'} />
                <circle cy="-30" r="6" className="rp-pin-eye" />
              </g>
            ))}
            <g transform="translate(262 196)">
              <rect width="190" height="104" rx="18" className="rp-card" />
              <rect x="16" y="18" width="42" height="42" rx="10" className="rp-media" />
              <rect x="70" y="22" width="96" height="10" rx="5" className="rp-bar rp-bar--strong" />
              <rect x="70" y="42" width="64" height="8" rx="4" className="rp-bar" />
              <rect x="16" y="72" width="158" height="18" rx="9" className="rp-pill" />
              <text x="95" y="85" textAnchor="middle" className="rp-text rp-text--sm">Claim</text>
            </g>
          </g>
        )}

        {id === 'courier' && (
          <g>
            <path d="M70 270C90 170 190 230 240 170S330 70 410 70" className="rp-route-base" />
            <path d="M70 270C90 170 190 230 240 170S330 70 410 70" className="rp-route" />
            <g transform="translate(70 270)">
              <circle r="16" className="rp-stop" />
              <circle r="5" className="rp-stop-core" />
              <text x="26" y="6" className="rp-text rp-text--light">Pickup</text>
            </g>
            <g transform="translate(410 70)">
              <circle r="16" className="rp-stop rp-stop--end" />
              <path d="M-6 0l4 4 8-8" className="rp-check" />
              <text x="-26" y="44" textAnchor="middle" className="rp-text rp-text--light">Drop-off</text>
            </g>
            <g transform="translate(240 170)">
              <circle r="28" className="rp-courier" />
              <circle cx="-9" cy="6" r="6" className="rp-line" />
              <circle cx="9" cy="6" r="6" className="rp-line" />
              <path d="M-9 6l6-10h8l4 10M-3-4l-2-5" className="rp-line" />
            </g>
            <g transform="translate(300 220)">
              <rect width="150" height="70" rx="16" className="rp-card" />
              <text x="18" y="30" className="rp-text rp-text--sm rp-text--muted">Handoff code</text>
              {[0, 1, 2, 3].map((i) => (
                <rect key={i} x={18 + i * 30} y="40" width="22" height="16" rx="4" className="rp-bar rp-bar--strong" />
              ))}
            </g>
          </g>
        )}

        {id === 'admin' && (
          <g>
            {[
              [110, 80, 'ok'],
              [370, 70, 'ok'],
              [420, 200, 'pending'],
              [300, 290, 'ok'],
              [120, 260, 'ok'],
              [60, 170, 'pending'],
            ].map(([x, y, state]) => (
              <g key={`${x}-${y}`}>
                <line x1="240" y1="170" x2={x} y2={y} className="rp-edge" />
                <g transform={`translate(${x} ${y})`}>
                  <circle r="22" className={state === 'ok' ? 'rp-org' : 'rp-org rp-org--pending'} />
                  {state === 'ok' ? <path d="M-7 0l5 5 9-9" className="rp-check" /> : <circle r="3" className="rp-stop-core" />}
                </g>
              </g>
            ))}
            <circle cx="240" cy="170" r="54" className="rp-hub-ring" />
            <circle cx="240" cy="170" r="40" className="rp-hub" />
            <FoodLoopMark x={216} y={146} width={48} height={48} className="rp-hub-mark" />
          </g>
        )}
      </g>
    </svg>
  )
}
