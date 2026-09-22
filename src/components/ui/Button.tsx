import { LoaderCircle } from 'lucide-react'
import type { AnchorHTMLAttributes, ButtonHTMLAttributes, ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { cn } from '../../lib/cn'
import { SectionLink } from '../layout/SectionLink'
import './ui.css'

export type ButtonVariant = 'primary' | 'secondary' | 'ghost' | 'outline' | 'danger' | 'on-dark'
export type ButtonSize = 'sm' | 'md' | 'lg'

type BaseProps = {
  variant?: ButtonVariant
  size?: ButtonSize
  iconStart?: ReactNode
  iconEnd?: ReactNode
  loading?: boolean
  children: ReactNode
}

type AnchorProps = AnchorHTMLAttributes<HTMLAnchorElement>
type AsButton = BaseProps & ButtonHTMLAttributes<HTMLButtonElement> & { href?: undefined; to?: undefined; section?: undefined }
type AsLink = BaseProps & AnchorProps & { href: string }
/** In-app route. */
type AsRoute = BaseProps & AnchorProps & { to: string }
/** Section of the Home page (see SectionLink). */
type AsSection = BaseProps & AnchorProps & { section: string }
export type ButtonProps = AsButton | AsLink | AsRoute | AsSection

/** Renders a <button>, or a link when `href` / `to` / `section` is given. Icons are decorative; label comes from children. */
export function Button(props: ButtonProps) {
  const { variant = 'primary', size = 'md', iconStart, iconEnd, loading = false, children, className, ...rest } = props
  const classes = cn('btn', `btn--${variant}`, `btn--${size}`, loading && 'is-loading', className)
  const content = (
    <>
      {loading ? (
        <LoaderCircle className="btn__icon btn__spinner" aria-hidden="true" />
      ) : (
        iconStart && <span className="btn__icon" aria-hidden="true">{iconStart}</span>
      )}
      <span className="btn__label">{children}</span>
      {iconEnd && <span className="btn__icon btn__icon--end" aria-hidden="true">{iconEnd}</span>}
    </>
  )

  if ('to' in rest && rest.to !== undefined) {
    return <Link className={classes} {...(rest as AnchorProps & { to: string })}>{content}</Link>
  }
  if ('section' in rest && rest.section !== undefined) {
    return <SectionLink className={classes} {...(rest as AnchorProps & { section: string })}>{content}</SectionLink>
  }
  if (rest.href !== undefined) {
    return (
      <a className={classes} aria-busy={loading || undefined} {...(rest as AnchorProps)}>
        {content}
      </a>
    )
  }

  const { disabled, type = 'button', ...buttonRest } = rest as ButtonHTMLAttributes<HTMLButtonElement>
  return (
    <button
      type={type}
      className={classes}
      disabled={disabled || loading}
      aria-busy={loading || undefined}
      {...buttonRest}
    >
      {content}
    </button>
  )
}
