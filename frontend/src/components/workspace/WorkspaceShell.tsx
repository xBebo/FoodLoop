import { motion } from 'framer-motion'
import { LogOut } from 'lucide-react'
import { useEffect, useRef, useState } from 'react'
import { Link, useLocation, useMatch, useNavigate } from 'react-router-dom'
import { PATHS, WORKSPACE_ROLES, type WorkspaceRole } from '../../app/routes'
import type { AuthenticatedSession } from '../../lib/api/auth'
import { spring } from '../../lib/motion'
import { useSession, workspaceRoleOf } from '../../lib/session/context'
import { FoodLoopWordmark } from '../brand/FoodLoopMark'
import { PageShell } from '../layout/PageShell'
import { WorkspaceRoleContext } from './role'
import './workspace.css'

/** Layout route for signed-in product pages: same shell mechanics as the public site, product chrome. */
export function WorkspaceShell() {
  const { state } = useSession()
  // Rendered only under RequireAuthenticated, so the session is always present here.
  const session = (state as { session: AuthenticatedSession }).session
  const role = workspaceRoleOf(session) ?? 'beneficiary'
  // /donations/:id is a Marketplace listing for a beneficiary. (/donations/new also matches, but is donor-only.)
  const listing = useMatch(PATHS.donation) && role === 'beneficiary'

  return (
    <WorkspaceRoleContext.Provider value={role}>
      <PageShell
        header={<WorkspaceHeader role={role} session={session} current={listing ? PATHS.marketplace : undefined} />}
        footer={<WorkspaceFooter />}
      />
    </WorkspaceRoleContext.Provider>
  )
}

function WorkspaceHeader({ role, session, current: forced }: { role: WorkspaceRole; session: AuthenticatedSession; current?: string }) {
  const { pathname } = useLocation()
  const navRef = useRef<HTMLElement>(null)
  const nav = WORKSPACE_ROLES.find((r) => r.id === role)!.nav
  // Deepest matching item wins: /admin/organizations/pending is "Pending requests", not "Overview".
  const current =
    forced ??
    nav
      .filter((i) => pathname === i.to || pathname.startsWith(`${i.to}/`))
      .reduce<string | undefined>((best, i) => (i.to.length > (best?.length ?? 0) ? i.to : best), undefined)
  const roleLabel = WORKSPACE_ROLES.find((r) => r.id === role)!.label
  const initials = session.displayName
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((w) => w[0].toUpperCase())
    .join('')

  // On the admin nav's narrow horizontally-scrolling strip, keep the active item in view on arrival.
  // A no-op everywhere else, since those navs never overflow their track.
  useEffect(() => {
    navRef.current?.querySelector<HTMLElement>('[aria-current="page"]')?.scrollIntoView({ block: 'nearest', inline: 'nearest' })
  }, [current])

  return (
    <header className={`ws-header ws-header--${role} on-dark grain`}>
      <div className="container ws-header__bar">
        <div className="ws-header__brand">
          <Link to={PATHS.home} className="ws-header__home" aria-label="FoodLoop home">
            <FoodLoopWordmark />
          </Link>
          <span className="ws-header__context t-label" aria-hidden="true">
            {roleLabel}
          </span>
        </div>

        <nav ref={navRef} className="ws-nav" aria-label="Workspace">
          <ul role="list" className="ws-nav__list">
            {nav.map((item) => (
              <li key={item.to}>
                <Link to={item.to} className="ws-nav__link" aria-current={current === item.to ? 'page' : undefined}>
                  {current === item.to && (
                    <motion.span layoutId="ws-nav-indicator" className="ws-nav__indicator" transition={spring.indicator} />
                  )}
                  <span className="ws-nav__label">{item.label}</span>
                </Link>
              </li>
            ))}
          </ul>
        </nav>

        {/* The signed-in account, from the real session. */}
        <div className="ws-profile">
          <span className="ws-profile__text">
            <span className="ws-profile__name">{session.displayName}</span>
            <span className="ws-profile__org">{session.organization?.name ?? roleLabel}</span>
          </span>
          <span className="ws-profile__avatar" aria-hidden="true">
            {initials}
          </span>
          <LogoutButton />
        </div>
      </div>
    </header>
  )
}

function LogoutButton() {
  const { logout } = useSession()
  const navigate = useNavigate()
  const [busy, setBusy] = useState(false)
  const [failed, setFailed] = useState(false)

  async function onClick() {
    setBusy(true)
    setFailed(false)
    try {
      await logout()
      navigate(PATHS.home, { replace: true })
    } catch {
      setFailed(true)
      setBusy(false)
    }
  }

  return (
    <>
      <button type="button" className="ws-logout" onClick={onClick} disabled={busy} aria-busy={busy || undefined}>
        <LogOut aria-hidden="true" />
        <span className="ws-logout__label">Log out</span>
      </button>
      <span role="alert" className="visually-hidden">
        {failed ? 'Logging out didn’t work. Please try again.' : ''}
      </span>
    </>
  )
}

function WorkspaceFooter() {
  return (
    <footer className="ws-footer">
      <div className="container ws-footer__inner">
        <p>
          <span className="ws-footer__dot" aria-hidden="true" />
          FoodLoop — rescuing surplus food, one verified handover at a time.
        </p>
        <Link to={PATHS.home} className="link-underline">
          Back to the public site
        </Link>
      </div>
    </footer>
  )
}
