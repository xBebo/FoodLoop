import { useEffect, useMemo, useState, type ReactNode } from 'react'
import { getSession, login as apiLogin, logout as apiLogout } from '../api/auth'
import { discardAntiforgeryToken, onSessionExpired } from '../api/client'
import { SessionContext, type SessionContextValue, type SessionState } from './context'

export function SessionProvider({ children }: { children: ReactNode }) {
  const [state, setState] = useState<SessionState>({ status: 'loading' })

  useEffect(() => {
    const controller = new AbortController()
    getSession(controller.signal).then(
      (s) => setState(s.isAuthenticated ? { status: 'authenticated', session: s } : { status: 'anonymous' }),
      // Unreachable API: the public site still works and protected routes send the visitor to Login.
      () => !controller.signal.aborted && setState({ status: 'anonymous' }),
    )
    return () => controller.abort()
  }, [])

  // Simultaneous 401s coalesce here: only the first one finds an authenticated state; the rest return the same
  // state object, so React skips the update. The route guard then sends the visitor to Login exactly once.
  useEffect(
    () => onSessionExpired(() => setState((s) => (s.status === 'authenticated' ? { status: 'anonymous', expired: true } : s))),
    [],
  )

  const value = useMemo<SessionContextValue>(
    () => ({
      state,
      async login(email, password) {
        const session = await apiLogin(email, password)
        setState({ status: 'authenticated', session })
        return session
      },
      async logout() {
        try {
          await apiLogout()
        } catch (error) {
          // Already signed out (expired cookie, another tab) counts as done; anything else is reported.
          discardAntiforgeryToken()
          const current = await getSession().catch(() => null)
          if (current?.isAuthenticated !== false) throw error
        }
        setState({ status: 'anonymous' })
      },
    }),
    [state],
  )

  return <SessionContext.Provider value={value}>{children}</SessionContext.Provider>
}
