import { motion, useInView, useReducedMotion } from 'framer-motion'
import { useRef, type ReactNode } from 'react'
import { cn } from '../../lib/cn'
import { ease } from '../../lib/motion'

type MediaRevealProps = {
  children: ReactNode
  className?: string
  delay?: number
  /** Direction the mask opens toward. */
  from?: 'bottom' | 'left'
}

const HIDDEN = { bottom: 'inset(100% 0% 0% 0%)', left: 'inset(0% 100% 0% 0%)' }
const SHOWN = 'inset(0% 0% 0% 0%)'

/** Wipes media in behind a clip mask while the content settles from a slight zoom. One-shot. */
export function MediaReveal({ children, className, delay = 0, from = 'bottom' }: MediaRevealProps) {
  const reduced = useReducedMotion()
  // IntersectionObserver reports an element fully hidden by its own clip-path as not intersecting,
  // so visibility is measured on the (unclipped) parent instead.
  const anchor = useRef<HTMLElement | null>(null)
  const inView = useInView(anchor, { once: true, amount: 0.25 })
  const shown = inView || reduced

  return (
    <motion.div
      ref={(el) => {
        anchor.current = el?.parentElement ?? null
      }}
      className={cn('media-reveal', className)}
      initial={reduced ? false : { clipPath: HIDDEN[from] }}
      animate={{ clipPath: shown ? SHOWN : HIDDEN[from] }}
      transition={{ duration: 1.1, ease: ease.inOut, delay }}
    >
      <motion.div
        className="media-reveal__inner"
        initial={reduced ? false : { scale: 1.12 }}
        animate={{ scale: shown ? 1 : 1.12 }}
        transition={{ duration: 1.6, ease: ease.out, delay }}
      >
        {children}
      </motion.div>
    </motion.div>
  )
}
