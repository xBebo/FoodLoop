import { Clock, Hourglass } from 'lucide-react'
import { expiryPhrase, type ExpiryView } from '../../lib/expiry'
import { cn } from '../../lib/cn'
import './food.css'

/** Urgency pill. Icon shape + wording carry the signal; colour only reinforces it. */
export function ExpiryBadge({ expiry, className }: { expiry: ExpiryView; className?: string }) {
  const Icon = expiry.urgency === 'critical' ? Hourglass : Clock
  return (
    <span className={cn('expiry', `expiry--${expiry.urgency}`, className)}>
      <Icon aria-hidden="true" />
      <span>{expiryPhrase(expiry)}</span>
    </span>
  )
}
