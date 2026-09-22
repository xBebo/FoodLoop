// Organization enums, exactly as the backend serializes them (FoodLoop.Domain.Enums).
import type { OrganizationStatus, OrganizationType } from '../lib/api/auth'

export type { OrganizationStatus, OrganizationType }

export const ORGANIZATION_STATUSES = ['Active', 'Pending', 'Suspended', 'Rejected'] as const satisfies readonly OrganizationStatus[]

export const ORGANIZATION_TYPE_LABELS: Record<OrganizationType, string> = {
  Donor: 'Donor',
  Beneficiary: 'Beneficiary',
}
