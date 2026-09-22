// Auth/session endpoints (R6.2). Mirrors FoodLoop.Web/Controllers/Api/AuthApiController.cs.
import { api, discardAntiforgeryToken, ensureAntiforgeryToken } from './client'

export type Role = 'Admin' | 'Donor' | 'Beneficiary' | 'Courier'
export type OrganizationType = 'Donor' | 'Beneficiary'
export type OrganizationStatus = 'Pending' | 'Active' | 'Rejected' | 'Suspended'

export type AuthenticatedSession = {
  isAuthenticated: true
  displayName: string
  roles: Role[]
  /** Null for Admin and Courier accounts. */
  organization: { name: string; type: OrganizationType; status: OrganizationStatus } | null
}
export type Session = { isAuthenticated: false } | AuthenticatedSession

export type RegisterRequest = {
  organizationName: string
  licenseNumber: string
  organizationType: OrganizationType
  email: string
  password: string
}

export const getSession = (signal?: AbortSignal) => api<Session>('GET', '/auth/session', { signal })

/** Signs in, then swaps the anonymous antiforgery token for one bound to the new identity. */
export async function login(email: string, password: string) {
  const session = await api<AuthenticatedSession>('POST', '/auth/login', { body: { email, password } })
  await rotateToken()
  return session
}

/** Signs out, then swaps the authenticated antiforgery token for an anonymous one. */
export async function logout() {
  await api<void>('POST', '/auth/logout')
  await rotateToken()
}

/** Creates a Pending organization. Never signs in: an administrator approves the organization first. */
export const register = (request: RegisterRequest) => api<void>('POST', '/auth/register', { body: request })

async function rotateToken() {
  discardAntiforgeryToken()
  // Best effort: if this fails, the next unsafe request fetches the token itself.
  await ensureAntiforgeryToken().catch(() => undefined)
}
