import { motion, useReducedMotion, type HTMLMotionProps } from 'framer-motion'
import { revealVariants, staggerVariants } from '../../lib/motion'

type RevealProps = HTMLMotionProps<'div'> & {
  /** Extra delay in seconds (standalone Reveal only). */
  delay?: number
}

const VIEWPORT = { once: true, amount: 'some', margin: '0px 0px -10% 0px' } as const

/** Fades + rises into view once. Under reduced motion it renders in its final state immediately. */
export function Reveal({ delay = 0, transition, ...props }: RevealProps) {
  const reduced = useReducedMotion()
  return (
    <motion.div
      variants={revealVariants}
      initial={reduced ? false : 'hidden'}
      whileInView="visible"
      viewport={VIEWPORT}
      transition={delay ? { delay, ...transition } : transition}
      {...props}
    />
  )
}

/** Parent that staggers its <RevealItem> children as the group enters the viewport. */
export function RevealGroup(props: HTMLMotionProps<'div'>) {
  const reduced = useReducedMotion()
  return (
    <motion.div
      variants={staggerVariants}
      initial={reduced ? false : 'hidden'}
      whileInView="visible"
      viewport={VIEWPORT}
      {...props}
    />
  )
}

/** Child of <RevealGroup>; inherits the group's timing. */
export function RevealItem(props: HTMLMotionProps<'div'>) {
  return <motion.div variants={revealVariants} {...props} />
}
