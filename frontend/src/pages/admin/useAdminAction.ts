import { useRef, useState } from 'react'
import { codeOf } from '../../lib/api/client'

export type ActionNotice = { id: string; ok: boolean; text: string }

// Copy for a failed admin command, from its stable code only.
const FAILURE: Record<string, string> = {
  'organization.invalid_state': 'already changed — its status no longer allows that action.',
  'organization.conflict': 'was just changed by another administrator. The list has been refreshed.',
  'organization.not_found': 'no longer exists.',
  'claim.not_assignable': 'can no longer be assigned — it may have expired, been cancelled or already been picked up.',
  'claim.conflict': 'was just changed by someone else. The list has been refreshed.',
  'claim.not_found': 'no longer exists.',
  'courier.invalid': 'can’t use that courier. Choose another from the list.',
  'antiforgery.invalid': 'wasn’t changed because your session changed. Please try again.',
  network: 'may not have been changed — FoodLoop couldn’t be reached. Check the refreshed list before trying again.',
}

/**
 * One admin command at a time: buttons disable while it runs, it is never retried, and `refetch` always runs after
 * so the page shows server truth instead of a locally faked update.
 */
export function useAdminAction(refetch: () => void) {
  const [pendingId, setPendingId] = useState<string | null>(null)
  const [notice, setNotice] = useState<ActionNotice | null>(null)
  const inFlight = useRef(false)

  async function run(id: string, name: string, command: () => Promise<unknown>, success: string) {
    if (inFlight.current) return
    inFlight.current = true
    setPendingId(id)
    setNotice(null)
    try {
      await command()
      setNotice({ id, ok: true, text: `${name} ${success}` })
    } catch (error) {
      setNotice({ id, ok: false, text: `${name} ${FAILURE[codeOf(error)] ?? 'wasn’t changed — something went wrong on our side. Please try again.'}` })
    } finally {
      inFlight.current = false
      setPendingId(null)
      refetch()
    }
  }

  return { pendingId, busy: pendingId !== null, notice, run }
}
