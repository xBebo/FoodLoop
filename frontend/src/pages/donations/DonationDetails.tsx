import { motion, useReducedMotion } from 'framer-motion'
import { AlertCircle, ArrowLeft, Building2, CheckCircle2, MapPin, Pencil, Send } from 'lucide-react'
import { useRef, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { PATHS, editDonationPath } from '../../app/routes'
import { FoodLoopMark } from '../../components/brand/FoodLoopMark'
import { FoodMedia } from '../../components/food/FoodMedia'
import { STATUS_META, categoryVisual, formatUnitQuantity, pickupAreaOf } from '../../components/food/presentation'
import { MagneticButton } from '../../components/motion/MagneticButton'
import { MediaReveal } from '../../components/motion/MediaReveal'
import { Button } from '../../components/ui/Button'
import { StatusChip } from '../../components/ui/StatusChip'
import { PageLoading, PageMessage } from '../../components/workspace/PageState'
import { codeOf } from '../../lib/api/client'
import { getDonation, publishDonation } from '../../lib/api/donations'
import { useLoad } from '../../lib/api/useLoad'
import { describeExpiry, expiryPhrase, formatAbsolute, formatAgo } from '../../lib/expiry'
import { ease } from '../../lib/motion'
import { useSession, workspaceRoleOf } from '../../lib/session/context'
import { MarketplaceDonation } from '../marketplace/MarketplaceDonation'
import './donations.css'

export function DonationDetails() {
  const { state } = useSession()
  // Beneficiaries read the live marketplace listing; donors read their own donation.
  const beneficiary = state.status === 'authenticated' && workspaceRoleOf(state.session) === 'beneficiary'
  return beneficiary ? <MarketplaceDonation /> : <DonorDonationDetails />
}

type Publish = { status: 'idle' } | { status: 'submitting' } | { status: 'done' } | { status: 'failed'; message: string }

function publishFailure(error: unknown): string {
  switch (codeOf(error)) {
    case 'donation.invalid_state':
      return 'This donation can’t be published: it is no longer a draft, or its collect-by time has already passed. Edit the draft first.'
    case 'donation.not_found':
      return 'This donation is no longer available to your organization.'
    case 'antiforgery.invalid':
      return 'Your session changed in the meantime. Nothing was published — please try again.'
    case 'network':
      return 'We couldn’t reach FoodLoop. Reload this page to see whether it was published.'
    default:
      return 'Something went wrong on our side and nothing was published. Please try again in a moment.'
  }
}

function DonorDonationDetails() {
  const { id = '' } = useParams()
  const reduced = useReducedMotion()
  const { state } = useSession()
  const orgName = (state.status === 'authenticated' && state.session.organization?.name) || 'Your organization'
  const load = useLoad(id, (signal) => getDonation(id, signal))
  const [publish, setPublish] = useState<Publish>({ status: 'idle' })
  const inFlight = useRef(false)

  if (codeOf(load.error) === 'donation.not_found') return <MissingDonation />
  if (load.error !== undefined && !load.data) return <PageMessage title="We couldn’t load this donation." onRetry={load.reload} />
  if (!load.data) return <PageLoading label="Loading donation…" />

  const d = load.data
  const expiry = describeExpiry(d.expiresAtUtc)
  const status = STATUS_META[d.status]
  const category = categoryVisual(d.category)
  const CategoryIcon = category.icon

  async function onPublish() {
    if (inFlight.current) return
    inFlight.current = true
    setPublish({ status: 'submitting' })
    try {
      await publishDonation(d.id)
      setPublish({ status: 'done' })
    } catch (error) {
      setPublish({ status: 'failed', message: publishFailure(error) })
    } finally {
      inFlight.current = false
      // Either way the server has the truth now: show it.
      load.reload()
    }
  }

  const enter = (delay: number) => ({
    initial: reduced ? false : { opacity: 0, y: 20 },
    animate: { opacity: 1, y: 0 },
    transition: { duration: 0.6, ease: ease.out, delay },
  })

  return (
    <div className="container ws-page detail">
      <Link to={PATHS.donations} className="ws-back">
        <ArrowLeft aria-hidden="true" />
        My donations
      </Link>

      <div className="detail__grid">
        <article className="detail__main" aria-labelledby="detail-title">
          <div className="detail__media-wrap">
            <MediaReveal className="detail__media">
              <FoodMedia visual={category} priority className="detail__img" />
            </MediaReveal>
            <span className="detail__category">
              <CategoryIcon aria-hidden="true" />
              {category.label}
            </span>
          </div>

          <motion.header className="detail__head" {...enter(0.2)}>
            <p className="detail__kicker t-label">
              Prepared {formatAgo(d.preparedAtUtc)} · {pickupAreaOf(d)}
            </p>
            <h1 id="detail-title" className="detail__title">
              {d.title}
            </h1>
            <p className="detail__org">
              <Building2 aria-hidden="true" />
              <span>
                Donated by <strong>{orgName}</strong>
                <span className="detail__own"> · your organization</span>
              </span>
            </p>
          </motion.header>

          <motion.div className="detail__body" {...enter(0.3)}>
            {d.description && (
              <>
                <h2 className="detail__section-title t-label">About this food</h2>
                <p className="detail__desc">{d.description}</p>
              </>
            )}
            {d.storageInstructions && (
              <>
                <h2 className="detail__section-title t-label">Storage</h2>
                <p className="detail__desc">{d.storageInstructions}</p>
              </>
            )}
            <h2 className="detail__section-title t-label">Where to collect</h2>
            <p className="detail__address">
              <MapPin aria-hidden="true" />
              {d.pickupAddress}
            </p>
          </motion.div>
        </article>

        <motion.aside
          className="ticket on-dark grain"
          aria-labelledby="ticket-title"
          initial={reduced ? false : { opacity: 0, x: 28 }}
          animate={{ opacity: 1, x: 0 }}
          transition={{ duration: 0.75, ease: ease.out, delay: 0.25 }}
        >
          <header className="ticket__head">
            <h2 id="ticket-title" className="ticket__kind t-label">
              Pickup ticket
            </h2>
            <span className="ticket__ref t-data">#{d.id.slice(0, 8).toUpperCase()}</span>
            <FoodLoopMark className="ticket__mark" />
          </header>

          <motion.div
            className="ticket__status"
            initial={reduced ? false : { opacity: 0, y: 10 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ duration: 0.5, ease: ease.out, delay: 0.55 }}
          >
            <StatusChip tone={status.tone}>{status.label}</StatusChip>
            <p className={`ticket__urgency is-${expiry.urgency}`}>{expiryPhrase(expiry)}</p>
            <p className="ticket__urgency-meta">
              <strong>{expiry.urgencyLabel}</strong> · {expiry.absolute}
            </p>
          </motion.div>

          <div className="ticket__tear" aria-hidden="true" />

          <dl className="ticket__facts">
            <div>
              <dt>Category</dt>
              <dd>{category.label}</dd>
            </div>
            <div>
              <dt>Quantity</dt>
              <dd className="t-data">{formatUnitQuantity(d.quantity, d.unit)}</dd>
            </div>
            <div className="ticket__fact--wide">
              <dt>Expires</dt>
              <dd>
                <time dateTime={d.expiresAtUtc}>{formatAbsolute(d.expiresAtUtc)}</time>
              </dd>
            </div>
            <div className="ticket__fact--wide">
              <dt>Pickup location</dt>
              <dd>{d.pickupAddress}</dd>
            </div>
            <div className="ticket__fact--wide">
              <dt>Prepared</dt>
              <dd>
                <time dateTime={d.preparedAtUtc}>{formatAbsolute(d.preparedAtUtc)}</time>
              </dd>
            </div>
          </dl>

          <div className="ticket__tear" aria-hidden="true" />

          <div className="ticket__actions">
            <p className="ticket__hint">
              {d.status === 'Draft' ? 'Your draft — not visible on the marketplace yet.' : `Your listing · ${status.label.toLowerCase()}.`}
            </p>
            {d.canPublish && (
              <MagneticButton className="ticket__claim">
                <Button
                  variant="on-dark"
                  size="lg"
                  iconStart={<Send />}
                  loading={publish.status === 'submitting'}
                  disabled={publish.status === 'submitting'}
                  onClick={onPublish}
                >
                  {publish.status === 'submitting' ? 'Publishing…' : 'Publish to marketplace'}
                </Button>
              </MagneticButton>
            )}
            {d.canEdit && (
              <Button variant={d.canPublish ? 'ghost' : 'on-dark'} size="lg" to={editDonationPath(d.id)} iconStart={<Pencil />}>
                Edit draft
              </Button>
            )}
            <div role="status" className="ticket__status-slot">
              {publish.status === 'done' && (
                <p className="ws-notice">
                  <CheckCircle2 aria-hidden="true" />
                  Published. Beneficiary organizations can now see and claim this donation.
                </p>
              )}
              {publish.status === 'failed' && (
                <p className="ws-notice">
                  <AlertCircle aria-hidden="true" />
                  {publish.message}
                </p>
              )}
            </div>
          </div>
        </motion.aside>
      </div>
    </div>
  )
}

function MissingDonation() {
  return (
    <PageMessage
      title="This donation isn’t available."
      action={
        <Button to={PATHS.donations} iconStart={<ArrowLeft />}>
          Back to my donations
        </Button>
      }
    >
      It may have been removed, belong to another organization, or the link is incomplete.
    </PageMessage>
  )
}
