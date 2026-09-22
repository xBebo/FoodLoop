// Route guards for UX only: they decide what to render, not what a user may do. The API authorizes every request.
import { ArrowRight } from 'lucide-react'
import { Navigate, Outlet, useLocation } from 'react-router-dom'
import { FoodLoopMark } from '../components/brand/FoodLoopMark'
import { Button } from '../components/ui/Button'
import { SectionEyebrow } from '../components/ui/SectionEyebrow'
import { homeOf, returnTo, useSession, workspaceRoleOf, type LoginState } from '../lib/session/context'
import { PATHS, type WorkspaceRole } from './routes'

export function SessionLoading() {
  return (
    <div className="session-loading" role="status" aria-busy="true">
      <FoodLoopMark className="session-loading__mark" />
      <span className="visually-hidden">Loading your workspace…</span>
    </div>
  )
}

/** Workspace gate: anonymous visitors go to Login. An expired session remembers the page it was on. */
export function RequireAuthenticated() {
  const { state } = useSession()
  const location = useLocation()
  if (state.status === 'loading') return <SessionLoading />
  if (state.status === 'anonymous') {
    const login: LoginState | undefined = state.expired ? { from: location, expired: true } : undefined
    return <Navigate to={PATHS.login} replace state={login} />
  }
  return <Outlet />
}

/** Pages of one workspace. Another role sees a calm "not in your workspace" page, never a redirect loop. */
export function RequireRole({ roles }: { roles: WorkspaceRole[] }) {
  const { state } = useSession()
  if (state.status !== 'authenticated') return <Navigate to={PATHS.login} replace />
  const role = workspaceRoleOf(state.session)
  if (role && roles.includes(role)) return <Outlet />
  return (
    <div className="container ws-page">
      <header className="ws-intro">
        <SectionEyebrow>Not available</SectionEyebrow>
        <h1 className="ws-intro__title">
          This page isn’t part of <em>your workspace.</em>
        </h1>
        <p className="t-lead ws-intro__lead">Your account works from a different set of pages.</p>
        <div className="ws-intro__actions">
          <Button to={homeOf(state.session)} iconEnd={<ArrowRight />}>
            Go to my workspace
          </Button>
        </div>
      </header>
    </div>
  )
}

/** Login and Register: a signed-in visitor goes straight to their workspace. */
export function RedirectIfAuthenticated() {
  const { state } = useSession()
  const location = useLocation()
  return state.status === 'authenticated' ? <Navigate to={returnTo(location.state, state.session)} replace /> : <Outlet />
}
