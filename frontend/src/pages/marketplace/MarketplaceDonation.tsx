import { motion, useReducedMotion } from 'framer-motion'
import { AlertCircle, ArrowLeft, ArrowRight, Building2, CheckCircle2, MapPin, RotateCcw } from 'lucide-react'
import { useRef, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { PATHS } from '../../app/routes'
import { FoodLoopMark } from '../../components/brand/FoodLoopMark'
import { FoodMedia } from '../../components/food/FoodMedia'
import { categoryVisual, formatUnitQuantity, pickupAreaOf } from '../../components/food/presentation'
import { MagneticButton } from '../../components/motion/MagneticButton'
import { MediaReveal } from '../../components/motion/MediaReveal'
import { Button } from '../../components/ui/Button'
import { StatusChip } from '../../components/ui/StatusChip'
import { ApiError } from '../../lib/api/client'
import { createClaim, getMarketplaceDonation } from '../../lib/api/marketplace'
import { useLoad } from '../../lib/api/useLoad'
import { describeExpiry, expiryPhrase, formatAbsolute, formatAgo } from '../../lib/expiry'
import { ease } from '../../lib/motion'
import '../donations/donations.css'

type Claim =
  | { status: 'idle' }
  | { status: 'submitting' }
  | { status: 'claimed'; claimId: string }
  /** `final`: the listing can't be claimed any more, so the button goes away. */
  | { status: 'failed'; message: string; final: boolean }

const refOf = (id: string) => `#${id.slice(0, 8).toUpperCase()}`

/** Copy for a failed claim, from the stable code only. A 401 never gets here: the session guard takes over. */
function claimFailure(error: unknown): Extract<Claim, { status: 'failed' }> {
  const code = error instanceof ApiError ? error.code : ''
  switch (code) {
    case 'claim.not_available':
    case 'claim.conflict':
    case 'donation.not_found':
      return { status: 'failed', final: true, message: 'Another organization claimed this donation first, or it was withdrawn. It’s no longer available.' }
    case 'claim.expired':
      return { status: 'failed', final: true, message: 'This donation’s pickup window has closed, so it can no longer be claimed.' }
    case 'organization.not_active':
      return { status: 'failed', final: true, message: 'Your organization can’t claim food right now. Contact the FoodLoop team if this looks wrong.' }
    case 'antiforgery.invalid':
      return { status: 'failed', final: false, message: 'Your session changed in the meantime. Nothing was claimed — please try again.' }
    case 'network':
      // The request may have reached the server: never resend blindly.
      return { status: 'failed', final: true, message: 'We couldn’t reach FoodLoop to confirm the claim. Reload this page to see whether it went through.' }
    default:
      return { status: 'failed', final: false, message: 'Something went wrong on our side and nothing was claimed. Please try again in a moment.' }
  }
}

/** Beneficiary view of a live marketplace listing (GET /api/marketplace/{id}) with the real claim action. */
export function MarketplaceDonation() {
  const { id = '' } = useParams()
  const reduced = useReducedMotion()
  const donation = useLoad(id, (signal) => getMarketplaceDonation(id, signal))
  const [claim, setClaim] = useState<Claim>({ status: 'idle' })
  // Blocks a second click that lands before React re-renders the disabled button.
  const inFlight = useRef(false)

  if (donation.error instanceof ApiError && donation.error.code === 'donation.not_found') return <UnavailableDonation />
  if (donation.error !== undefined) return <LoadFailed onRetry={donation.reload} error={donation.error} />
  if (!donation.fresh || !donation.data) {
    return (
      <div className="container ws-page" aria-busy="true">
        <p className="ws-empty" role="status">
          Loading this listing…
        </p>
      </div>
    )
  }

  const d = donation.data
  const expiry = describeExpiry(d.expiresAtUtc)
  const category = categoryVisual(d.category)
  const CategoryIcon = category.icon
  const claimed = claim.status === 'claimed'

  async function onClaim() {
    if (inFlight.current) return
    inFlight.current = true
    setClaim({ status: 'submitting' })
    try {
      const { claimId } = await createClaim(d.id)
      setClaim({ status: 'claimed', claimId })
    } catch (error) {
      setClaim(claimFailure(error))
    } finally {
      inFlight.current = false
    }
  }

  const enter = (delay: number) => ({
    initial: reduced ? false : { opacity: 0, y: 20 },
    animate: { opacity: 1, y: 0 },
    transition: { duration: 0.6, ease: ease.out, delay },
  })

  return (
    <div className="container ws-page detail">
      <Link to={PATHS.marketplace} className="ws-back">
        <ArrowLeft aria-hidden="true" />
        Marketplace
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
                Donated by <strong>{d.donorName}</strong>
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
            <span className="ticket__ref t-data">{refOf(d.id)}</span>
            <FoodLoopMark className="ticket__mark" />
          </header>

          <motion.div
            className="ticket__status"
            initial={reduced ? false : { opacity: 0, y: 10 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ duration: 0.5, ease: ease.out, delay: 0.55 }}
          >
            {/* The server listed it, so it is Available; after a successful claim it is ours. */}
            <StatusChip tone={claimed ? 'info' : 'success'}>{claimed ? 'Claimed' : 'Available'}</StatusChip>
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
              <dt>Donor</dt>
              <dd>{d.donorName}</dd>
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
            {claimed ? (
              <Button variant="on-dark" size="lg" to={PATHS.marketplace} iconStart={<ArrowLeft />}>
                Back to marketplace
              </Button>
            ) : claim.status === 'failed' && claim.final ? (
              <Button variant="on-dark" size="lg" to={PATHS.marketplace} iconStart={<ArrowLeft />}>
                Find other food
              </Button>
            ) : (
              <MagneticButton className="ticket__claim">
                <Button
                  variant="on-dark"
                  size="lg"
                  iconEnd={<ArrowRight />}
                  loading={claim.status === 'submitting'}
                  disabled={claim.status === 'submitting'}
                  onClick={onClaim}
                >
                  {claim.status === 'submitting' ? 'Claiming…' : 'Claim donation'}
                </Button>
              </MagneticButton>
            )}
            <div role="status" className="ticket__status-slot">
              {claim.status === 'claimed' && (
                <p className="ws-notice">
                  <CheckCircle2 aria-hidden="true" />
                  <span>
                    Claimed for your organization. Reference <strong className="t-data">{refOf(claim.claimId)}</strong> — the donor
                    can see your claim now.
                  </span>
                </p>
              )}
              {claim.status === 'failed' && (
                <p className="ws-notice">
                  <AlertCircle aria-hidden="true" />
                  {claim.message}
                </p>
              )}
            </div>
          </div>
        </motion.aside>
      </div>
    </div>
  )
}

function UnavailableDonation() {
  return (
    <div className="container ws-page">
      <div className="ws-empty">
        <h1 className="t-h2">This donation isn’t available.</h1>
        <p>It may have been claimed by another organization, reached its pickup window, or the link is incomplete.</p>
        <Button to={PATHS.marketplace} iconStart={<ArrowLeft />}>
          Back to marketplace
        </Button>
      </div>
    </div>
  )
}

function LoadFailed({ error, onRetry }: { error: unknown; onRetry: () => void }) {
  const forbidden = error instanceof ApiError && error.code === 'organization.not_active'
  return (
    <div className="container ws-page">
      <div className="ws-empty">
        <h1 className="t-h2">{forbidden ? 'The marketplace isn’t open to your organization.' : 'We couldn’t load this donation.'}</h1>
        <p>
          {forbidden
            ? 'Only active beneficiary organizations can browse and claim food.'
            : 'Check your connection and try again.'}
        </p>
        {forbidden ? (
          <Button to={PATHS.marketplace} iconStart={<ArrowLeft />}>
            Back to marketplace
          </Button>
        ) : (
          <Button variant="outline" iconStart={<RotateCcw />} onClick={onRetry}>
            Try again
          </Button>
        )}
      </div>
    </div>
  )
}
