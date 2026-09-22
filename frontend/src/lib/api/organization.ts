// The signed-in member's organization (R6.4), read-only. Mirrors OrganizationApiController.cs.
import type { OrganizationStatus, OrganizationType } from '../../types/organization'
import { api } from './client'

export type MyOrganization = {
  name: string
  address: string
  licenseNumber: string
  type: OrganizationType
  status: OrganizationStatus
  createdAt: string
  /** True while Suspended: history stays visible, nothing can change. */
  isReadOnly: boolean
}

export const getMyOrganization = (signal?: AbortSignal) => api<MyOrganization>('GET', '/organization', { signal })
