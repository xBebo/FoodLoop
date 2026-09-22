import { motion, useReducedMotion, type MotionValue } from 'framer-motion'
import type { CSSProperties } from 'react'
import { AmbientOrb, BotanicalBranch } from '../../components/brand/Botanical'
import { FoodLoopMark } from '../../components/brand/FoodLoopMark'
import { MediaReveal } from '../../components/motion/MediaReveal'
import { ParallaxLayer } from '../../components/motion/ParallaxLayer'
import { Photo } from '../../components/ui/Photo'
import { StatusChip } from '../../components/ui/StatusChip'
import { PHOTOS } from '../../data/photography'
import { ease } from '../../lib/motion'
import { LoopIcon } from './LoopIcons'

/*
 * One orbit system wrapped around the photo. Geometry is in units of the visual's width
 * (box is 100 × 110, see .hero-visual aspect-ratio). The ring passes behind the arch; a front arc
 * of the same circle crosses the photo and ends in the node the rescue ticket hangs from.
 * Ring, arc, labels and ticket share one parallax depth so they never drift apart.
 */
const BOX_H = 110
const CX = 52
const CY = 51.7
const R = 46
// The front arc is the delivery route: it leaves the Community node, crosses the base of the photo
// and ends at the node the rescue ticket hangs from.
const ARC_FROM = 152 // Community
const ARC_TO = 35 // ticket anchor

function onOrbit(deg: number) {
  const a = (deg * Math.PI) / 180
  return { x: CX + R * Math.cos(a), y: ((CY + R * Math.sin(a)) / BOX_H) * 100 }
}
// Same angle on the orbit SVG's own 0–100 box (circle r=49.5 centred at 50,50).
function onRing(deg: number) {
  const a = (deg * Math.PI) / 180
  return `${(50 + 49.5 * Math.cos(a)).toFixed(2)} ${(50 + 49.5 * Math.sin(a)).toFixed(2)}`
}

const ORBIT_BOX: CSSProperties = {
  left: `${CX - R}%`,
  top: `${((CY - R) / BOX_H) * 100}%`,
  width: `${R * 2}%`,
}
const anchor = onOrbit(ARC_TO)
const TICKET_ANCHOR = { '--anchor-x': `${anchor.x}%`, '--anchor-y': `${anchor.y}%` } as CSSProperties

// `side`: which way the label reads from its node on desktop (outward). Tablet flips them inward in CSS.
const LABELS = [
  { id: 'donor', text: 'Donor', deg: 206, side: 'left' },
  { id: 'courier', text: 'Courier', deg: -62, side: 'right' },
  { id: 'community', text: 'Community', deg: ARC_FROM, side: 'left' },
] as const

type HeroVisualProps = { px: MotionValue<number>; py: MotionValue<number> }

export function HeroVisual({ px, py }: HeroVisualProps) {
  const reduced = useReducedMotion()
  const enter = (delay: number, from: { opacity?: number; y?: number; scale?: number } = { opacity: 0 }) => ({
    initial: reduced ? false : from,
    animate: { opacity: 1, y: 0, scale: 1 },
    transition: { duration: 0.9, ease: ease.out, delay },
  })

  return (
    <div className="hero-visual">
      {/* Atmosphere: light + branch, furthest back */}
      <ParallaxLayer px={px} py={py} depth={-6} className="hero-visual__plane">
        <AmbientOrb tone="mint" drift={false} className="hero-visual__glow" />
        <BotanicalBranch className="hero-visual__branch" />
      </ParallaxLayer>

      {/* Orbit, behind the photo */}
      <ParallaxLayer px={px} py={py} depth={6} className="hero-visual__plane">
        <motion.div className="hero-orbit" style={ORBIT_BOX} {...enter(0.35, { opacity: 0, scale: 0.94 })}>
          <svg viewBox="0 0 100 100" className="hero-orbit__ring" aria-hidden="true">
            <circle cx="50" cy="50" r="49.5" className="hero-orbit__track" />
            <circle cx="50" cy="50" r="49.5" className="hero-orbit__dash" />
            <circle cx="50" cy="0.5" r="0.9" className="hero-orbit__node" />
            <circle cx="0.5" cy="50" r="0.7" className="hero-orbit__node" />
          </svg>
        </motion.div>
      </ParallaxLayer>

      {/* Photo — moves against the orbit for depth */}
      <ParallaxLayer px={px} py={py} depth={-3} className="hero-visual__plane">
        <MediaReveal className="hero-visual__media media-frame" delay={0.15}>
          <Photo photo={PHOTOS.heroRescue} priority className="hero-visual__img" />
        </MediaReveal>
      </ParallaxLayer>

      {/* Orbit, in front of the photo: arc, role nodes, and the ticket hanging from the arc's end */}
      <ParallaxLayer px={px} py={py} depth={6} className="hero-visual__plane hero-visual__plane--front">
        <motion.div className="hero-orbit hero-orbit--front" style={ORBIT_BOX} {...enter(0.7)}>
          <svg viewBox="0 0 100 100" aria-hidden="true">
            <path d={`M${onRing(ARC_FROM)}A49.5 49.5 0 0 0 ${onRing(ARC_TO)}`} className="hero-orbit__front" />
          </svg>
        </motion.div>

        {LABELS.map((l, i) => {
          const p = onOrbit(l.deg)
          return (
            <motion.span
              key={l.id}
              className={`hero-tag hero-tag--${l.id}`}
              data-side={l.side}
              style={{ left: `${p.x}%`, top: `${p.y}%` }}
              {...enter(0.9 + i * 0.12, { opacity: 0, scale: 0.9 })}
            >
              <span className="hero-tag__node" aria-hidden="true" />
              <span className="hero-tag__label">
                <span className="hero-tag__icon" aria-hidden="true">
                  <LoopIcon id={l.id} />
                </span>
                {l.text}
              </span>
            </motion.span>
          )
        })}

        <motion.div className="hero-ticket" style={TICKET_ANCHOR} {...enter(1.05, { opacity: 0, y: 18 })}>
          <span className="hero-ticket__node" aria-hidden="true" />
          <div className="hero-ticket__card">
            <div className="hero-ticket__head">
              <span className="t-label hero-ticket__kind">Rescue ticket</span>
              <StatusChip tone="success">Ready for pickup</StatusChip>
            </div>
            <p className="hero-ticket__title">Bakery surplus</p>
            <p className="hero-ticket__meta">Sourdough, rye &amp; morning pastries</p>
            <div className="hero-ticket__route" aria-hidden="true">
              <span className="hero-ticket__stop">Donor</span>
              <span className="hero-ticket__line">
                <span className="hero-ticket__mark">
                  <FoodLoopMark />
                </span>
              </span>
              <span className="hero-ticket__stop">Community</span>
            </div>
            <p className="hero-ticket__foot">Pickup window · this evening</p>
          </div>
        </motion.div>
      </ParallaxLayer>
    </div>
  )
}
