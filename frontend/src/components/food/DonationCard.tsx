import { AnimatePresence, motion } from 'framer-motion'
import { ArrowUpRight, Building2, Clock, MapPin, Package } from 'lucide-react'
import { Link } from 'react-router-dom'
import { donationPath } from '../../app/routes'
import { cn } from '../../lib/cn'
import { describeExpiry } from '../../lib/expiry'
import { duration } from '../../lib/motion'
import { StatusChip } from '../ui/StatusChip'
import { ExpiryBadge } from './ExpiryBadge'
import { FoodMedia } from './FoodMedia'
import { STATUS_META, pickupAreaOf, type Listing } from './presentation'
import './food.css'

type DonationCardProps = {
  donation: Listing
  /** Wide editorial layout (media beside the copy) for the lead listing. */
  feature?: boolean
  /** Live-preview mode: no link, no hover affordance. */
  preview?: boolean
  className?: string
}

/**
 * Marketplace listing. Visual order: food → urgency → title → organization → logistics.
 * Critical facts are always visible; the description is the only hover-revealed extra (fine pointers only).
 * The CTA's hit area stretches over the whole card, so there is one tab stop per listing.
 */
export function DonationCard({ donation: d, feature, preview, className }: DonationCardProps) {
  // Preview drafts may not have an expiry / quantity yet.
  const expiry = d.expiresAt ? describeExpiry(d.expiresAt) : null
  const category = d.category
  const status = STATUS_META[d.status]
  const CategoryIcon = category.icon

  return (
    <article className={cn('food-card', feature && 'food-card--feature', preview && 'food-card--preview', className)}>
      <div className="food-card__body">
        <h3 className="food-card__title">{d.title}</h3>
        <p className="food-card__org">
          <Building2 aria-hidden="true" />
          {d.donorName}
        </p>
        {feature && d.description && <p className="food-card__lede">{d.description}</p>}

        <dl className="food-card__facts">
          <div>
            <dt>
              <Package aria-hidden="true" />
              Quantity
            </dt>
            <dd className="t-data">{d.quantityLabel}</dd>
          </div>
          <div>
            <dt>
              <MapPin aria-hidden="true" />
              Pickup
            </dt>
            <dd>{pickupAreaOf(d)}</dd>
          </div>
          <div className="food-card__fact--wide">
            <dt>Closes</dt>
            <dd>{expiry ? <time dateTime={d.expiresAt}>{expiry.absolute}</time> : 'Not set yet'}</dd>
          </div>
        </dl>

        <div className="food-card__foot">
          <StatusChip tone={status.tone}>{status.label}</StatusChip>
          {preview ? (
            <span className="food-card__cta" aria-hidden="true">
              View &amp; claim
              <ArrowUpRight />
            </span>
          ) : (
            <Link to={donationPath(d.id)} className="food-card__cta">
              View &amp; claim<span className="visually-hidden">: {d.title}</span>
              <ArrowUpRight aria-hidden="true" />
            </Link>
          )}
        </div>
      </div>
      {/* Visually first (CSS order), after the copy in reading order so the title leads. */}
      <div className="food-card__media">
        {/* Crossfades when the category changes (live preview); static everywhere else. */}
        <AnimatePresence initial={false}>
          <motion.div
            key={d.imageUrl ?? category.label}
            className="food-card__layer"
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            transition={{ duration: duration.reveal }}
          >
            <FoodMedia visual={category} imageUrl={d.imageUrl} className="food-card__img" />
          </motion.div>
        </AnimatePresence>
        <span className="food-card__category">
          <CategoryIcon aria-hidden="true" />
          {category.label}
        </span>
        {expiry ? (
          <ExpiryBadge expiry={expiry} className="food-card__expiry" />
        ) : (
          <span className="expiry expiry--later food-card__expiry">
            <Clock aria-hidden="true" />
            Expiry not set
          </span>
        )}
        {/* Secondary: slides over the image on hover (fine pointers). Always in the accessibility tree. */}
        {d.description && !feature && <p className="food-card__more">{d.description}</p>}
      </div>
    </article>
  )
}
