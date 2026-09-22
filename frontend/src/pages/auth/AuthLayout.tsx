import { motion, useReducedMotion } from 'framer-motion'
import type { ReactNode } from 'react'
import { AmbientOrb, BotanicalDecoration } from '../../components/brand/Botanical'
import { cn } from '../../lib/cn'
import { ease } from '../../lib/motion'
import './auth.css'

type AuthLayoutProps = {
  variant: 'login' | 'register'
  /** Brand art panel (supplementary; rendered after the form in DOM order, placed first visually). */
  panel: ReactNode
  /** Botanical art for the panel background. */
  art: ReactNode
  children: ReactNode
}

/** Split composition shared by Login and Register: forest art panel + paper form panel. */
export function AuthLayout({ variant, panel, art, children }: AuthLayoutProps) {
  const reduced = useReducedMotion()
  return (
    <div className={cn('auth', `auth--${variant}`)}>
      <div className="auth-main">{children}</div>

      <motion.aside
        className="auth-panel on-dark grain"
        aria-label="About FoodLoop"
        initial={reduced ? false : { clipPath: 'inset(0% 0% 100% 0% round 36px)' }}
        animate={{ clipPath: 'inset(0% 0% 0% 0% round 36px)' }}
        transition={{ duration: 0.9, ease: ease.inOut }}
      >
        <BotanicalDecoration>
          <AmbientOrb tone="sage" className="auth-panel__orb" />
          {/* The Home hero's orbit, sweeping through the panel: same world, quieter. */}
          <svg viewBox="0 0 100 100" className="auth-panel__orbit" aria-hidden="true" focusable="false">
            <circle cx="50" cy="50" r="49.5" className="auth-panel__orbit-track" />
            <circle cx="50" cy="50" r="49.5" className="auth-panel__orbit-dash" />
            <circle cx="3.5" cy="33" r="1.1" className="auth-panel__orbit-node" />
          </svg>
          {art}
        </BotanicalDecoration>
        <div className="auth-panel__content">{panel}</div>
      </motion.aside>
    </div>
  )
}
