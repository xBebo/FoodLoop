// Admin workspace (R6.6). Mirrors AdminApiController.cs. No actor ids, audit Details or courier emails ever arrive here.
import type { ClaimStatus } from '../../types/claim'
import type { OrganizationStatus, OrganizationType } from '../../types/organization'
import { api } from './client'

/** The five counts the backend computes. Nothing else is aggregated. */
export type DashboardSummary = {
  pendingOrganizations: number
  availableDonations: number
  closedDeliveries: number
  cancelledClaims: number
  expiredDonations: number
}

export type AdminOrganization = {
  id: string
  name: string
  type: OrganizationType
  licenseNumber: string
  status: OrganizationStatus
  createdAtUtc: string
}

export type Paged<T> = { items: T[]; page: number; hasPrevious: boolean; hasNext: boolean; totalCount: number }

export type PendingOrganization = { id: string; name: string; type: OrganizationType; licenseNumber: string; createdAtUtc: string }

export type AssignableClaim = {
  claimId: string
  donationTitle: string
  donorName: string
  beneficiaryName: string
  pickupAddress: string
  expiresAtUtc: string
  claimedAtUtc: string
  status: Extract<ClaimStatus, 'Booked' | 'PickupPending'>
  /** PickupPending claims already have a courier; assigning again reassigns. */
  hasCourier: boolean
}

/** `id` is only for submitting the assignment; `name` is the courier's display name. */
export type CourierOption = { id: string; name: string }

/** Every action the backend records (IAuditService.Record call sites). */
export const AUDIT_ACTIONS = [
  'OrganizationApproved',
  'OrganizationRejected',
  'OrganizationSuspended',
  'OrganizationReactivated',
  'OrganizationUpdated',
  'DonationCreated',
  'DonationUpdated',
  'DonationPublished',
  'DonationExpired',
  'ClaimCreated',
  'ClaimCancelled',
  'CourierAssigned',
  'HandoverCodeIssued',
  'PickupVerified',
  'DeliveryVerified',
] as const

export type AuditEntry = {
  id: string
  action: string
  actorName: string
  entityType: string
  entityId: string
  timestampUtc: string
}

export type AuditQuery = { page: number; action?: string; actor?: string; fromUtc?: string; toUtc?: string }

const query = (params: Record<string, string | number | undefined>) => {
  const q = new URLSearchParams()
  for (const [k, v] of Object.entries(params)) if (v !== undefined && v !== '') q.set(k, String(v))
  const s = q.toString()
  return s ? `?${s}` : ''
}

export const getDashboard = (signal?: AbortSignal) => api<DashboardSummary>('GET', '/admin/dashboard', { signal })

export const getOrganizations = (status: OrganizationStatus | undefined, page: number, signal?: AbortSignal) =>
  api<Paged<AdminOrganization>>('GET', `/admin/organizations${query({ status, page })}`, { signal })

export const getPendingOrganizations = (signal?: AbortSignal) =>
  api<PendingOrganization[]>('GET', '/admin/organizations/pending', { signal })

/** 204 on success; 409 organization.invalid_state / organization.conflict, 404 organization.not_found. */
export const changeOrganization = (id: string, action: 'suspend' | 'reactivate' | 'approve' | 'reject') =>
  api<void>('POST', `/admin/organizations/${encodeURIComponent(id)}/${action}`)

export const getAssignableClaims = (signal?: AbortSignal) => api<AssignableClaim[]>('GET', '/admin/claims/assignable', { signal })

export const getCouriers = (signal?: AbortSignal) => api<CourierOption[]>('GET', '/admin/couriers', { signal })

/** 400 courier.invalid, 409 claim.not_assignable / claim.conflict, 404 claim.not_found. */
export const assignCourier = (claimId: string, courierUserId: string) =>
  api<void>('POST', `/admin/claims/${encodeURIComponent(claimId)}/courier`, { body: { courierUserId } })

/** 400 audit.invalid_range when fromUtc is not before toUtc. */
export const getAudit = (q: AuditQuery, signal?: AbortSignal) =>
  api<Paged<AuditEntry>>('GET', `/admin/audit${query(q)}`, { signal })
