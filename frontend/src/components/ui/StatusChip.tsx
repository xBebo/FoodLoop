import type { HTMLAttributes, ReactNode } from 'react'
import { cn } from '../../lib/cn'
import './ui.css'

export type StatusTone = 'neutral' | 'warning' | 'info' | 'success' | 'complete' | 'danger'

type StatusChipProps = HTMLAttributes<HTMLSpanElement> & {
  tone?: StatusTone
  icon?: ReactNode
  /** Show a leading status dot (ignored when an icon is given). */
  dot?: boolean
}

/** Presentational status label. Colour is never the only signal — the text carries meaning. */
export function StatusChip({ tone = 'neutral', icon, dot = true, className, children, ...rest }: StatusChipProps) {
  return (
    <span className={cn('chip', `chip--${tone}`, className)} {...rest}>
      {icon ? (
        <span className="chip__icon" aria-hidden="true">{icon}</span>
      ) : (
        dot && <span className="chip__dot" aria-hidden="true" />
      )}
      {children}
    </span>
  )
}
