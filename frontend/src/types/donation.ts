// Donation status, exactly as the backend serializes FoodLoop.Domain.Enums.DonationStatus.
// Categories are database records ({ id, name }) and units are QuantityUnit — both live in lib/api/marketplace.ts.

export const DONATION_STATUSES = [
  'Draft',
  'Available',
  'Claimed',
  'PickupPending',
  'PickedUp',
  'InTransit',
  'Delivered',
  'Closed',
  'Expired',
  'Cancelled',
  'Failed',
] as const
export type DonationStatus = (typeof DONATION_STATUSES)[number]
