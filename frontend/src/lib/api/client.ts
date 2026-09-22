// The only path from React to the ASP.NET Core API. Same-origin /api (Vite proxies it in development), the HttpOnly
// Identity cookie does authentication, and unsafe methods carry the antiforgery token in X-XSRF-TOKEN.
// The token lives in memory only: never localStorage, sessionStorage, a URL or a JavaScript-written cookie.

/** Structured API failure. `code` is the stable machine-readable value; UI copy is chosen from it, never from server text. */
export class ApiError extends Error {
  readonly status: number
  readonly code: string
  readonly title: string
  /** Field validation messages keyed by camelCase field name. */
  readonly errors: Record<string, string[]>
  /** Only set where the backend sends a service's own user-facing rule text (e.g. `donation.invalid`). */
  readonly detail?: string

  constructor(status: number, code: string, title: string, errors: Record<string, string[]> = {}, detail?: string) {
    super(title)
    this.name = 'ApiError'
    this.status = status
    this.code = code
    this.title = title
    this.errors = errors
    this.detail = detail
  }
}

/** The stable code of a failure ('' for anything that isn't an ApiError). UI copy branches on this. */
export const codeOf = (error: unknown) => (error instanceof ApiError ? error.code : '')

type Method = 'GET' | 'POST' | 'PUT' | 'DELETE'
type Options = { body?: unknown; signal?: AbortSignal }

let token: string | null = null
let pendingToken: Promise<string> | null = null
let sessionExpired: (() => void) | null = null

/**
 * One listener (the SessionProvider) hears about a protected request answered 401, i.e. the cookie is gone.
 * Kept as a plain callback so this module stays usable outside React and never imports it. Returns an unsubscribe.
 */
export function onSessionExpired(listener: () => void) {
  sessionExpired = listener
  return () => {
    if (sessionExpired === listener) sessionExpired = null
  }
}

/** The token is bound to the current identity: discard it whenever the session changes (login, logout). */
export function discardAntiforgeryToken() {
  token = null
  pendingToken = null
}

export function ensureAntiforgeryToken(): Promise<string> {
  if (token) return Promise.resolve(token)
  const request = (pendingToken ??= send<{ token: string }>('GET', '/auth/antiforgery').then((r) => {
    // Ignore a response that arrived after the token was discarded.
    if (pendingToken === request) token = r.token
    return r.token
  }))
  return request.finally(() => {
    if (pendingToken === request) pendingToken = null
  })
}

/** One request, never retried automatically. Resolves with the parsed JSON body (undefined for 201/204 without a body). */
export async function api<T>(method: Method, path: string, { body, signal }: Options = {}): Promise<T> {
  const headers: Record<string, string> = {}
  if (method !== 'GET') headers['X-XSRF-TOKEN'] = await ensureAntiforgeryToken()
  return send<T>(method, path, { body, signal }, headers)
}

async function send<T>(method: Method, path: string, { body, signal }: Options = {}, headers: Record<string, string> = {}): Promise<T> {
  let response: Response
  try {
    response = await fetch(`/api${path}`, {
      method,
      credentials: 'include',
      signal,
      headers: { Accept: 'application/json', ...(body !== undefined && { 'Content-Type': 'application/json' }), ...headers },
      body: body === undefined ? undefined : JSON.stringify(body),
    })
  } catch (error) {
    if (signal?.aborted) throw error
    throw new ApiError(0, 'network', 'Network error')
  }

  const text = await response.text()
  let data: unknown
  try {
    data = text ? JSON.parse(text) : undefined
  } catch {
    data = undefined
  }
  if (response.ok) return data as T
  // /auth/* answer 401 as a normal outcome (wrong password, logout of a dead session) and handle it themselves.
  // Anything else means the session ended: drop the identity-bound token and tell the listener. The request is not retried.
  if (response.status === 401 && !path.startsWith('/auth/')) {
    discardAntiforgeryToken()
    sessionExpired?.()
  }

  const problem = (data ?? {}) as { code?: unknown; title?: unknown; errors?: unknown; detail?: unknown }
  const code = typeof problem.code === 'string' ? problem.code : 'error'
  if (code === 'antiforgery.invalid') discardAntiforgeryToken() // The next unsafe request fetches a fresh one.
  throw new ApiError(
    response.status,
    code,
    typeof problem.title === 'string' ? problem.title : response.statusText,
    fieldErrors(problem.errors),
    typeof problem.detail === 'string' ? problem.detail : undefined,
  )
}

function fieldErrors(errors: unknown): Record<string, string[]> {
  if (!errors || typeof errors !== 'object') return {}
  return Object.fromEntries(
    Object.entries(errors as Record<string, unknown>)
      .filter(([, v]) => Array.isArray(v))
      .map(([k, v]) => [k.charAt(0).toLowerCase() + k.slice(1), (v as unknown[]).map(String)]),
  )
}
