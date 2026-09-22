// Courier tasks + handover codes (R6.7). Mirrors CourierApiController.cs and HandoverApiController.cs.
// nextStep and canIssue are server-derived; React never computes them from status.
import type { ClaimStatus } from '../../types/claim'
import { api } from './client'

export type HandoverType = 'Pickup' | 'Delivery'

/** CourierService.NextStep: the only steps the handover lifecycle produces. */
export type CourierNextStep = 'None' | 'VerifyPickup' | 'VerifyDelivery' | 'Completed'

export type CourierTask = {
  claimId: string
  donationTitle: string
  status: ClaimStatus
  nextStep: CourierNextStep
  donorName: string
  beneficiaryName: string
  pickupAddress: string
  expiresAtUtc: string
}

export type HandoverEvidence = { type: HandoverType; verifiedAtUtc: string }

export type CourierTaskDetails = CourierTask & { evidence: HandoverEvidence[] }

export const getCourierTasks = (signal?: AbortSignal) => api<CourierTask[]>('GET', '/courier/tasks', { signal })

/** 404 task.not_found for a missing task or one assigned to another courier. */
export const getCourierTask = (id: string, signal?: AbortSignal) =>
  api<CourierTaskDetails>('GET', `/courier/tasks/${encodeURIComponent(id)}`, { signal })

/** 400 handover.invalid_code, 409 handover.invalid_state / task.conflict, 404 task.not_found. Whitespace is ignored server-side. */
export const verifyHandover = (id: string, type: HandoverType, code: string) =>
  api<void>('POST', `/courier/tasks/${encodeURIComponent(id)}/verify`, { body: { type, code } })

export type HandoverTask = { claimId: string; donationTitle: string; status: ClaimStatus; type: HandoverType; canIssue: boolean }

/**
 * A one-time issue result. The raw code is never stored server-side and cannot be fetched again: keep it in memory only
 * (never storage, URLs or logs). Issuing again revokes the previous code.
 */
export type IssuedCode = { code: string; type: HandoverType; expiresAtUtc: string; qrSvg: string }

/** 403 organization.not_active. */
export const getHandoverTasks = (signal?: AbortSignal) => api<HandoverTask[]>('GET', '/handover/tasks', { signal })

/** 409 handover.not_ready, 404 claim.not_found. */
export const issueHandoverCode = (claimId: string, type: HandoverType) =>
  api<IssuedCode>('POST', `/handover/${encodeURIComponent(claimId)}/codes`, { body: { type } })
