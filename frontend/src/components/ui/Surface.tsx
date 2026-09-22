import type { HTMLAttributes, ReactNode } from 'react'
import { cn } from '../../lib/cn'
import { AmbientOrb } from '../brand/Botanical'
import './ui.css'

export type SurfaceTone = 'paper' | 'mint' | 'dark' | 'elevated' | 'sunken'
type SurfaceElement = 'div' | 'section' | 'article' | 'aside' | 'li'

type SurfaceProps = HTMLAttributes<HTMLElement> & {
  as?: SurfaceElement
  tone?: SurfaceTone
  /** Hover lift + pointer affordance. Pair with a real link/button inside for semantics. */
  interactive?: boolean
  /** Signature asymmetric "leaf" corners instead of uniform radius. */
  leaf?: boolean
}

/** Base layer. Everything with a background and a radius is a Surface. */
export function Surface({ as: Tag = 'div', tone = 'paper', interactive, leaf, className, ...rest }: SurfaceProps) {
  return (
    <Tag
      className={cn(
        'surface',
        `surface--${tone}`,
        interactive && 'surface--interactive',
        leaf && 'surface--leaf',
        tone === 'dark' && 'on-dark grain',
        className,
      )}
      {...rest}
    />
  )
}

/** Surface with content padding and an optional header row. */
export function Card({ eyebrow, title, children, className, ...rest }: SurfaceProps & { eyebrow?: ReactNode; title?: ReactNode }) {
  return (
    <Surface className={cn('card', className)} {...rest}>
      {(eyebrow || title) && (
        <header className="card__header">
          {eyebrow && <div className="card__eyebrow t-label">{eyebrow}</div>}
          {title && <h3 className="card__title t-h4">{title}</h3>}
        </header>
      )}
      {children}
    </Surface>
  )
}

/** Dramatic forest panel with ambient light. Use sparingly — one or two per page. */
export function DarkPanel({ children, className, glow = true, ...rest }: Omit<SurfaceProps, 'tone'> & { glow?: boolean }) {
  return (
    <Surface tone="dark" className={cn('dark-panel', className)} {...rest}>
      {glow && <AmbientOrb className="dark-panel__glow" />}
      {children}
    </Surface>
  )
}
