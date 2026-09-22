import { useRef, useState } from 'react'
import { cancelClaim } from '../../lib/api/claims'
import { codeOf } from '../../lib/api/client'

type CancelState = { status: 'idle' } | { status: 'submitting' } | { status: 'done' } | { status: 'failed'; message: string }

function cancelFailure(error: unknown): string {
  switch (codeOf(error)) {
    case 'claim.not_cancellable':
    case 'claim.conflict':
      return 'This claim can no longer be cancelled — a courier may have just been assigned.'
    case 'claim.not_found':
      return 'This claim is no longer on record.'
    case 'organization.not_active':
      return 'Your organization is read-only right now, so claims can’t be cancelled.'
    case 'antiforgery.invalid':
      return 'Your session changed in the meantime. Nothing was cancelled — please try again.'
    case 'network':
      return 'We couldn’t reach FoodLoop. Reload to see whether the claim was cancelled.'
    default:
      return 'Something went wrong on our side and nothing was cancelled. Please try again in a moment.'
  }
}

/** One cancel at a time, after an explicit confirmation, never retried; `refetch` always runs so the page shows server truth. */
export function useCancelClaim(refetch: () => void) {
  const [state, setState] = useState<CancelState>({ status: 'idle' })
  const inFlight = useRef(false)

  async function run(claimId: string, title: string) {
    if (inFlight.current || !window.confirm(`Cancel your claim on “${title}”? The donation will be released.`)) return
    inFlight.current = true
    setState({ status: 'submitting' })
    try {
      await cancelClaim(claimId)
      setState({ status: 'done' })
    } catch (error) {
      setState({ status: 'failed', message: cancelFailure(error) })
    } finally {
      inFlight.current = false
      refetch()
    }
  }

  return { state, run }
}
