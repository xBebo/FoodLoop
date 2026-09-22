import { AnimatePresence, motion, useReducedMotion } from 'framer-motion'
import { Hourglass, Pencil, Plus } from 'lucide-react'
import { useState } from 'react'
import { PATHS, donationPath, editDonationPath } from '../../app/routes'
import { BotanicalCorner, BotanicalDecoration } from '../../components/brand/Botanical'
import { FoodMedia } from '../../components/food/FoodMedia'
import { STATUS_META, categoryVisual, formatUnitQuantity, pickupAreaOf, type StatusPhase } from '../../components/food/presentation'
import { MagneticButton } from '../../components/motion/MagneticButton'
import { RevealGroup, RevealItem } from '../../components/motion/Reveal'
import { Button } from '../../components/ui/Button'
import { SectionEyebrow } from '../../components/ui/SectionEyebrow'
import { StatusChip } from '../../components/ui/StatusChip'
import { PageLoading, PageMessage } from '../../components/workspace/PageState'
import { getMyDonations, type DonationItem } from '../../lib/api/donations'
import { useLoad } from '../../lib/api/useLoad'
import { cn } from '../../lib/cn'
import { describeExpiry, formatAgo } from '../../lib/expiry'
import { duration, ease, spring } from '../../lib/motion'
import { useSession } from '../../lib/session/context'
import './donations.css'

type View = 'all' | 'open' | 'progress' | 'done' | 'other'

const VIEWS: { id: View; label: string; phases: StatusPhase[] }[] = [
  { id: 'all', label: 'All', phases: ['draft', 'open', 'progress', 'done', 'ended'] },
  { id: 'open', label: 'Available', phases: ['open'] },
  { id: 'progress', label: 'In progress', phases: ['progress'] },
  { id: 'done', label: 'Completed', phases: ['done'] },
  { id: 'other', label: 'Drafts & ended', phases: ['draft', 'ended'] },
]

// Composition bar order + legend copy. Counts always come from the dataset.
const PHASES: { phase: StatusPhase; label: string }[] = [
  { phase: 'open', label: 'Available' },
  { phase: 'progress', label: 'In progress' },
  { phase: 'done', label: 'Completed' },
  { phase: 'draft', label: 'Draft' },
  { phase: 'ended', label: 'Ended' },
]

const phaseOf = (d: DonationItem) => STATUS_META[d.status].phase

