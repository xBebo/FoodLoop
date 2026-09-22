import { motion, useReducedMotion } from 'framer-motion'
import { AlertCircle, ArrowLeft, CheckCircle2, MapPin, Truck } from 'lucide-react'
import { useParams, Link } from 'react-router-dom'
import { PATHS } from '../../app/routes'
import { BotanicalCorner, BotanicalDecoration } from '../../components/brand/Botanical'
import { formatUnitQuantity } from '../../components/food/presentation'
import { CLAIM_STATUS_META, refOf } from '../../components/operations/presentation'
import { Button } from '../../components/ui/Button'
import { StatusChip } from '../../components/ui/StatusChip'
import { PageLoading, PageMessage } from '../../components/workspace/PageState'
import { getClaim } from '../../lib/api/claims'
import { codeOf } from '../../lib/api/client'
import { useLoad } from '../../lib/api/useLoad'
import { describeExpiry, formatAbsolute, formatAgo } from '../../lib/expiry'
import { ease } from '../../lib/motion'
import { ClaimJourney } from './ClaimJourney'
import { EventTimeline } from './EventTimeline'
import { useCancelClaim } from './useCancelClaim'
import './claims.css'

export function ClaimDetails() {
  const { id = '' } = useParams()
  const reduced = useReducedMotion()
  const load = useLoad(id, (signal) => getClaim(id, signal))
  const cancel = useCancelClaim(load.reload)

  if (codeOf(load.error) === 'claim.not_found') return <MissingClaim />
  if (load.error !== undefined && !load.data) return <PageMessage title="We couldn’t load this claim." onRetry={load.reload} />
  if (!load.data) return <PageLoading label="Loading claim…" />

  const c = load.data
  const d = c.donation
  const status = CLAIM_STATUS_META[c.status]
  const expiry = describeExpiry(d.expiresAtUtc)
  const enter = (delay: number, x = 0) => ({
    initial: reduced ? false : { opacity: 0, y: x ? 0 : 18, x },
    animate: { opacity: 1, y: 0, x: 0 },
    transition: { duration: 0.65, ease: ease.out, delay },
  })

  return (
    <div className="container ws-page claim">
      <Link to={PATHS.claims} className="ws-back">
        <ArrowLeft aria-hidden="true" />
        My claims
      </Link>

      <div className="claim__grid">
        <motion.section className="claim__identity" aria-labelledby="claim-title" {...enter(0.05)}>
          <p className="claim__kicker t-label">
            Claim <span className="t-data">{refOf(c.claimId)}</span> · {d.categoryName}
          </p>
          <h1 id="claim-title" className="claim__title">
            {d.title}
          </h1>
          <p className="claim__org">
            <Truck aria-hidden="true" />
            <span>{c.courierDisplayName ? <>Courier: <strong>{c.courierDisplayName}</strong></> : 'No courier assigned yet'}</span>
          </p>
          <div className="claim__status">
            <StatusChip tone={status.tone}>{status.label}</StatusChip>
            <span className="claim__updated">
              Claimed <time dateTime={c.claimedAtUtc}>{formatAgo(c.claimedAtUtc).toLowerCase()}</time>
            </span>
          </div>

          <h2 className="visually-hidden">Claim summary</h2>
          <dl className="claim__summary">
            <div>
              <dt>Quantity</dt>
              <dd className="t-data">{formatUnitQuantity(d.quantity, d.unit)}</dd>
            </div>
            <div>
              <dt>Expiry</dt>
              <dd>
                {status.phase === 'active' && <span className="claim__expiry-rel">{expiry.relative}</span>}
                <time dateTime={d.expiresAtUtc}>{expiry.absolute}</time>
              </dd>
            </div>
            <div className="is-wide">
              <dt>Pickup location</dt>
              <dd className="claim__address">
                <MapPin aria-hidden="true" />
                {d.pickupAddress}
              </dd>
            </div>
            {d.storageInstructions && (
              <div className="is-wide">
                <dt>Storage</dt>
                <dd>{d.storageInstructions}</dd>
              </div>
            )}
            <div className="is-wide">
              <dt>Claimed on</dt>
              <dd>
                <time dateTime={c.claimedAtUtc}>{formatAbsolute(c.claimedAtUtc)}</time>
              </dd>
            </div>
          </dl>

          {(c.canCancel || cancel.state.status !== 'idle') && (
            <div className="claim__actions">
              {c.canCancel && (
                <Button
                  variant="outline"
                  loading={cancel.state.status === 'submitting'}
                  disabled={cancel.state.status === 'submitting'}
                  onClick={() => cancel.run(c.claimId, d.title)}
                >
                  {cancel.state.status === 'submitting' ? 'Cancelling…' : 'Cancel claim'}
                </Button>
              )}
              <div role="status" className="claim__status-slot">
                {cancel.state.status === 'done' && (
                  <p className="ws-notice">
                    <CheckCircle2 aria-hidden="true" />
                    Claim cancelled. The donation was released.
                  </p>
                )}
                {cancel.state.status === 'failed' && (
                  <p className="ws-notice">
                    <AlertCircle aria-hidden="true" />
                    {cancel.state.message}
                  </p>
                )}
              </div>
            </div>
          )}
        </motion.section>

        <motion.section className="claim__stage on-dark grain" aria-labelledby="journey-title" {...enter(0.2, 28)}>
          <BotanicalDecoration>
            <BotanicalCorner position="bottom-right" className="claim__contours" />
          </BotanicalDecoration>
          <h2 id="journey-title" className="claim__stage-title t-label">
            Claim journey
          </h2>
          <ClaimJourney timeline={c.timeline} />
        </motion.section>
      </div>

      <section className="claim__history" aria-labelledby="history-title">
        <div className="claim__history-head">
          <h2 id="history-title" className="claim__history-title">
            Event <em>history</em>
          </h2>
          <p className="claim__history-note">
            {c.timeline.length} recorded {c.timeline.length === 1 ? 'event' : 'events'}, oldest first. Only what FoodLoop has recorded
            appears here.
          </p>
        </div>
        <EventTimeline events={c.timeline} />
      </section>
    </div>
  )
}

function MissingClaim() {
  return (
    <PageMessage
      title="This claim isn’t on record."
      action={
        <Button to={PATHS.claims} iconStart={<ArrowLeft />}>
          Back to my claims
        </Button>
      }
    >
      The link may be incomplete. Your current claims are listed on My claims.
    </PageMessage>
  )
}
