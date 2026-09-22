// Beneficiary claims (R6.5). Mirrors ClaimsApiController.cs → ClaimService read models.
// The timeline and canCancel come from the server; React never derives lifecycle events or permissions from status.
import type { ClaimStatus } from '../../types/claim'
import type { DonationStatus } from '../../types/donation'
import { api } from './client'
import type { QuantityUnit } from './marketplace'

export type ClaimSummary = {
  claimId: string
  donationId: string
  donationTitle: string
  quantity: number
  unit: QuantityUnit
  pickupAddress: string
  expiresAtUtc: string
  claimStatus: ClaimStatus
  claimedAtUtc: string
  canCancel: boolean
}

/** No total by design: the backend only says whether another page exists. */
export type ClaimsPage = { items: ClaimSummary[]; page: number; hasNext: boolean }

/** Server-defined ClaimTimelineEvent kinds (ClaimService.BuildTimeline). */
export type TimelineKind =
  | 'Claimed'
  | 'CourierAssigned'
  | 'CourierReassigned'
  | 'Cancelled'
  | 'PickupVerified'
  | 'DeliveryVerified'
  | 'Closed'

export type TimelineEvent = { kind: TimelineKind; label: string; atUtc: string }

export type ClaimDetails = {
  claimId: string
  status: ClaimStatus
  claimedAtUtc: string
  canCancel: boolean
  /** Null while unassigned. Never an email or id. */
  courierDisplayName: string | null
  donation: {
    title: string
    categoryName: string
    quantity: number
    unit: QuantityUnit
    preparedAtUtc: string
    expiresAtUtc: string
    pickupAddress: string
    storageInstructions: string
    description: string
    status: DonationStatus
  }
  /** Persisted evidence only, oldest first. */
  timeline: TimelineEvent[]
}

export const CLAIMS_PAGE_SIZE = 20

export const getMyClaims = (page: number, signal?: AbortSignal) =>
  api<ClaimsPage>('GET', `/claims?page=${page}&pageSize=${CLAIMS_PAGE_SIZE}`, { signal })

/** 404 claim.not_found for a missing or another organization's claim. */
export const getClaim = (id: string, signal?: AbortSignal) => api<ClaimDetails>('GET', `/claims/${encodeURIComponent(id)}`, { signal })

/** 409 claim.not_cancellable / claim.conflict, 403 organization.not_active. */
export const cancelClaim = (id: string) => api<void>('POST', `/claims/${encodeURIComponent(id)}/cancel`)
