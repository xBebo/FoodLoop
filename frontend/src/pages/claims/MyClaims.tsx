import { AnimatePresence, motion, useReducedMotion } from 'framer-motion'
import { AlertCircle, ArrowLeft, ArrowRight } from 'lucide-react'
import { useState } from 'react'
import { PATHS, claimPath } from '../../app/routes'
import { BotanicalCorner, BotanicalDecoration } from '../../components/brand/Botanical'
import { formatUnitQuantity, pickupAreaOf } from '../../components/food/presentation'
import { CLAIM_STATUS_META, refOf, type ClaimPhase } from '../../components/operations/presentation'
import { Button } from '../../components/ui/Button'
import { SectionEyebrow } from '../../components/ui/SectionEyebrow'
import { StatusChip } from '../../components/ui/StatusChip'
import { PageLoading, PageMessage } from '../../components/workspace/PageState'
import { getMyClaims, type ClaimSummary } from '../../lib/api/claims'
import { codeOf } from '../../lib/api/client'
import { useLoad } from '../../lib/api/useLoad'
import { cn } from '../../lib/cn'
import { describeExpiry, formatAgo } from '../../lib/expiry'
import { duration, ease, spring } from '../../lib/motion'
import type { ClaimStatus } from '../../types/claim'
import { useCancelClaim } from './useCancelClaim'
import './claims.css'

type View = 'all' | ClaimPhase

const VIEWS: { id: View; label: string }[] = [
  { id: 'all', label: 'All' },
  { id: 'active', label: 'Active' },
  { id: 'done', label: 'Completed' },
  { id: 'ended', label: 'Cancelled' },
]

// The statuses the handover lifecycle actually passes through while a claim is active.
const ACTIVE_STAGES: { status: ClaimStatus; label: string }[] = [
  { status: 'Booked', label: 'Booked' },
  { status: 'PickupPending', label: 'Pickup pending' },
  { status: 'InTransit', label: 'In transit' },
]

const phaseOf = (c: ClaimSummary) => CLAIM_STATUS_META[c.claimStatus].phase

export function MyClaims() {
  const reduced = useReducedMotion()
  const [page, setPage] = useState(1)
  const load = useLoad(String(page), (signal) => getMyClaims(page, signal))
  const [view, setView] = useState<View>('all')

  if (load.error !== undefined && !load.data)
    return (
      <PageMessage title="We couldn’t load your claims." onRetry={load.reload}>
        {codeOf(load.error) === 'auth.forbidden' ? 'Claims are available to beneficiary organizations only.' : 'Check your connection and try again.'}
      </PageMessage>
    )
  if (!load.data) return <PageLoading label="Loading your claims…" />

  const { items: claims, hasNext } = load.data
  const inView = (v: View) => (v === 'all' ? claims : claims.filter((c) => phaseOf(c) === v))
  const visible = inView(view)
  const active = inView('active')
  const paged = page > 1 || hasNext

  return (
    <div className="container ws-page claims">
      <header className="claims-intro">
        <div className="claims-intro__text">
          <SectionEyebrow>Beneficiary workspace</SectionEyebrow>
          <h1 className="ws-intro__title">
            My <em>claims</em>
          </h1>
          <p className="t-lead ws-intro__lead">Food you’ve claimed, where it is on its way to you, and what already arrived.</p>
        </div>

        {/* ---- Pulse: counted from the claims listed below ---- */}
        <section className="claims-pulse on-dark grain" aria-labelledby="pulse-title">
          <BotanicalDecoration>
            <BotanicalCorner position="top-right" className="claims-pulse__contours" />
          </BotanicalDecoration>
          <h2 id="pulse-title" className="claims-pulse__title t-label">
            {paged ? `Page ${page} at a glance` : 'Claims at a glance'}
          </h2>
          <dl className="claims-pulse__totals">
            <div className="is-primary">
              <dt>Active</dt>
              <dd className="t-data">{active.length}</dd>
            </div>
            <div>
              <dt>{paged ? 'On this page' : 'Total'}</dt>
              <dd className="t-data">{claims.length}</dd>
            </div>
            <div>
              <dt>Closed</dt>
              <dd className="t-data">{inView('done').length}</dd>
            </div>
            <div>
              <dt>Cancelled</dt>
              <dd className="t-data">{inView('ended').length}</dd>
            </div>
          </dl>
          <div className="claims-pulse__pipe">
            <p className="claims-pulse__pipe-title">Active claims by status</p>
            <ol role="list" className="pipe">
              {ACTIVE_STAGES.map((s) => {
                const here = active.filter((c) => c.claimStatus === s.status)
                return (
                  <li key={s.status} className={cn('pipe__stage', here.length > 0 && 'is-occupied')}>
                    <span className="pipe__dots" aria-hidden="true">
                      {here.map((c) => (
                        <span key={c.claimId} className="pipe__dot" />
                      ))}
                    </span>
                    <span className="pipe__count t-data">{here.length}</span>
                    <span className="pipe__label">{s.label}</span>
                  </li>
                )
              })}
            </ol>
          </div>
        </section>
      </header>

      <section className="claims-records" aria-labelledby="records-title">
        <div className="claims-records__head">
          <h2 id="records-title" className="claims-records__title">
            Claim log
          </h2>
          <div className="claims-filter" role="group" aria-label="Filter claims">
            {VIEWS.map((v) => (
              <button
                key={v.id}
                type="button"
                className={cn('claims-filter__btn', view === v.id && 'is-active')}
                aria-pressed={view === v.id}
                onClick={() => setView(v.id)}
              >
                {view === v.id && (
                  <motion.span layoutId="claims-filter-indicator" className="claims-filter__indicator" transition={spring.indicator} />
                )}
                <span className="claims-filter__label">{v.label}</span>
                <span className="claims-filter__count t-data">{inView(v.id).length}</span>
              </button>
            ))}
          </div>
        </div>

        {visible.length > 0 ? (
          <ol role="list" className="claims-list" aria-busy={load.loading}>
            <AnimatePresence mode="popLayout" initial={!reduced}>
              {visible.map((c, i) => (
                <motion.li
                  key={c.claimId}
                  layout="position"
                  initial={reduced ? false : { opacity: 0, y: 16 }}
                  animate={{ opacity: 1, y: 0 }}
                  exit={{ opacity: 0, transition: { duration: duration.fast } }}
                  transition={{ duration: 0.45, ease: ease.out, delay: Math.min(i, 10) * 0.05 }}
                >
                  <ClaimRow claim={c} onChanged={load.reload} />
                </motion.li>
              ))}
            </AnimatePresence>
          </ol>
        ) : (
          <div className="ws-empty">
            <h3>{claims.length === 0 ? 'No claims yet.' : 'Nothing in this view.'}</h3>
            <p>
              {claims.length === 0
                ? 'Claim food from the marketplace and it will appear here.'
                : 'Claims move between views as they are delivered, closed or cancelled.'}
            </p>
            {claims.length === 0 && page === 1 && <Button to={PATHS.marketplace}>Browse the marketplace</Button>}
          </div>
        )}

        {paged && (
          <nav className="ws-pager" aria-label="Claim pages">
            <Button variant="outline" size="sm" iconStart={<ArrowLeft />} disabled={page === 1 || load.loading} onClick={() => setPage((p) => p - 1)}>
              Newer
            </Button>
            <span className="t-data">Page {page}</span>
            <Button variant="outline" size="sm" iconEnd={<ArrowRight />} disabled={!hasNext || load.loading} onClick={() => setPage((p) => p + 1)}>
              Older
            </Button>
          </nav>
        )}
      </section>
    </div>
  )
}

