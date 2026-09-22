// Beneficiary marketplace + claim creation (R6.3). Mirrors FoodLoop.Web/Controllers/Api/MarketplaceApiController.cs
// and ClaimsApiController.cs. Categories are database records: their ids come from the API, never from the client.
import { api } from './client'

/** FoodLoop.Domain.Enums.QuantityUnit, serialized by name. */
export type QuantityUnit = 'Meals' | 'Kilograms' | 'Packages'

export type Category = { id: string; name: string }

export type MarketplaceItem = {
  id: string
  title: string
  category: Category
  quantity: number
  unit: QuantityUnit
  preparedAtUtc: string
  expiresAtUtc: string
  /** Always 'Available' in the marketplace. */
  status: 'Available'
  pickupAddress: string
  donorName: string
}

/** No total: the backend pages with hasPrevious/hasNext only. */
export type MarketplacePage = { items: MarketplaceItem[]; page: number; hasPrevious: boolean; hasNext: boolean }

export type MarketplaceDonation = Omit<MarketplaceItem, 'status'> & { description: string; storageInstructions: string }

export type MarketplaceQuery = { search?: string; categoryId?: string; page?: number }

export const getCategories = (signal?: AbortSignal) => api<Category[]>('GET', '/categories', { signal })

export function getMarketplace({ search, categoryId, page }: MarketplaceQuery, signal?: AbortSignal) {
  const params = new URLSearchParams()
  if (search?.trim()) params.set('search', search.trim())
  if (categoryId) params.set('categoryId', categoryId)
  if (page && page > 1) params.set('page', String(page))
  const query = params.toString()
  return api<MarketplacePage>('GET', `/marketplace${query ? `?${query}` : ''}`, { signal })
}

/** 404 `donation.not_found` when the donation is not currently claimable (missing, expired, claimed…). */
export const getMarketplaceDonation = (id: string, signal?: AbortSignal) =>
  api<MarketplaceDonation>('GET', `/marketplace/${encodeURIComponent(id)}`, { signal })

/** One attempt, never retried. Failures: 404 donation.not_found, 409 claim.not_available / claim.expired / claim.conflict, 403 organization.not_active. */
export const createClaim = (donationId: string) => api<{ claimId: string }>('POST', '/claims', { body: { donationId } })
