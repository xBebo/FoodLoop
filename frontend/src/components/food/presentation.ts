// UI presentation for donation enums: labels, tones, groupings. No lifecycle rules live here.
import { Carrot, CookingPot, Milk, Package, ShoppingBasket, Wheat, type LucideIcon } from 'lucide-react'
import type { CategoryMediaSlot } from '../../data/categoryMedia'
import type { Category, MarketplaceItem, QuantityUnit } from '../../lib/api/marketplace'
import type { DonationStatus } from '../../types/donation'
import type { StatusTone } from '../ui/StatusChip'

export type StatusPhase = 'draft' | 'open' | 'progress' | 'done' | 'ended'

export const STATUS_META: Record<DonationStatus, { label: string; tone: StatusTone; phase: StatusPhase }> = {
  Draft: { label: 'Draft', tone: 'neutral', phase: 'draft' },
  Available: { label: 'Available', tone: 'success', phase: 'open' },
  Claimed: { label: 'Claimed', tone: 'info', phase: 'progress' },
  PickupPending: { label: 'Pickup pending', tone: 'warning', phase: 'progress' },
  PickedUp: { label: 'Picked up', tone: 'info', phase: 'progress' },
  InTransit: { label: 'In transit', tone: 'info', phase: 'progress' },
  Delivered: { label: 'Delivered', tone: 'complete', phase: 'done' },
  Closed: { label: 'Closed', tone: 'complete', phase: 'done' },
  Expired: { label: 'Expired', tone: 'danger', phase: 'ended' },
  Cancelled: { label: 'Cancelled', tone: 'neutral', phase: 'ended' },
  Failed: { label: 'Failed', tone: 'danger', phase: 'ended' },
}

/** How a category looks: its label, media slot and icon. Presentation only — never the taxonomy. */
export type CategoryVisual = { label: string; slot: CategoryMediaSlot; icon: LucideIcon }

// Real categories are database records. Known names get a matching visual; any other (new) category gets the
// generic one, so adding a category never needs a frontend change.
const VISUAL_BY_NAME: Record<string, Omit<CategoryVisual, 'label'>> = {
  'prepared meals': { slot: 'preparedMeals', icon: CookingPot },
  produce: { slot: 'produce', icon: Carrot },
  'packaged food': { slot: 'pantry', icon: Package },
  bakery: { slot: 'bakery', icon: Wheat },
  dairy: { slot: 'dairy', icon: Milk },
}
const GENERIC_VISUAL = { slot: 'mixed', icon: ShoppingBasket } as const

export const categoryVisual = ({ name }: Pick<Category, 'name'>): CategoryVisual => ({
  label: name,
  ...(VISUAL_BY_NAME[name.trim().toLowerCase()] ?? GENERIC_VISUAL),
})

const UNIT_LABELS: Record<QuantityUnit, [one: string, many: string]> = {
  Meals: ['meal', 'meals'],
  Kilograms: ['kg', 'kg'],
  Packages: ['package', 'packages'],
}

/** "12.5 kg", "1 meal", "3 packages" — from the backend's canonical unit name. */
export const formatUnitQuantity = (quantity: number, unit: QuantityUnit) =>
  `${quantity.toLocaleString('en-US', { maximumFractionDigits: 2 })} ${UNIT_LABELS[unit]?.[quantity === 1 ? 0 : 1] ?? unit}`

/** What a listing card shows. Built from API data (listingOf) or, for the donor form's preview, from the form. */
export type Listing = {
  id: string
  title: string
  description?: string
  donorName: string
  quantityLabel: string
  pickupAddress: string
  /** ISO timestamp; empty while a draft has none. */
  expiresAt: string
  status: DonationStatus
  category: CategoryVisual
  imageUrl?: string
}

export const listingOf = (d: MarketplaceItem): Listing => ({
  id: d.id,
  title: d.title,
  donorName: d.donorName,
  quantityLabel: formatUnitQuantity(d.quantity, d.unit),
  pickupAddress: d.pickupAddress,
  expiresAt: d.expiresAtUtc,
  status: d.status,
  category: categoryVisual(d.category),
})

/** Neighbourhood shown on cards: the last part of the pickup address ("14 Harbour Street, Harbourside" → "Harbourside"). */
export const pickupAreaOf = (d: { pickupAddress: string }) => d.pickupAddress.split(',').at(-1)?.trim() || '—'
