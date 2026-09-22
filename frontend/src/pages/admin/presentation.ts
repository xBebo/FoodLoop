// Admin presentation: tones, labels, formatting. Reads what the API says; decides nothing.
import { useSearchParams } from 'react-router-dom'
import type { StatusTone } from '../../components/ui/StatusChip'
import type { OrganizationStatus } from '../../types/organization'

export const ORG_STATUS_TONE: Record<OrganizationStatus, StatusTone> = {
  Active: 'success',
  Pending: 'warning',
  Suspended: 'danger',
  Rejected: 'neutral',
}

/** Which admin action a row offers (presentation only; the backend re-checks the status on every command). */
export const ORG_ACTION: Partial<Record<OrganizationStatus, 'Suspend' | 'Reactivate'>> = {
  Active: 'Suspend',
  Suspended: 'Reactivate',
}

const dateFmt = new Intl.DateTimeFormat('en-US', { day: 'numeric', month: 'short', year: 'numeric' })
export const formatDate = (iso: string) => dateFmt.format(new Date(iso))

/** "2026-09-22 14:03:11" — audit timestamps read in UTC, second precision. */
export const formatUtc = (iso: string) => iso.slice(0, 19).replace('T', ' ')

/** "14:03" in UTC, for compact activity rows. */
export const formatUtcTime = (iso: string) => iso.slice(11, 16)

/** Whole days since an ISO timestamp (0 = today). */
export const daysSince = (iso: string, now = Date.now()) => Math.floor((now - new Date(iso).getTime()) / 86_400_000)

export const pad2 = (n: number) => String(n).padStart(2, '0')

/** Filters kept in the URL (as Marketplace does), so back/forward and reloads restore them. */
export function useQueryParams() {
  const [params, setParams] = useSearchParams()
  function update(patch: Record<string, string | null>) {
    // Read the live URL: the router's `prev` can be stale when two updates land in quick succession.
    const next = new URLSearchParams(window.location.search)
    for (const [key, value] of Object.entries(patch)) {
      if (value) next.set(key, value)
      else next.delete(key)
    }
    setParams(next, { replace: true, preventScrollReset: true })
  }
  return [params, update] as const
}
