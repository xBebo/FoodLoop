import type { SVGProps } from 'react'
import { cn } from '../../lib/cn'
import './brand.css'

// Geometry: a 284° rescue loop (circle r=11) whose open gap is closed by a single leaf.
// Loop inherits currentColor; leaf uses --mark-leaf so it can take an accent on either ground.
// public/favicon.svg uses the same two paths — update both together.
const MARK_LOOP = 'M26.55 13.03A11 11 0 1 1 15.35 6.56'
const MARK_LEAF = 'M17.27 6.53C19.83 1.85 28.37 6.04 25.83 11.67C25.12 9.74 19.65 7.8 17.27 6.53Z'

type MarkProps = SVGProps<SVGSVGElement> & {
  /** Accessible name. Omit when the mark sits next to visible "FoodLoop" text. */
  title?: string
}

export function FoodLoopMark({ title, className, ...rest }: MarkProps) {
  return (
    <svg
      viewBox="0 0 32 32"
      fill="none"
      className={cn('fl-mark', className)}
      role={title ? 'img' : undefined}
      aria-hidden={title ? undefined : true}
      {...rest}
    >
      {title && <title>{title}</title>}
      <path d={MARK_LOOP} stroke="currentColor" strokeWidth="3" strokeLinecap="round" />
      <path d={MARK_LEAF} fill="var(--mark-leaf, currentColor)" />
    </svg>
  )
}

export function FoodLoopWordmark({ className }: { className?: string }) {
  return (
    <span className={cn('fl-wordmark', className)}>
      <FoodLoopMark className="fl-wordmark__mark" />
      <span className="fl-wordmark__text">
        Food<em>Loop</em>
      </span>
    </span>
  )
}
