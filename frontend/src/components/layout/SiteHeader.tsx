import { AnimatePresence, motion, useReducedMotion } from 'framer-motion'
import { ArrowRight, ArrowUpRight } from 'lucide-react'
import { useEffect, useRef, useState } from 'react'
import { Link, useLocation } from 'react-router-dom'
import { EXPLORE_FOOD_PATH, PATHS, PRIMARY_NAV, SECTIONS, type NavItem } from '../../app/routes'
import { cn } from '../../lib/cn'
import { homeOf, useSession } from '../../lib/session/context'
import { duration, ease, spring } from '../../lib/motion'
import { BotanicalCorner, BotanicalDecoration } from '../brand/Botanical'
import { FoodLoopWordmark } from '../brand/FoodLoopMark'
import { Button } from '../ui/Button'
import { NavItemLink } from './SectionLink'
import './layout.css'

const DESKTOP_QUERY = '(min-width: 1024px)'
const FOCUSABLE = 'a[href], button:not([disabled])'
// Desktop: Home-page links live in the centre pill; Login / Register become the action buttons.
const CENTER_NAV = PRIMARY_NAV.filter((item) => item.to === PATHS.home)

export function SiteHeader() {
  const { pathname } = useLocation()
  const { state } = useSession()
  // Signed in: one "Open workspace" action replaces Login / Register.
  const workspace = state.status === 'authenticated' ? homeOf(state.session) : undefined
  const sheetNav = workspace
    ? [...PRIMARY_NAV.filter((i) => i.to !== PATHS.login && i.to !== PATHS.register), { label: 'Open workspace', to: workspace }]
    : PRIMARY_NAV
  const reduced = useReducedMotion()
  const [hovered, setHovered] = useState<string | null>(null)
  const [inLoop, setInLoop] = useState(false)
  const [open, setOpen] = useState(false)
  const [scrolled, setScrolled] = useState(() => window.scrollY > 8)
  const toggleRef = useRef<HTMLButtonElement>(null)
  const sheetRef = useRef<HTMLDivElement>(null)

  // Section awareness on Home: "How it works" reads as active while the loop section crosses a thin band
  // just above the viewport's middle. One element, one band, so the state flips once per edge (no flicker).
  // Presentation only: the URL is never touched while scrolling.
  useEffect(() => {
    if (pathname !== PATHS.home) return
    const section = document.getElementById(SECTIONS.howItWorks)
    if (!section) return
    const io = new IntersectionObserver(([entry]) => setInLoop(entry.isIntersecting), {
      rootMargin: '-40% 0px -55% 0px',
    })
    io.observe(section)
    return () => io.disconnect()
  }, [pathname])

  const loopActive = pathname === PATHS.home && inLoop
  /** Visual state: which nav item the reader is "at". */
  const isActive = (item: NavItem) => (item.section ? loopActive : pathname === item.to && !loopActive)
  /** Semantics: Home stays the current page; the section is the current location within it. */
  const ariaCurrent = (item: NavItem) =>
    item.section ? (loopActive ? 'location' : undefined) : pathname === item.to ? 'page' : undefined

  useEffect(() => {
    const onScroll = () => setScrolled(window.scrollY > 8)
    window.addEventListener('scroll', onScroll, { passive: true })
    return () => window.removeEventListener('scroll', onScroll)
  }, [])

  // Menu open: focus first item, Escape closes, Tab cycles toggle <-> sheet, page scroll locked.
  useEffect(() => {
    if (!open) return
    const sheet = sheetRef.current
    const toggle = toggleRef.current
    sheet?.querySelector<HTMLElement>(FOCUSABLE)?.focus()
    document.documentElement.style.overflow = 'hidden'

    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        setOpen(false)
        toggle?.focus()
        return
      }
      if (e.key !== 'Tab' || !sheet || !toggle) return
      const items = [toggle, ...sheet.querySelectorAll<HTMLElement>(FOCUSABLE)]
      const i = items.indexOf(document.activeElement as HTMLElement)
      const next = e.shiftKey ? (i <= 0 ? items.length - 1 : i - 1) : (i + 1) % items.length
      e.preventDefault()
      items[next].focus()
    }
    const mq = window.matchMedia(DESKTOP_QUERY)
    const onResize = () => mq.matches && setOpen(false)

    document.addEventListener('keydown', onKey)
    mq.addEventListener('change', onResize)
    return () => {
      document.documentElement.style.overflow = ''
      document.removeEventListener('keydown', onKey)
      mq.removeEventListener('change', onResize)
    }
  }, [open])

  const close = () => setOpen(false)
  const indicatorAt = hovered ?? CENTER_NAV.find(isActive)?.label

  return (
    <header className={cn('site-header', scrolled && 'is-scrolled', open && 'is-open')}>
      <div className="container site-header__bar">
        <Link to={PATHS.home} className="site-header__brand" aria-label="FoodLoop home" onClick={close}>
          <FoodLoopWordmark />
        </Link>

        <nav className="site-nav" aria-label="Primary">
          <ul role="list" className="site-nav__list" onMouseLeave={() => setHovered(null)}>
            {CENTER_NAV.map((item) => (
              <li key={item.label}>
                <NavItemLink
                  item={item}
                  className={cn('site-nav__link', isActive(item) && 'is-active')}
                  aria-current={ariaCurrent(item)}
                  onMouseEnter={() => setHovered(item.label)}
                  onFocus={() => setHovered(item.label)}
                  onBlur={() => setHovered(null)}
                >
                  {indicatorAt === item.label && (
                    <motion.span layoutId="nav-indicator" className="site-nav__indicator" transition={spring.indicator} />
                  )}
                  <span className="site-nav__label">{item.label}</span>
                </NavItemLink>
              </li>
            ))}
          </ul>
        </nav>

        <div className="site-header__actions">
          {workspace ? (
            <Button variant="primary" size="sm" to={workspace} iconEnd={<ArrowUpRight />} className="site-header__cta" onClick={close}>
              Open workspace
            </Button>
          ) : (
            <>
              <Button
                variant="ghost"
                size="sm"
                to={PATHS.login}
                className="site-header__signin"
                aria-current={pathname === PATHS.login ? 'page' : undefined}
              >
                Login
              </Button>
              <Button
                variant="primary"
                size="sm"
                to={PATHS.register}
                iconEnd={<ArrowUpRight />}
                className="site-header__cta"
                aria-current={pathname === PATHS.register ? 'page' : undefined}
                onClick={close}
              >
                Register
              </Button>
            </>
          )}
          <button
            ref={toggleRef}
            type="button"
            className="menu-toggle"
            aria-expanded={open}
            aria-controls="mobile-menu"
            aria-label={open ? 'Close menu' : 'Open menu'}
            onClick={() => setOpen((o) => !o)}
          >
            <span className="menu-toggle__line" />
            <span className="menu-toggle__line" />
          </button>
        </div>
      </div>

      <AnimatePresence>
        {open && (
          <motion.div
            ref={sheetRef}
            id="mobile-menu"
            className="menu-sheet on-dark grain"
            initial={reduced ? false : { opacity: 0, y: -16 }}
            animate={{ opacity: 1, y: 0 }}
            exit={{ opacity: 0, y: -12, transition: { duration: duration.fast } }}
            transition={{ duration: duration.ui, ease: ease.out }}
          >
            <BotanicalDecoration>
              <BotanicalCorner position="bottom-right" className="menu-sheet__corner" />
            </BotanicalDecoration>
            <nav aria-label="Mobile" className="container menu-sheet__inner">
              <motion.ul
                role="list"
                className="menu-sheet__list"
                initial={reduced ? false : 'hidden'}
                animate="visible"
                variants={{ visible: { transition: { staggerChildren: 0.05, delayChildren: 0.06 } } }}
              >
                {sheetNav.map((item, i) => (
                  <motion.li
                    key={item.label}
                    variants={{
                      hidden: { opacity: 0, y: 14 },
                      visible: { opacity: 1, y: 0, transition: { duration: duration.ui, ease: ease.out } },
                    }}
                  >
                    <NavItemLink
                      item={item}
                      className={cn('menu-sheet__link', isActive(item) && 'is-active')}
                      aria-current={ariaCurrent(item)}
                      onClick={close}
                    >
                      <span className="menu-sheet__index">{String(i + 1).padStart(2, '0')}</span>
                      {item.label}
                    </NavItemLink>
                  </motion.li>
                ))}
              </motion.ul>
              <div className="menu-sheet__actions">
                <Button variant="on-dark" size="lg" to={EXPLORE_FOOD_PATH} iconEnd={<ArrowRight />} onClick={close}>
                  Explore available food
                </Button>
              </div>
              <p className="menu-sheet__tagline t-label">Rescue · Redistribute · Repeat</p>
            </nav>
          </motion.div>
        )}
      </AnimatePresence>
    </header>
  )
}