function ClaimRow({ claim: c, onChanged }: { claim: ClaimSummary; onChanged: () => void }) {
  const cancel = useCancelClaim(onChanged)
  const status = CLAIM_STATUS_META[c.claimStatus]
  const expiry = describeExpiry(c.expiresAtUtc)
  const live = status.phase === 'active'

  return (
    <article className={cn('claim-row', `claim-row--${status.phase}`)} aria-labelledby={`claim-${c.claimId}`}>
      <p className="claim-row__ref">
        <span className="t-data">{refOf(c.claimId)}</span>
        <time dateTime={c.claimedAtUtc}>Claimed {formatAgo(c.claimedAtUtc).toLowerCase()}</time>
      </p>

      <div className="claim-row__main">
        <h3 id={`claim-${c.claimId}`} className="claim-row__title">
          {c.donationTitle}
        </h3>
        <p className="claim-row__org">
          Pickup in <strong>{pickupAreaOf(c)}</strong>
        </p>
        <dl className="claim-row__facts">
          <div>
            <dt>Quantity</dt>
            <dd className="t-data">{formatUnitQuantity(c.quantity, c.unit)}</dd>
          </div>
          <div>
            <dt>Expiry</dt>
            <dd>
              <time dateTime={c.expiresAtUtc} className={cn(live && expiry.urgency === 'critical' && 'is-urgent')}>
                {live ? expiry.relative : expiry.absolute}
              </time>
            </dd>
          </div>
        </dl>
      </div>

      <div className="claim-row__journey">
        <StatusChip tone={status.tone}>{status.label}</StatusChip>
      </div>

      <div className="claim-row__actions">
        <Button variant={live ? 'primary' : 'outline'} size="sm" to={claimPath(c.claimId)} iconEnd={<ArrowRight />}>
          Details<span className="visually-hidden"> for {c.donationTitle}</span>
        </Button>
        {c.canCancel && (
          <Button
            variant="ghost"
            size="sm"
            loading={cancel.state.status === 'submitting'}
            disabled={cancel.state.status === 'submitting'}
            onClick={() => cancel.run(c.claimId, c.donationTitle)}
          >
            Cancel claim<span className="visually-hidden"> for {c.donationTitle}</span>
          </Button>
        )}
      </div>

      {cancel.state.status === 'failed' && (
        <div role="status" className="claim-row__status">
          <p className="ws-notice">
            <AlertCircle aria-hidden="true" />
            {cancel.state.message}
          </p>
        </div>
      )}
    </article>
  )
}
