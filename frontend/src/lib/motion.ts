import type { Transition, Variants } from 'framer-motion'

// Mirrors the CSS tokens in src/styles/motion.css (seconds here, ms there).
export const duration = {
  fast: 0.15,
  ui: 0.26,
  reveal: 0.45,
  page: 0.28,
  ambient: 18,
} as const

export const ease = {
  out: [0.22, 1, 0.36, 1],
  inOut: [0.65, 0, 0.35, 1],
} as const

// Springs are critically-damped-ish: settle quickly, never wobble.
export const spring = {
  ui: { type: 'spring', stiffness: 420, damping: 38, mass: 0.9 },
  indicator: { type: 'spring', stiffness: 380, damping: 34 },
  magnetic: { type: 'spring', stiffness: 220, damping: 20, mass: 0.5 },
  parallax: { stiffness: 90, damping: 22, mass: 0.6 },
} as const satisfies Record<string, Transition>

export const REVEAL_DISTANCE = 24
export const STAGGER = 0.07

export const revealVariants: Variants = {
  hidden: { opacity: 0, y: REVEAL_DISTANCE },
  visible: { opacity: 1, y: 0, transition: { duration: duration.reveal, ease: ease.out } },
}

export const staggerVariants: Variants = {
  hidden: {},
  visible: { transition: { staggerChildren: STAGGER, delayChildren: 0.04 } },
}
