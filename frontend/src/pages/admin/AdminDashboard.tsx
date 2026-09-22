import { motion, useReducedMotion } from 'framer-motion'
import { ArrowRight, ArrowUpRight } from 'lucide-react'
import { Link } from 'react-router-dom'
import { PATHS } from '../../app/routes'
import { BotanicalCorner, BotanicalDecoration } from '../../components/brand/Botanical'
import { RevealGroup, RevealItem } from '../../components/motion/Reveal'
import { PageLoading, PageMessage } from '../../components/workspace/PageState'
import { getAssignableClaims, getAudit, getDashboard, getOrganizations, getPendingOrganizations } from '../../lib/api/admin'
import { useLoad } from '../../lib/api/useLoad'
import { cn } from '../../lib/cn'
import { describeExpiry, formatAgo } from '../../lib/expiry'
import { ease } from '../../lib/motion'
import { ORGANIZATION_STATUSES } from '../../types/organization'
import { AdminIntro } from './kit'
import { daysSince, formatUtcTime, pad2 } from './presentation'
import './dashboard.css'

const plural = (n: number, one: string, many = `${one}s`) => `${n} ${n === 1 ? one : many}`

/** Everything on this page comes from admin endpoints: the five backend counts plus the real queues and registry totals. */
async function loadOverview(signal: AbortSignal) {
  const [summary, pending, assignable, audit, ...byStatus] = await Promise.all([
    getDashboard(signal),
    getPendingOrganizations(signal),
    getAssignableClaims(signal),
    getAudit({ page: 1 }, signal),
    ...ORGANIZATION_STATUSES.map((s) => getOrganizations(s, 1, signal)),
  ])
  return {
    summary,
    pending,
    assignable,
    audit,
    byOrgStatus: ORGANIZATION_STATUSES.map((status, i) => ({ status, count: byStatus[i].totalCount })),
  }
}

