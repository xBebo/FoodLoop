import { motion, useInView, useReducedMotion } from 'framer-motion'
import { useEffect, useRef, useState, type CSSProperties } from 'react'
import { SECTIONS } from '../../app/routes'
import { AmbientOrb, BotanicalCorner, BotanicalDecoration } from '../../components/brand/Botanical'
import { Reveal } from '../../components/motion/Reveal'
import { SectionEyebrow } from '../../components/ui/SectionEyebrow'
import { cn } from '../../lib/cn'
import { ease, revealVariants, staggerVariants } from '../../lib/motion'
import { LOOP_STEPS } from './content'
import { LoopIcon } from './LoopIcons'

const ADVANCE_MS = 3600
// Diagram geometry (viewBox 600). Stations sit clockwise from 12 o'clock.
const C = 300
const R = 240
const angleOf = (i: number) => -90 + i * 90
const point = (deg: number, r = R) => {
  const a = (deg * Math.PI) / 180
  return [C + r * Math.cos(a), C + r * Math.sin(a)] as const
}
const arcTo = (i: number) => {
  const [x0, y0] = point(angleOf(i - 1))
  const [x1, y1] = point(angleOf(i))
  return `M${x0} ${y0}A${R} ${R} 0 0 1 ${x1} ${y1}`
}
const stationStyle = (i: number): CSSProperties => {
  const [x, y] = point(angleOf(i))
  return { left: `${(x / 600) * 100}%`, top: `${(y / 600) * 100}%` }
}

export function LoopSection() {
  const [active, setActive] = useState(0)
  const [paused, setPaused] = useState(false)
  const ref = useRef<HTMLElement>(null)
  const inView = useInView(ref, { amount: 0.35 })
  const reduced = useReducedMotion()

  // Gentle auto-advance while visible; hovering the loop takes over. All content is readable regardless.
  useEffect(() => {
    if (!inView || paused || reduced) return
    const id = window.setInterval(() => setActive((a) => (a + 1) % LOOP_STEPS.length), ADVANCE_MS)
    return () => window.clearInterval(id)
  }, [inView, paused, reduced])

  const step = LOOP_STEPS[active]

  return (
    <section
      ref={ref}
      id={SECTIONS.howItWorks}
      tabIndex={-1}
      className="loop on-dark grain"
      aria-labelledby="loop-title"
    >
      <BotanicalDecoration>
        <AmbientOrb tone="forest" className="loop__orb" />
        <BotanicalCorner position="top-right" className="loop__corner" />
      </BotanicalDecoration>

      <div className="container">
        <div className="loop__head">
          <Reveal>
            <SectionEyebrow index="02">How it works</SectionEyebrow>
            <h2 id="loop-title" className="loop__title">
              One loop.
              <br />
              Less waste.
              <br />
              <em>More good.</em>
            </h2>
          </Reveal>
          <Reveal className="loop__intro" delay={0.1}>
            <p className="t-lead">
              Four roles, one shared picture. Every listing moves through the same loop, so everyone knows what is
              happening to the food — and who has it now.
            </p>
          </Reveal>
        </div>

        <div className="loop__body" onMouseEnter={() => setPaused(true)} onMouseLeave={() => setPaused(false)}>
          <div className="loop-diagram" aria-hidden="true">
            <svg viewBox="0 0 600 600" className="loop-diagram__svg">
              <circle cx={C} cy={C} r={292} className="loop-diagram__outer" />
              <circle cx={C} cy={C} r={150} className="loop-diagram__inner" />
              <motion.circle
                cx={C}
                cy={C}
                r={R}
                className="loop-diagram__path"
                transform={`rotate(-90 ${C} ${C})`}
                initial={reduced ? false : { pathLength: 0 }}
                whileInView={{ pathLength: 1 }}
                viewport={{ once: true, amount: 0.4 }}
                transition={{ duration: 1.8, ease: ease.inOut }}
              />
              <motion.path
                key={active}
                d={arcTo(active)}
                className="loop-diagram__active"
                initial={reduced ? false : { pathLength: 0 }}
                animate={{ pathLength: 1 }}
                transition={{ duration: 0.9, ease: ease.out }}
              />
              {[45, 135, 225, 315].map((deg) => {
                const [x, y] = point(deg - 90)
                return (
                  <path
                    key={deg}
                    d="M-5 -7L4 0L-5 7"
                    className="loop-diagram__chevron"
                    transform={`translate(${x} ${y}) rotate(${deg})`}
                  />
                )
              })}
              <g className="loop-diagram__runner">
                <circle cx={C} cy={C - R} r={14} className="loop-diagram__runner-glow" />
                <circle cx={C} cy={C - R} r={4} className="loop-diagram__runner-dot" />
              </g>
            </svg>

            {LOOP_STEPS.map((s, i) => (
              <div
                key={s.id}
                className={cn('loop-station', i === active && 'is-active')}
                style={stationStyle(i)}
                onMouseEnter={() => setActive(i)}
              >
                <span className="loop-station__node">
                  <LoopIcon id={s.id} />
                </span>
                <span className="loop-station__label t-label">{s.role}</span>
              </div>
            ))}

            <div className="loop-diagram__center">
              <motion.div
                key={active}
                initial={reduced ? false : { opacity: 0, y: 8 }}
                animate={{ opacity: 1, y: 0 }}
                transition={{ duration: 0.4, ease: ease.out }}
              >
                <span className="loop-diagram__num">{String(active + 1).padStart(2, '0')}</span>
                <span className="loop-diagram__caption">{step.title}</span>
              </motion.div>
            </div>
          </div>

          <motion.ol
            className="loop-steps"
            role="list"
            variants={staggerVariants}
            initial={reduced ? false : 'hidden'}
            whileInView="visible"
            viewport={{ once: true, amount: 0.2 }}
          >
            {LOOP_STEPS.map((s, i) => (
              <motion.li
                key={s.id}
                variants={revealVariants}
                className={cn('loop-step', i === active && 'is-active')}
                onMouseEnter={() => setActive(i)}
              >
                <span className="loop-step__node" aria-hidden="true">
                  <LoopIcon id={s.id} />
                </span>
                <div className="loop-step__text">
                  <p className="loop-step__role t-label">
                    <span className="loop-step__index">{String(i + 1).padStart(2, '0')}</span>
                    {s.role}
                  </p>
                  <h3 className="loop-step__title">{s.title}</h3>
                  <p className="loop-step__body">{s.body}</p>
                </div>
              </motion.li>
            ))}
          </motion.ol>
        </div>
      </div>
    </section>
  )
}
