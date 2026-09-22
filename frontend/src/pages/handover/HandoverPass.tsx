import { motion, useReducedMotion } from 'framer-motion'
import { ArrowLeft, Copy, KeyRound } from 'lucide-react'
import { useEffect, useMemo, useRef, useState } from 'react'
import { PATHS } from '../../app/routes'
import { FoodLoopMark } from '../../components/brand/FoodLoopMark'
import { HANDOVER_SHOWN_BY } from '../../components/operations/presentation'
import { Button } from '../../components/ui/Button'
import { StatusChip } from '../../components/ui/StatusChip'
import { PageMessage } from '../../components/workspace/PageState'
import type { IssuedCode } from '../../lib/api/courier'
import { cn } from '../../lib/cn'
import { describeExpiry } from '../../lib/expiry'
import { ease, spring } from '../../lib/motion'
import './handover.css'

// Security-line geometry: a fan of fine sine waves, computed once. Decorative only.
const SECURITY_LINES = Array.from({ length: 14 }, (_, i) => {
  const pts: string[] = []
  for (let x = 0; x <= 400; x += 8) {
    const y = 40 + i * 9 + Math.sin(x / 38 + i * 0.45) * (10 + i * 0.8) + Math.sin(x / 13 - i) * 2
    pts.push(`${x} ${y.toFixed(1)}`)
  }
  return `M${pts.join('L')}`
})

type CopyState = 'idle' | 'copied' | 'failed'
const FEEDBACK_MS = 1500

/**
 * A freshly issued one-time code, rendered from memory. Nothing here is persisted: closing, navigating away or
 * refreshing discards it, and the server cannot return it again.
 */
export function HandoverPass({ donationTitle, issued: c, onClose }: { donationTitle: string; issued: IssuedCode; onClose: () => void }) {
  const reduced = useReducedMotion()
  const [copy, setCopy] = useState<CopyState>('idle')
  const [now, setNow] = useState(() => Date.now())
  const timer = useRef<number | undefined>(undefined)
  // The backend's SVG is shown as an image (data URL), never injected as markup, so it cannot run anything.
  const qrSrc = useMemo(() => `data:image/svg+xml;charset=utf-8,${encodeURIComponent(c.qrSvg)}`, [c.qrSvg])

  useEffect(() => () => window.clearTimeout(timer.current), [])
  useEffect(() => {
    const tick = window.setInterval(() => setNow(Date.now()), 15_000)
    return () => window.clearInterval(tick)
  }, [])

  const expired = new Date(c.expiresAtUtc).getTime() <= now
  const expiry = describeExpiry(c.expiresAtUtc)
  // Grouped for reading only; what gets copied is the raw code.
  const groups = c.code.match(/.{1,8}/g) ?? [c.code]

  async function copyCode() {
    try {
      await navigator.clipboard.writeText(c.code)
      setCopy('copied')
    } catch {
      setCopy('failed')
    }
    window.clearTimeout(timer.current)
    timer.current = window.setTimeout(() => setCopy('idle'), FEEDBACK_MS)
  }

  return (
    <div className="pass-scene on-dark grain">
      <div className="container pass-scene__inner">
        <div className="pass-scene__context">
          <button type="button" className="ws-back" onClick={onClose}>
            <ArrowLeft aria-hidden="true" />
            Handover codes
          </button>
          <p className="pass-scene__kicker t-label">{c.type} handover code</p>
          <h1 className="pass-scene__title">{donationTitle}</h1>
          <p className="pass-scene__lead">
            {HANDOVER_SHOWN_BY[c.type]} They enter it to confirm the handover. This code is shown only once — if you leave this page,
            issue a new one (it replaces this code).
          </p>
          <dl className="pass-scene__facts">
            <div>
              <dt>Status</dt>
              <dd>
                <StatusChip tone={expired ? 'neutral' : 'success'}>{expired ? 'Expired' : 'Active'}</StatusChip>
              </dd>
            </div>
            <div>
              <dt>{expired ? 'Expired' : 'Valid until'}</dt>
              <dd>
                <time dateTime={c.expiresAtUtc}>{expiry.absolute}</time>
              </dd>
            </div>
          </dl>
        </div>

        <motion.article
          className={cn('pass', expired && 'is-inactive')}
          aria-labelledby="pass-brand"
          initial={reduced ? false : { opacity: 0, y: 48, rotate: -2.5 }}
          animate={{ opacity: 1, y: 0, rotate: 0 }}
          transition={{ duration: 0.9, ease: ease.out, delay: 0.15 }}
        >
          <svg className="pass__security" viewBox="0 0 400 200" preserveAspectRatio="none" aria-hidden="true">
            {SECURITY_LINES.map((d, i) => (
              <path key={i} d={d} fill="none" stroke="currentColor" strokeWidth="0.6" vectorEffect="non-scaling-stroke" />
            ))}
          </svg>

          <header className="pass__head">
            <FoodLoopMark className="pass__mark" />
            <p id="pass-brand" className="pass__brand">
              <span>FoodLoop</span>
              <span>Secure handover</span>
            </p>
            <span className="pass__type">{c.type}</span>
          </header>

          <figure className="qr qr--live">
            <img src={qrSrc} alt={`QR code for this ${c.type.toLowerCase()} handover`} className="qr__img" width={232} height={232} />
          </figure>

          <div className="pass__tear" aria-hidden="true" />

          <div className="pass__code-block">
            <p className="pass__code-label t-label">Handover code · {c.code.length} characters</p>
            <p className="pass__code">
              {groups.map((g, i) => (
                <span key={i}>{g}</span>
              ))}
            </p>
          </div>

          <div className="pass__foot">
            <p className="pass__expiry">
              <span className="t-label">{expired ? 'Expired' : 'Expires'}</span>
              <time dateTime={c.expiresAtUtc}>{expiry.absolute}</time>
            </p>
            <Button variant="primary" onClick={copyCode} iconStart={<Copy />} className="pass__copy">
              <motion.span key={copy} initial={reduced ? false : { opacity: 0, y: 4 }} animate={{ opacity: 1, y: 0 }} transition={spring.ui}>
                {copy === 'copied' ? 'Copied ✓' : 'Copy code'}
              </motion.span>
            </Button>
          </div>
          <p className={cn('pass__live', copy !== 'failed' && 'visually-hidden')} aria-live="polite">
            {copy === 'copied' ? 'Copied ✓' : copy === 'failed' ? 'Couldn’t copy — select the code and copy it instead.' : ''}
          </p>

          {expired && (
            <span className="pass__stamp" aria-hidden="true">
              Expired
            </span>
          )}
        </motion.article>
      </div>
    </div>
  )
}

/** /handover/codes/:id — a raw code can never be loaded again, so an old link only explains that truthfully. */
export function HandoverCodeGone() {
  return (
    <PageMessage
      title="This code can’t be reopened."
      action={
        <Button to={PATHS.handoverCodes} iconStart={<KeyRound />}>
          Issue a new code
        </Button>
      }
    >
      For security, a handover code is shown only once, when it is issued, and FoodLoop keeps no readable copy. Issue a new code — it
      replaces any earlier one.
    </PageMessage>
  )
}
