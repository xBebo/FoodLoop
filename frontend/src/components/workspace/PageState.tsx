import { RotateCcw } from 'lucide-react'
import type { ReactNode } from 'react'
import { Button } from '../ui/Button'

/** Full-page loading placeholder for a workspace screen waiting on its first API response. */
export function PageLoading({ label }: { label: string }) {
  return (
    <div className="container ws-page" aria-busy="true">
      <p className="ws-empty" role="status">
        {label}
      </p>
    </div>
  )
}

/** Full-page empty / not-found / blocked / failed state. Pass `onRetry` for a load that can simply be tried again. */
export function PageMessage({ title, children, action, onRetry }: { title: string; children?: ReactNode; action?: ReactNode; onRetry?: () => void }) {
  return (
    <div className="container ws-page">
      <div className="ws-empty">
        <h1 className="t-h2">{title}</h1>
        {children && <p>{children}</p>}
        {action}
        {onRetry && (
          <Button variant="outline" iconStart={<RotateCcw />} onClick={onRetry}>
            Try again
          </Button>
        )}
      </div>
    </div>
  )
}
