import { motion, useReducedMotion } from 'framer-motion'
import type { HTMLAttributes } from 'react'
import { cn } from '../../lib/cn'
import { duration, ease } from '../../lib/motion'
import './brand.css'

/* Original line-art system. Every piece is aria-hidden, stroke = currentColor, 1px non-scaling. */

type Pt = [number, number]
type Cubic = [Pt, Pt, Pt, Pt]

function cubicAt([p0, p1, p2, p3]: Cubic, t: number) {
  const u = 1 - t
  const x = u * u * u * p0[0] + 3 * u * u * t * p1[0] + 3 * u * t * t * p2[0] + t * t * t * p3[0]
  const y = u * u * u * p0[1] + 3 * u * u * t * p1[1] + 3 * u * t * t * p2[1] + t * t * t * p3[1]
  const dx = 3 * u * u * (p1[0] - p0[0]) + 6 * u * t * (p2[0] - p1[0]) + 3 * t * t * (p3[0] - p2[0])
  const dy = 3 * u * u * (p1[1] - p0[1]) + 6 * u * t * (p2[1] - p1[1]) + 3 * t * t * (p3[1] - p2[1])
  return { x, y, angle: (Math.atan2(dy, dx) * 180) / Math.PI }
}

const pathOf = (segs: Cubic[]) =>
  `M${segs[0][0].join(' ')}` + segs.map(([, a, b, c]) => `C${a.join(' ')} ${b.join(' ')} ${c.join(' ')}`).join('')

// Leaf outline pointing along +x from the origin, plus its midrib.
const LEAF = 'M0 0C8 -7 26 -9 40 0C26 9 8 7 0 0Z'
const RIB = 'M3 0C14 -1 26 -1 35 0'

// Tall stem: two cubics sharing a continuous tangent at the joint.
const STEM: Cubic[] = [
  [[100, 400], [96, 322], [124, 262], [108, 190]],
  [[108, 190], [94, 118], [82, 70], [118, 12]],
]
const STEM_D = pathOf(STEM)

const BRANCH_LEAVES = [0.14, 0.3, 0.46, 0.6, 0.74, 0.86, 0.95].map((s, i) => {
  const p = s < 0.5 ? cubicAt(STEM[0], s * 2) : cubicAt(STEM[1], (s - 0.5) * 2)
  const side = i % 2 === 0 ? -1 : 1
  return { x: p.x, y: p.y, rotate: p.angle + side * 48, scale: 1.25 - s * 0.7 }
})

const BUDS = [cubicAt(STEM[1], 1), cubicAt(STEM[1], 0.35), cubicAt(STEM[0], 0.55)]

type DrawProps = { className?: string; draw?: boolean }

/** Tall sprig with alternating leaves. `draw` traces the stem in once when scrolled into view. */
export function BotanicalBranch({ className, draw = true }: DrawProps) {
  const reduced = useReducedMotion()
  const animate = draw && !reduced
  return (
    <svg viewBox="0 0 220 410" fill="none" className={cn('botanical', className)} aria-hidden="true">
      <motion.path
        d={STEM_D}
        stroke="currentColor"
        vectorEffect="non-scaling-stroke"
        initial={animate ? { pathLength: 0 } : false}
        whileInView={{ pathLength: 1 }}
        viewport={{ once: true }}
        transition={{ duration: 1.6, ease: ease.inOut }}
      />
      {BRANCH_LEAVES.map((l, i) => (
        <motion.g
          key={i}
          initial={animate ? { opacity: 0 } : false}
          whileInView={{ opacity: 1 }}
          viewport={{ once: true }}
          transition={{ duration: duration.reveal * 2, delay: 0.3 + i * 0.14, ease: ease.out }}
        >
          <g transform={`translate(${l.x} ${l.y}) rotate(${l.rotate}) scale(${l.scale})`}>
            <path d={LEAF} stroke="currentColor" vectorEffect="non-scaling-stroke" />
            <path d={RIB} stroke="currentColor" strokeOpacity="0.5" vectorEffect="non-scaling-stroke" />
          </g>
        </motion.g>
      ))}
      {BUDS.map((b, i) => (
        <circle key={i} cx={b.x} cy={b.y} r={i === 0 ? 3.5 : 2} fill="currentColor" opacity={i === 0 ? 0.9 : 0.5} />
      ))}
    </svg>
  )
}

// Topographic contours: perturbed circles centred on the corner; only one quadrant shows.
const CONTOURS = Array.from({ length: 7 }, (_, i) => {
  const R = 70 + i * 34
  const pts: string[] = []
  for (let k = 0; k <= 96; k++) {
    const th = (k / 96) * Math.PI * 2
    const r = R * (1 + 0.045 * Math.sin(3 * th + i * 0.7) + 0.022 * Math.sin(7 * th - i * 1.3))
    pts.push(`${(r * Math.cos(th)).toFixed(1)} ${(r * Math.sin(th)).toFixed(1)}`)
  }
  return `M${pts.join('L')}Z`
})

const SPRIG_LEAVES: [number, number, number, number][] = [
  [22, 4, -40, 0.8],
  [44, 13, 58, 0.72],
  [64, 27, -34, 0.6],
]

type CornerProps = { className?: string; position?: 'top-left' | 'top-right' | 'bottom-left' | 'bottom-right' }

/** Contour lines + a small sprig radiating from one corner of its parent. */
export function BotanicalCorner({ className, position = 'top-left' }: CornerProps) {
  return (
    <svg
      viewBox="0 0 320 320"
      fill="none"
      className={cn('botanical botanical-corner', `botanical-corner--${position}`, className)}
      aria-hidden="true"
    >
      {CONTOURS.map((d, i) => (
        <path key={i} d={d} stroke="currentColor" strokeOpacity={0.55 - i * 0.06} vectorEffect="non-scaling-stroke" />
      ))}
      <g transform="translate(150 108) rotate(28)">
        <path d="M0 0C30 6 58 20 84 44" stroke="currentColor" vectorEffect="non-scaling-stroke" />
        {SPRIG_LEAVES.map(([x, y, r, s], i) => (
          <path
            key={i}
            d={LEAF}
            transform={`translate(${x} ${y}) rotate(${r}) scale(${s})`}
            stroke="currentColor"
            vectorEffect="non-scaling-stroke"
          />
        ))}
        <circle cx="84" cy="44" r="2.5" fill="currentColor" />
      </g>
      <circle cx="238" cy="36" r="2" fill="currentColor" opacity="0.6" />
      <circle cx="70" cy="226" r="1.5" fill="currentColor" opacity="0.5" />
    </svg>
  )
}

type OrbProps = HTMLAttributes<HTMLDivElement> & {
  tone?: 'sage' | 'mint' | 'forest'
  /** Slow CSS drift (disabled under reduced motion). */
  drift?: boolean
}

/** Soft radial light. Pure gradient, no blur filter, so it costs almost nothing to composite. */
export function AmbientOrb({ tone = 'sage', drift = true, className, ...rest }: OrbProps) {
  return <div className={cn('orb', `orb--${tone}`, drift && 'orb--drift', className)} aria-hidden="true" {...rest} />
}

/** Absolutely positioned, clipped, non-interactive layer for decorations inside a relative parent. */
export function BotanicalDecoration({ className, ...rest }: HTMLAttributes<HTMLDivElement>) {
  return <div className={cn('botanical-layer', className)} aria-hidden="true" {...rest} />
}
