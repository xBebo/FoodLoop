import { useReducedMotion, useSpring } from 'framer-motion'
import type { PointerEvent } from 'react'
import { spring } from './motion'
import { PARALLAX_QUERY, useMediaQuery } from './useMediaQuery'

/**
 * Normalised pointer position (-1..1 from centre) of the element the handlers are bound to.
 * Inert (stays at 0) on touch, small screens and reduced motion, so layers simply sit still.
 */
export function usePointerParallax() {
  const reduced = useReducedMotion()
  const enabled = useMediaQuery(PARALLAX_QUERY) && !reduced
  const px = useSpring(0, spring.parallax)
  const py = useSpring(0, spring.parallax)

  function onPointerMove(e: PointerEvent<HTMLElement>) {
    if (!enabled || e.pointerType !== 'mouse') return
    const r = e.currentTarget.getBoundingClientRect()
    px.set(((e.clientX - r.left) / r.width - 0.5) * 2)
    py.set(((e.clientY - r.top) / r.height - 0.5) * 2)
  }

  function onPointerLeave() {
    px.set(0)
    py.set(0)
  }

  return { px, py, handlers: { onPointerMove, onPointerLeave } }
}
