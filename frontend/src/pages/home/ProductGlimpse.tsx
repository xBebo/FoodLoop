import { motion, useReducedMotion, useScroll, useTransform } from 'framer-motion'
import { Bike, Check, Clock, MapPin } from 'lucide-react'
import { useRef } from 'react'
import { SECTIONS } from '../../app/routes'
import { FoodLoopMark } from '../../components/brand/FoodLoopMark'
import { Reveal } from '../../components/motion/Reveal'
import { Photo } from '../../components/ui/Photo'
import { SectionEyebrow } from '../../components/ui/SectionEyebrow'
import { StatusChip } from '../../components/ui/StatusChip'
import { PHOTOS } from '../../data/photography'
import { cn } from '../../lib/cn'
import { ease } from '../../lib/motion'
import { PARALLAX_QUERY, useMediaQuery } from '../../lib/useMediaQuery'
import { GLIMPSE } from './content'

const LEGEND = [
  ['Marketplace card', 'What donors publish and organizations browse.'],
  ['Claim journey', 'Every claim, from listed to delivered, in one line.'],
  ['Courier ticket', 'A run with a pickup, a drop-off and a handoff check.'],
] as const

// Deterministic 5×5 "handoff code" pattern (mirrored like a real 2D code). Decorative.
const CODE = [1, 0, 1, 1, 0, 1, 1, 0, 1, 0, 0, 1, 1, 0, 1, 1, 0, 1, 0, 1, 1, 1, 0, 0, 1]

export function ProductGlimpse() {
  const stage = useRef<HTMLDivElement>(null)
  const reduced = useReducedMotion()
  const depth = useMediaQuery(PARALLAX_QUERY) && !reduced
  const { scrollYProgress } = useScroll({ target: stage, offset: ['start end', 'end start'] })
  const backY = useTransform(scrollYProgress, [0, 1], depth ? [36, -36] : [0, 0])
  const frontY = useTransform(scrollYProgress, [0, 1], depth ? [-28, 44] : [0, 0])

  const rise = (delay: number) => ({
    initial: reduced ? false : { opacity: 0, y: 40 },
    whileInView: { opacity: 1, y: 0 },
    viewport: { once: true, amount: 0.3 },
    transition: { duration: 0.8, ease: ease.out, delay },
  })

  return (
    <section id={SECTIONS.platform} tabIndex={-1} className="glimpse" aria-labelledby="glimpse-title">
      <div className="container glimpse__grid">
        <Reveal className="glimpse__copy">
          <SectionEyebrow index="05">Product glimpse</SectionEyebrow>
          <h2 id="glimpse-title">
            Not a campaign. <em>A working platform.</em>
          </h2>
          <p className="t-lead">
            Behind every rescue is a listing, a claim and a courier run. FoodLoop gives each one a clear surface, so
            nothing depends on phone calls and good luck.
          </p>
          <ol className="glimpse-legend" role="list">
            {LEGEND.map(([name, text], i) => (
              <li key={name}>
                <span className="glimpse-legend__index">{String(i + 1).padStart(2, '0')}</span>
                <span>
                  <strong>{name}</strong>
                  {text}
                </span>
              </li>
            ))}
          </ol>
        </Reveal>

        <figure className="glimpse__stage" ref={stage}>
          <figcaption className="glimpse__caption t-label">Interface preview · sample data</figcaption>

          {/* 01 — Marketplace card */}
          <motion.div className="glimpse-layer glimpse-layer--market" style={{ y: backY }}>
            <motion.article className="gl-card gl-market" {...rise(0)}>
              <div className="gl-market__media">
                <Photo photo={PHOTOS.heroRescue} decorative className="gl-market__img" />
                <StatusChip tone="success" className="gl-market__chip">
                  Available
                </StatusChip>
              </div>
              <div className="gl-market__body">
                <p className="gl-card__eyebrow">{GLIMPSE.listing.donor}</p>
                <h3 className="gl-card__title">{GLIMPSE.listing.title}</h3>
                <p className="gl-market__meta">
                  <Clock aria-hidden="true" />
                  {GLIMPSE.listing.window}
                </p>
                <div className="gl-market__foot">
                  <span className="gl-market__tags">
                    {GLIMPSE.listing.tags.map((t) => (
                      <span key={t} className="gl-tag">
                        {t}
                      </span>
                    ))}
                  </span>
                  <span className="gl-fauxbtn">Claim</span>
                </div>
              </div>
            </motion.article>
          </motion.div>

          {/* 02 — Claim journey */}
          <div className="glimpse-layer glimpse-layer--claim">
            <motion.article className="gl-card gl-claim" {...rise(0.12)}>
              <header className="gl-claim__head">
                <p className="gl-card__eyebrow">Claim journey</p>
                <h3 className="gl-card__title">{GLIMPSE.claim.org}</h3>
              </header>
              <ol className="gl-steps" role="list">
                {GLIMPSE.claim.steps.map((s) => (
                  <li key={s.label} className={cn('gl-step', `gl-step--${s.state}`)}>
                    <span className="gl-step__dot" aria-hidden="true">
                      {s.state === 'done' && <Check />}
                    </span>
                    <span className="gl-step__label">{s.label}</span>
                    <span className="gl-step__state">
                      {s.state === 'done' ? 'Done' : s.state === 'current' ? 'In progress' : 'Next'}
                    </span>
                  </li>
                ))}
              </ol>
            </motion.article>
          </div>

          {/* 03 — Courier ticket */}
          <motion.div className="glimpse-layer glimpse-layer--ticket" style={{ y: frontY }}>
            <motion.article className="gl-card gl-ticket on-dark" {...rise(0.24)}>
              <header className="gl-ticket__head">
                <span className="gl-ticket__kind">
                  <Bike aria-hidden="true" />
                  Courier ticket
                </span>
                <FoodLoopMark className="gl-ticket__mark" />
              </header>
              <div className="gl-ticket__route">
                <p>
                  <span className="t-label">Pickup</span>
                  <MapPin aria-hidden="true" />
                  {GLIMPSE.ticket.pickup}
                </p>
                <p>
                  <span className="t-label">Drop-off</span>
                  <MapPin aria-hidden="true" />
                  {GLIMPSE.ticket.dropoff}
                </p>
              </div>
              <div className="gl-ticket__tear" aria-hidden="true" />
              <div className="gl-ticket__foot">
                <span className="gl-code" aria-hidden="true">
                  {CODE.map((on, i) => (
                    <span key={i} className={on ? 'is-on' : undefined} />
                  ))}
                </span>
                <span className="gl-ticket__hint">Scan at handoff to confirm delivery</span>
              </div>
            </motion.article>
          </motion.div>
        </figure>
      </div>
    </section>
  )
}