export function AdminDashboard() {
  const reduced = useReducedMotion()
  const load = useLoad('overview', loadOverview)

  if (load.error !== undefined && !load.data) return <PageMessage title="We couldn’t load the overview." onRetry={load.reload} />
  if (!load.data) return <PageLoading label="Loading the operations overview…" />

  const { summary, pending, assignable, audit, byOrgStatus } = load.data
  const unassigned = assignable.filter((c) => !c.hasCourier)
  const organizations = byOrgStatus.reduce((a, s) => a + s.count, 0)
  const closingSoon = unassigned.filter((c) => describeExpiry(c.expiresAtUtc).urgency === 'critical').length
  const oldestWait = pending.length ? daysSince(pending[0].createdAtUtc) : 0
  const attention = [summary.pendingOrganizations, unassigned.length, summary.expiredDonations].filter(Boolean).length

  // Bars grow from the left as their tile reveals; under reduced motion they render full width at once.
  const grow = (i = 0) => ({
    initial: reduced ? false : { scaleX: 0 },
    whileInView: { scaleX: 1 },
    viewport: { once: true },
    transition: { duration: 0.7, ease: ease.out, delay: 0.25 + i * 0.05 },
  })

  return (
    <div className="container ws-page adm">
      <AdminIntro
        code="ADM-01"
        title={
          <>
            Operations <em>overview</em>
          </>
        }
        lead="Everything moving through FoodLoop right now — what needs a decision first, and what already reached a table."
        meta={[plural(organizations, 'organization'), plural(summary.availableDonations, 'available donation'), plural(audit.totalCount, 'audit entry', 'audit entries')]}
      />

      <RevealGroup className="bento">
        {/* ---------------- Operations summary ---------------- */}
        <RevealItem className="bento__tile bento__summary on-dark grain">
          <section aria-labelledby="ops-title" className="ops">
            <BotanicalDecoration>
              <BotanicalCorner position="top-right" className="ops__contours" />
            </BotanicalDecoration>
            <h2 id="ops-title" className="bento__label">
              Food on the network
            </h2>

            <div className="ops__hero">
              <p className="ops__figure t-data">{pad2(summary.availableDonations)}</p>
              <p className="ops__caption">
                <strong>Available donations</strong>
                Open on the marketplace for beneficiaries to claim right now.
              </p>
            </div>

            <dl className="ops__facts">
              <div>
                <dt>Waiting for a courier</dt>
                <dd className="t-data">{pad2(unassigned.length)}</dd>
              </div>
              <div>
                <dt>Closed deliveries</dt>
                <dd className="t-data">{pad2(summary.closedDeliveries)}</dd>
              </div>
              <div>
                <dt>Cancelled claims</dt>
                <dd className="t-data">{pad2(summary.cancelledClaims)}</dd>
              </div>
            </dl>
          </section>
        </RevealItem>

        {/* ---------------- Needs attention ---------------- */}
        <RevealItem className="bento__tile bento__attention">
          <section aria-labelledby="attention-title" className="attention">
            <motion.span
              className="attention__rule"
              aria-hidden="true"
              initial={reduced ? false : { scaleX: 0 }}
              animate={{ scaleX: 1 }}
              transition={{ duration: 0.8, ease: ease.out, delay: 0.3 }}
            />
            <header className="attention__head">
              <h2 id="attention-title" className="attention__title">
                Needs attention
              </h2>
              <p className="attention__count t-label">{attention ? plural(attention, 'queue') : 'All clear'}</p>
            </header>
            <ol role="list" className="attention__list">
              <li>
                <Link to={PATHS.adminPending} className={cn('attention__item', summary.pendingOrganizations > 0 && 'is-due')}>
                  <span className="attention__num t-data">{pad2(summary.pendingOrganizations)}</span>
                  <span className="attention__text">
                    <strong>Organizations awaiting review</strong>
                    <span>
                      {summary.pendingOrganizations
                        ? `Oldest waiting ${oldestWait === 0 ? 'since today' : plural(oldestWait, 'day')}`
                        : 'The review queue is empty'}
                    </span>
                  </span>
                  <ArrowUpRight aria-hidden="true" className="attention__arrow" />
                </Link>
              </li>
              <li>
                <Link to={PATHS.adminCourier} className={cn('attention__item', unassigned.length > 0 && 'is-due')}>
                  <span className="attention__num t-data">{pad2(unassigned.length)}</span>
                  <span className="attention__text">
                    <strong>Claims without a courier</strong>
                    <span>{closingSoon ? `${closingSoon} close within 3 hours` : 'None closing within 3 hours'}</span>
                  </span>
                  <ArrowUpRight aria-hidden="true" className="attention__arrow" />
                </Link>
              </li>
              <li>
                <div className={cn('attention__item', summary.expiredDonations > 0 && 'is-due')}>
                  <span className="attention__num t-data">{pad2(summary.expiredDonations)}</span>
                  <span className="attention__text">
                    <strong>Expired donations</strong>
                    <span>Listings that closed unclaimed — follow up with the donor.</span>
                  </span>
                </div>
              </li>
            </ol>
          </section>
        </RevealItem>

        {/* ---------------- Registry ---------------- */}
        <RevealItem className="bento__tile bento__registry">
          <section aria-labelledby="registry-title" className="tile">
            <h2 id="registry-title" className="bento__label">
              Organization registry
            </h2>
            <p className="tile__figure">
              <span className="t-data">{organizations}</span> organizations
            </p>
            <div className="statusbar" aria-hidden="true">
              {byOrgStatus
                .filter((s) => s.count)
                .map((s, i) => (
                  <motion.span key={s.status} className={`statusbar__seg is-${s.status.toLowerCase()}`} style={{ flexGrow: s.count }} {...grow(i)} />
                ))}
            </div>
            <dl className="tile__rows">
              {byOrgStatus.map((s) => (
                <div key={s.status} className={`is-${s.status.toLowerCase()}`}>
                  <dt>
                    <span className="tile__swatch" aria-hidden="true" />
                    {s.status}
                  </dt>
                  <dd className="t-data">{s.count}</dd>
                </div>
              ))}
            </dl>
            <Link to={PATHS.adminOrganizations} className="tile__link">
              Manage organizations <ArrowRight aria-hidden="true" />
            </Link>
          </section>
        </RevealItem>

        {/* ---------------- Delivery closure ---------------- */}
        <RevealItem className="bento__tile bento__closure">
          <section aria-labelledby="closure-title" className="tile">
            <h2 id="closure-title" className="bento__label">
              Delivery closure
            </h2>
            <p className="tile__figure">
              <span className="t-data">{pad2(summary.closedDeliveries)}</span> closed deliveries
            </p>
            <p className="tile__note">Picked up and delivered with verified handover codes at both ends.</p>
          </section>
        </RevealItem>

        {/* ---------------- Risk ---------------- */}
        <RevealItem className="bento__tile bento__risk">
          <section aria-labelledby="risk-title" className="tile risk">
            <h2 id="risk-title" className="bento__label">
              Lost along the way
            </h2>
            <dl className="risk__pair">
              <div>
                <dt>Cancelled claims</dt>
                <dd className="t-data">{pad2(summary.cancelledClaims)}</dd>
              </div>
              <div>
                <dt>Expired donations</dt>
                <dd className="t-data">{pad2(summary.expiredDonations)}</dd>
              </div>
            </dl>
            <p className="tile__note">Food that never reached a table.</p>
          </section>
        </RevealItem>

        {/* ---------------- Quick actions ---------------- */}
        <RevealItem className="bento__tile bento__actions">
          <nav aria-label="Quick actions" className="quick">
            <ul role="list" className="quick__list">
              {(
                [
                  [PATHS.adminPending, 'Review pending requests', plural(pending.length, 'request')],
                  [PATHS.adminCourier, 'Assign couriers', plural(assignable.length, 'claim')],
                  [PATHS.adminOrganizations, 'Manage organizations', plural(organizations, 'record')],
                  [PATHS.adminAudit, 'Open the audit log', plural(audit.totalCount, 'entry', 'entries')],
                ] as const
              ).map(([to, label, count], i) => (
                <li key={to}>
                  <Link to={to} className="quick__link">
                    <span className="quick__index t-data" aria-hidden="true">
                      {pad2(i + 1)}
                    </span>
                    <span className="quick__label">{label}</span>
                    <span className="quick__count">{count}</span>
                    <ArrowRight aria-hidden="true" className="quick__arrow" />
                  </Link>
                </li>
              ))}
            </ul>
          </nav>
        </RevealItem>

        {/* ---------------- Recent activity ---------------- */}
        <RevealItem className="bento__tile bento__activity">
          <section aria-labelledby="activity-title" className="activity">
            <header className="activity__head">
              <h2 id="activity-title" className="bento__label">
                Recent activity
              </h2>
              <Link to={PATHS.adminAudit} className="tile__link">
                Full audit log <ArrowRight aria-hidden="true" />
              </Link>
            </header>
            {audit.items.length ? (
              <ol role="list" className="activity__list">
                {audit.items.slice(0, 6).map((e) => (
                  <li key={e.id} className="activity__row">
                    <time dateTime={e.timestampUtc} className="activity__time">
                      <span className="t-data">{formatUtcTime(e.timestampUtc)}</span>
                      <span>{formatAgo(e.timestampUtc)}</span>
                    </time>
                    <code className="activity__action">{e.action}</code>
                    <span className="activity__who">{e.actorName}</span>
                    <span className="activity__entity">
                      {e.entityType} <code>{e.entityId.slice(0, 8)}</code>
                    </span>
                  </li>
                ))}
              </ol>
            ) : (
              <p className="tile__note">No activity recorded yet.</p>
            )}
          </section>
        </RevealItem>
      </RevealGroup>
    </div>
  )
}