export function MyDonations() {
  const reduced = useReducedMotion()
  const { state } = useSession()
  const orgName = state.status === 'authenticated' ? state.session.organization?.name : undefined
  const load = useLoad('mine', getMyDonations)
  const [view, setView] = useState<View>('all')

  if (load.error !== undefined && !load.data)
    return <PageMessage title="We couldn’t load your donations." onRetry={load.reload}>Check your connection and try again.</PageMessage>
  if (!load.data) return <PageLoading label="Loading your donations…" />
  const donations = load.data

  const count = (phase: StatusPhase) => donations.filter((d) => phaseOf(d) === phase).length
  const closingSoon = donations.filter(
    (d) => phaseOf(d) === 'open' && describeExpiry(d.expiresAtUtc).urgency === 'critical',
  ).length
  const inView = (v: View) => donations.filter((d) => VIEWS.find((x) => x.id === v)!.phases.includes(phaseOf(d)))
  const visible = inView(view)

  return (
    <div className="container ws-page mine">
      <header className="ws-intro">
        <SectionEyebrow>Donor workspace</SectionEyebrow>
        <h1 className="ws-intro__title">
          My <em>donations</em>
        </h1>
        <p className="t-lead ws-intro__lead">
          Everything {orgName ?? 'your organization'} has shared — from first draft to the last delivery.
        </p>
        <div className="ws-intro__actions">
          <MagneticButton>
            <Button size="lg" to={PATHS.newDonation} iconStart={<Plus />}>
              Create donation
            </Button>
          </MagneticButton>
        </div>
      </header>

      {/* ---- Overview: every number is derived from the listing data below ---- */}
      <section className="mine-overview" aria-labelledby="overview-title">
        <h2 id="overview-title" className="visually-hidden">
          Overview
        </h2>
        <RevealGroup className="mine-overview__grid">
          <RevealItem className="mine-total on-dark grain">
            <BotanicalDecoration>
              <BotanicalCorner position="bottom-right" className="mine-total__contours" />
            </BotanicalDecoration>
            <p className="mine-total__label t-label">Total listed</p>
            <p className="mine-total__value">{donations.length}</p>
            <div className="mine-bar" aria-hidden="true">
              {PHASES.map(({ phase }) => (
                <span key={phase} className={`mine-bar__seg mine-bar__seg--${phase}`} style={{ flexGrow: count(phase) }} />
              ))}
            </div>
            <ul role="list" className="mine-legend">
              {PHASES.map(({ phase, label }) => (
                <li key={phase}>
                  <span className={`mine-legend__swatch mine-bar__seg--${phase}`} aria-hidden="true" />
                  {label} <strong className="t-data">{count(phase)}</strong>
                </li>
              ))}
            </ul>
            {closingSoon > 0 && (
              <p className="mine-total__alert">
                <Hourglass aria-hidden="true" />
                {closingSoon} available {closingSoon === 1 ? 'listing closes' : 'listings close'} within 3 hours
              </p>
            )}
          </RevealItem>

          <RevealItem className="mine-stats-wrap">
            <dl className="mine-stats">
              <div className="mine-stat mine-stat--open">
                <dt>Available</dt>
                <dd>{count('open')}</dd>
                <dd className="mine-stat__note">Open on the marketplace</dd>
              </div>
              <div className="mine-stat mine-stat--progress">
                <dt>In progress</dt>
                <dd>{count('progress')}</dd>
                <dd className="mine-stat__note">Claimed or on the move</dd>
              </div>
              <div className="mine-stat mine-stat--done">
                <dt>Completed</dt>
                <dd>{count('done')}</dd>
                <dd className="mine-stat__note">Delivered or closed</dd>
              </div>
            </dl>
          </RevealItem>
        </RevealGroup>
      </section>

      {/* ---- Records ---- */}
      <section className="mine-records" aria-labelledby="records-title">
        <div className="mine-records__head">
          <h2 id="records-title" className="mine-records__title">
            Listings
          </h2>
          <div className="mine-tabs" role="group" aria-label="Filter listings by status">
            {VIEWS.map((v) => (
              <button
                key={v.id}
                type="button"
                className={cn('mine-tab', view === v.id && 'is-active')}
                aria-pressed={view === v.id}
                onClick={() => setView(v.id)}
              >
                {view === v.id && <motion.span layoutId="mine-tab-indicator" className="mine-tab__indicator" transition={spring.indicator} />}
                <span className="mine-tab__label">{v.label}</span>
                <span className="mine-tab__count t-data">{inView(v.id).length}</span>
              </button>
            ))}
          </div>
        </div>

        <div className="mine-cols" aria-hidden="true">
          <span />
          <span>Donation</span>
          <span>Status</span>
          <span>Quantity</span>
          <span>Expires</span>
          <span>Prepared</span>
          <span />
        </div>

        {visible.length > 0 ? (
          <ol role="list" className="mine-list">
            <AnimatePresence mode="popLayout" initial={!reduced}>
              {visible.map((d, i) => (
                <motion.li
                  key={d.id}
                  layout="position"
                  initial={reduced ? false : { opacity: 0, y: 16 }}
                  animate={{ opacity: 1, y: 0 }}
                  exit={{ opacity: 0, x: -12, transition: { duration: duration.fast } }}
                  transition={{ duration: 0.45, ease: ease.out, delay: Math.min(i, 10) * 0.04 }}
                >
                  <Record donation={d} />
                </motion.li>
              ))}
            </AnimatePresence>
          </ol>
        ) : (
          <div className="ws-empty">
            <h3>No listings here yet.</h3>
            <p>
              {donations.length === 0
                ? 'Create your first donation — it starts as a draft you can review before publishing.'
                : 'Nothing in this view right now. Listings move between views as they are claimed and delivered.'}
            </p>
          </div>
        )}
      </section>
    </div>
  )
}

function Record({ donation: d }: { donation: DonationItem }) {
  const status = STATUS_META[d.status]
  const category = categoryVisual(d.category)
  const expiry = describeExpiry(d.expiresAtUtc)
  const live = status.phase !== 'done' && status.phase !== 'ended'
  const urgent = status.phase === 'open' && expiry.urgency === 'critical'

  return (
    <article className={cn('record', urgent && 'is-urgent', `record--${status.phase}`)} aria-labelledby={`rec-${d.id}`}>
      <div className="record__media">
        <FoodMedia visual={category} className="record__img" />
      </div>
      <div className="record__main">
        <h3 id={`rec-${d.id}`} className="record__title">
          {d.title}
        </h3>
        <p className="record__meta">
          {category.label} · {pickupAreaOf(d)}
        </p>
      </div>
      <div className="record__status">
        <motion.span key={d.status} initial={{ opacity: 0, scale: 0.9 }} animate={{ opacity: 1, scale: 1 }} transition={spring.ui}>
          <StatusChip tone={status.tone}>{status.label}</StatusChip>
        </motion.span>
      </div>
      <dl className="record__facts">
        <div>
          <dt>Quantity</dt>
          <dd className="t-data">{formatUnitQuantity(d.quantity, d.unit)}</dd>
        </div>
        <div>
          <dt>Expires</dt>
          <dd>
            {live && (
              <span className={cn('record__expiry', urgent && 'is-urgent')}>
                {urgent && <Hourglass aria-hidden="true" />}
                {expiry.relative}
              </span>
            )}
            <time dateTime={d.expiresAtUtc} className="record__abs">
              {expiry.absolute}
            </time>
          </dd>
        </div>
        <div>
          <dt>Prepared</dt>
          <dd>
            <time dateTime={d.preparedAtUtc}>{formatAgo(d.preparedAtUtc)}</time>
          </dd>
        </div>
      </dl>
      <div className="record__actions">
        <Button variant="outline" size="sm" to={donationPath(d.id)}>
          View<span className="visually-hidden"> {d.title}</span>
        </Button>
        {d.canEdit && (
          <Button variant="ghost" size="sm" to={editDonationPath(d.id)} iconStart={<Pencil />}>
            Edit<span className="visually-hidden"> {d.title}</span>
          </Button>
        )}
      </div>
    </article>
  )
}
