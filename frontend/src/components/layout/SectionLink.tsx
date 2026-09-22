import { useReducedMotion } from 'framer-motion'
import type { AnchorHTMLAttributes, MouseEvent } from 'react'
import { Link, useLocation } from 'react-router-dom'
import type { NavItem } from '../../app/routes'

type AnchorProps = AnchorHTMLAttributes<HTMLAnchorElement>

/**
 * Link to a section of the Home page.
 * On Home: smooth-scrolls (instant under reduced motion) and moves focus to the section.
 * Elsewhere: navigates to /#id and the router's ScrollRestoration lands on the section.
 */
export function SectionLink({ section, onClick, ...rest }: AnchorProps & { section: string }) {
  const { pathname } = useLocation()
  const reduced = useReducedMotion()

  function handleClick(e: MouseEvent<HTMLAnchorElement>) {
    onClick?.(e)
    if (pathname !== '/' || e.defaultPrevented || e.button !== 0 || e.metaKey || e.ctrlKey || e.shiftKey) return
    const target = document.getElementById(section)
    if (!target) return
    e.preventDefault()
    target.scrollIntoView({ behavior: reduced ? 'auto' : 'smooth', block: 'start' })
    target.focus({ preventScroll: true })
  }

  return <Link to={{ pathname: '/', hash: section }} onClick={handleClick} {...rest} />
}

/** Renders a NavItem as a router link or a section link. */
export function NavItemLink({ item, ...rest }: AnchorProps & { item: NavItem }) {
  return item.section ? <SectionLink section={item.section} {...rest} /> : <Link to={item.to} {...rest} />
}
