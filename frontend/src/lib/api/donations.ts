// Donor donations (R6.4). Mirrors FoodLoop.Web/Controllers/Api/DonationsApiController.cs.
// canEdit / canPublish are server-derived presentation flags; the backend re-checks every rule on submit.
import type { DonationStatus } from '../../types/donation'
import { api } from './client'
import type { Category, QuantityUnit } from './marketplace'

export type DonationItem = {
  id: string
  title: string
  category: Category
  quantity: number
  unit: QuantityUnit
  preparedAtUtc: string
  expiresAtUtc: string
  pickupAddress: string
  status: DonationStatus
  canEdit: boolean
  canPublish: boolean
}

export type DonationDetails = DonationItem & { description: string; storageInstructions: string }

/** Draft-only edit data. `version` is an opaque concurrency value: send it back unchanged. */
export type DonationForEdit = {
  id: string
  categoryId: string
  title: string
  description: string
  quantity: number
  unit: QuantityUnit
  preparedAtUtc: string
  expiresAtUtc: string
  storageInstructions: string
  pickupAddress: string
  version: string
}

export type DonationInput = {
  categoryId: string
  title: string
  description: string
  quantity: number
  unit: QuantityUnit
  /** ISO-8601 with offset. */
  preparedAt: string
  expiresAt: string
  storageInstructions: string
  pickupAddress: string
}

export const getMyDonations = (signal?: AbortSignal) => api<DonationItem[]>('GET', '/donations', { signal })

/** 404 donation.not_found for a missing or another organization's donation. */
export const getDonation = (id: string, signal?: AbortSignal) =>
  api<DonationDetails>('GET', `/donations/${encodeURIComponent(id)}`, { signal })

/** 409 donation.not_editable once the donation is no longer a Draft. */
export const getDonationForEdit = (id: string, signal?: AbortSignal) =>
  api<DonationForEdit>('GET', `/donations/${encodeURIComponent(id)}/edit`, { signal })

/** Creates a Draft. Failures: 400 donation.invalid (detail is safe copy) / validation, 403 organization.not_active. */
export const createDonation = (input: DonationInput) => api<{ id: string }>('POST', '/donations', { body: input })

/** 409 donation.stale when someone else saved first; 409 donation.invalid_state when it is no longer a Draft. */
export const updateDonation = (id: string, input: DonationInput, version: string) =>
  api<void>('PUT', `/donations/${encodeURIComponent(id)}`, { body: { ...input, version } })

/** 409 donation.invalid_state (not a Draft, or already expired). */
export const publishDonation = (id: string) => api<void>('POST', `/donations/${encodeURIComponent(id)}/publish`)
