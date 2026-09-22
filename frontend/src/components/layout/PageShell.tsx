import { motion, useReducedMotion } from 'framer-motion'
import { useEffect, type ReactNode } from 'react'
import { Outlet, ScrollRestoration, useLocation, useMatches } from 'react-router-dom'
import { BARE_PATHS, PAGE_TITLES } from '../../app/routes'
import { duration, ease } from '../../lib/motion'
import { SiteFooter } from './SiteFooter'
import { SiteHeader } from './SiteHeader'
import './layout.css'

type ShellProps = {
  /** Public site header by default; the workspace passes its product navigation. */
  header?: ReactNode
  footer?: ReactNode
}

// Module scope, not component state: see the comment on the focus effect below.
let hasRenderedOnce = false

/**
 * Layout route: skip link, sticky header, <main> with the page transition, footer.
 * Transition is enter-only (no exit / wait), so navigation is never delayed.
 * Title comes from the deepest route `handle.title`, falling back to PAGE_TITLES.
 */
export function PageShell({ header = <SiteHeader />, footer = <SiteFooter /> }: ShellProps) {
  const { pathname, hash } = useLocation()
  const reduced = useReducedMotion()
  const handleTitle = (useMatches().at(-1)?.handle as { title?: string } | undefined)?.title

  useEffect(() => {
    document.title = handleTitle ?? PAGE_TITLES[pathname] ?? 'FoodLoop'
  }, [pathname, handleTitle])

  // After in-app navigation, move focus to <main> so screen readers start at the new page.
  // Hash targets are handled by SectionLink / ScrollRestoration instead.
  // The "first render" flag lives at module scope (not a ref): the public site and the workspace
  // are separate top-level routes, each with their own <PageShell>, so crossing between them
  // unmounts one instance and mounts another. A per-instance ref would look like a fresh first
  // render every time and skip the focus move; the module-level flag survives that remount.
  useEffect(() => {
    if (!hasRenderedOnce) {
      hasRenderedOnce = true
      return
    }
    if (!hash) document.getElementById('main')?.focus({ preventScroll: true })
  }, [pathname, hash])

  return (
    <div className="page-shell">
      <a className="skip-link" href="#main">
        Skip to content
      </a>
      {header}
      <main id="main" tabIndex={-1} className="page-shell__main">
        <motion.div
          key={pathname}
          className="page-transition"
          initial={reduced ? false : { opacity: 0, y: 10 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ duration: duration.page, ease: ease.out }}
        >
          <Outlet />
        </motion.div>
      </main>
      {!BARE_PATHS.includes(pathname) && footer}
      <ScrollRestoration />
    </div>
  )
}
