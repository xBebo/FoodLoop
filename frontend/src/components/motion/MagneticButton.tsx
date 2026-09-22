import { motion, useMotionValue, useReducedMotion, useSpring } from 'framer-motion'
import type { PointerEvent, ReactNode } from 'react'
import { spring } from '../../lib/motion'
import { cn } from '../../lib/cn'

type MagneticProps = {
  children: ReactNode
  /** Max travel in px. Clamped to 7 so the target never drifts away from the pointer. */
  strength?: number
  className?: string
}

/**
 * Wraps any CTA and lets it lean a few pixels toward a fine pointer.
 * Mouse only (touch/pen ignored), disabled under reduced motion, transform-only.
 */
export function MagneticButton({ children, strength = 6, className }: MagneticProps) {
  const reduced = useReducedMotion()
  const max = Math.min(strength, 7)
  const x = useSpring(useMotionValue(0), spring.magnetic)
  const y = useSpring(useMotionValue(0), spring.magnetic)

  function onPointerMove(e: PointerEvent<HTMLSpanElement>) {
    if (reduced || e.pointerType !== 'mouse') return
    const r = e.currentTarget.getBoundingClientRect()
    // -1..1 from centre, eased so the pull is strongest near the middle of the travel
    const dx = ((e.clientX - r.left) / r.width - 0.5) * 2
    const dy = ((e.clientY - r.top) / r.height - 0.5) * 2
    x.set(dx * max)
    y.set(dy * max * 0.6)
  }

  function reset() {
    x.set(0)
    y.set(0)
  }

  return (
    <motion.span
      className={cn('magnetic', className)}
      style={reduced ? undefined : { x, y }}
      onPointerMove={onPointerMove}
      onPointerLeave={reset}
      onBlur={reset}
    >
      {children}
    </motion.span>
  )
}
