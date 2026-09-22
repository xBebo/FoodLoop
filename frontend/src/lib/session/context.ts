// Real signed-in state from GET /api/auth/session. Held in React state only: a refresh recovers it from the
// HttpOnly Identity cookie, never from browser storage. Role checks here shape navigation — the API authorizes.
import { createContext, useContext } from 'react'
import type { Location } from 'react-router-dom'
import { PATHS, WORKSPACE_ROLES, type WorkspaceRole } from '../../app/routes'
import type { AuthenticatedSession } from '../api/auth'

export type SessionState =
  | { status: 'loading' }
  /** `expired`: the server ended a session React still believed in (a protected request answered 401). */
  | { status: 'anonymous'; expired?: boolean }
  | { status: 'authenticated'; session: AuthenticatedSession }

export type SessionContextValue = {
  state: SessionState
  login: (email: string, password: string) => Promise<AuthenticatedSession>
  logout: () => Promise<void>
}

export const SessionContext = createContext<SessionContextValue | null>(null)

export function useSession() {
  const value = useContext(SessionContext)
  if (!value) throw new Error('useSession must be used inside <SessionProvider>.')
  return value
}

/** The workspace a session belongs to (accounts hold one role). */
export const workspaceRoleOf = (session: AuthenticatedSession): WorkspaceRole | undefined =>
  WORKSPACE_ROLES.find((r) => session.roles.some((role) => role.toLowerCase() === r.id))?.id

export const homeOf = (session: AuthenticatedSession) =>
  WORKSPACE_ROLES.find((r) => r.id === workspaceRoleOf(session))?.home ?? PATHS.home

/** Router state handed to Login when a session expired: where the visitor was, to return there after signing in. */
export type LoginState = { from?: Pick<Location, 'pathname' | 'search'>; expired?: boolean }

/** Post-login destination: the page an expired session was on, else the account's workspace home. */
export function returnTo(state: unknown, session: AuthenticatedSession) {
  const from = (state as LoginState | null)?.from
  return from ? from.pathname + from.search : homeOf(session)
}
