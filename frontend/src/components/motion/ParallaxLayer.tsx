import { motion, useTransform, type HTMLMotionProps, type MotionValue } from 'framer-motion'

type ParallaxLayerProps = HTMLMotionProps<'div'> & {
  px: MotionValue<number>
  py: MotionValue<number>
  /** Max travel in px at the pointer extremes. Keep within 4–10. Negative = moves against the pointer. */
  depth: number
}

/** One depth plane of a pointer-parallax composition (see usePointerParallax). Transform-only. */
export function ParallaxLayer({ px, py, depth, style, ...rest }: ParallaxLayerProps) {
  const x = useTransform(px, (v) => v * depth)
  const y = useTransform(py, (v) => v * depth)
  return <motion.div style={{ ...style, x, y }} {...rest} />
}
