import type { HTMLAttributes } from 'react'
import { cn } from '../../lib/cn'
import './ui.css'

type SectionEyebrowProps = HTMLAttributes<HTMLParagraphElement> & {
  /** Editorial index, e.g. "01". */
  index?: string
}

export function SectionEyebrow({ index, className, children, ...rest }: SectionEyebrowProps) {
  return (
    <p className={cn('eyebrow t-label', className)} {...rest}>
      {index && <span className="eyebrow__index">{index}</span>}
      <span className="eyebrow__rule" aria-hidden="true" />
      <span>{children}</span>
    </p>
  )
}
