// Claim status, exactly as the backend serializes FoodLoop.Domain.Enums.ClaimStatus.
// The handover lifecycle only produces Booked → PickupPending → InTransit → Closed (or Cancelled);
// the other values exist in the enum but are never written.

export const CLAIM_STATUSES = [
  'Booked',
  'PickupPending',
  'PickedUp',
  'InTransit',
  'Delivered',
  'Closed',
  'Cancelled',
  'Failed',
] as const
export type ClaimStatus = (typeof CLAIM_STATUSES)[number]
