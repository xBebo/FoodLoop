import { motion, useReducedMotion } from 'framer-motion'
import { AlertCircle, CheckCircle2, Send } from 'lucide-react'
import { useRef, useState } from 'react'
import { BotanicalCorner, BotanicalDecoration } from '../../components/brand/Botanical'
import { Button } from '../../components/ui/Button'
import { StatusChip } from '../../components/ui/StatusChip'
import { PageLoading, PageMessage } from '../../components/workspace/PageState'
import { assignCourier, getAssignableClaims, getCouriers, type AssignableClaim, type CourierOption } from '../../lib/api/admin'
import { useLoad } from '../../lib/api/useLoad'
import { cn } from '../../lib/cn'
import { describeExpiry, formatAgo } from '../../lib/expiry'
import { ease } from '../../lib/motion'
import { Route } from '../courier/Route'
import { AdminIntro } from './kit'
import { pad2 } from './presentation'
import { useAdminAction, type ActionNotice } from './useAdminAction'
import './dispatch-ops.css'

const loadBoard = (signal: AbortSignal) => Promise.all([getAssignableClaims(signal), getCouriers(signal)])

/** Claims the backend says can take a courier (booked, or pickup pending for reassignment). The server re-checks on assign. */
export function AssignCourier() {
  const load = useLoad('dispatch', loadBoard)
  const action = useAdminAction(load.reload)

  if (load.error !== undefined && !load.data) return <PageMessage title="We couldn’t load the dispatch board." onRetry={load.reload} />
  if (!load.data) return <PageLoading label="Loading the dispatch board…" />

  const [assignable, couriers] = load.data
  // Soonest to expire first; unassigned before reassignments.
  const claims = [...assignable].sort(
    (a, b) => Number(a.hasCourier) - Number(b.hasCourier) || a.expiresAtUtc.localeCompare(b.expiresAtUtc),
  )
  const unassigned = claims.filter((c) => !c.hasCourier)
  const urgent = unassigned.filter((c) => describeExpiry(c.expiresAtUtc).urgency === 'critical').length

  return (
    <div className="container ws-page adm">
      <AdminIntro
        code="ADM-04"
        title={
          <>
            Assign <em>courier</em>
          </>
        }
        lead="Claims waiting for a courier, soonest to expire first. Claims that already have one can be reassigned until pickup."
        aside={
          claims.length > 0 && (
            <dl className="readout">
              <div className={cn(urgent > 0 && 'is-warn')}>
                <dt>Closing soon</dt>
                <dd>{pad2(urgent)}</dd>
              </div>
              <div>
                <dt>Unassigned</dt>
                <dd>{pad2(unassigned.length)}</dd>
              </div>
            </dl>
          )
        }
        meta={['Dispatch operations', `${couriers.length} couriers on roster`, `${unassigned.length} awaiting dispatch`]}
      />

      <div className="adm-notice" role="status">
        {action.notice && <Notice notice={action.notice} />}
      </div>

      {claims.length === 0 ? (
        <div className="ws-empty queue-empty">
          <h2>Nothing to dispatch.</h2>
          <p>No claim is waiting for a courier. New claims appear here the moment they’re booked.</p>
        </div>
      ) : (
        <ol role="list" className="dboard" aria-label="Claims awaiting a courier, soonest to expire first">
          {claims.map((c, i) => (
            <li key={c.claimId}>
              <DispatchRow
                claim={c}
                index={i}
                couriers={couriers}
                pending={action.pendingId === c.claimId}
                busy={action.busy}
                onAssign={(courier) =>
                  action.run(c.claimId, `“${c.donationTitle}”`, () => assignCourier(c.claimId, courier.id), `was assigned to ${courier.name}.`)
                }
              />
            </li>
          ))}
        </ol>
      )}
    </div>
  )
}

function Notice({ notice }: { notice: ActionNotice }) {
  return (
    <p className="ws-notice">
      {notice.ok ? <CheckCircle2 aria-hidden="true" /> : <AlertCircle aria-hidden="true" />}
      {notice.text}
    </p>
  )
}

type RowProps = {
  claim: AssignableClaim
  index: number
  couriers: CourierOption[]
  pending: boolean
  busy: boolean
  onAssign: (courier: CourierOption) => void
}

function DispatchRow({ claim: c, index, couriers, pending, busy, onAssign }: RowProps) {
  const reduced = useReducedMotion()
  const [courierId, setCourierId] = useState('')
  const [error, setError] = useState(false)
  const expiry = describeExpiry(c.expiresAtUtc)
  const selectRef = useRef<HTMLSelectElement>(null)

  function assign() {
    const courier = couriers.find((cr) => cr.id === courierId)
    if (!courier) {
      setError(true)
      selectRef.current?.focus()
      return
    }
    setError(false)
    onAssign(courier)
  }

  const selectId = `courier-${c.claimId}`

  return (
    <motion.article
      className={cn('dop', !c.hasCourier && expiry.urgency === 'critical' && 'is-urgent')}
      aria-labelledby={`dop-title-${c.claimId}`}
      initial={reduced ? false : { opacity: 0, y: 24 }}
      whileInView={{ opacity: 1, y: 0 }}
      viewport={{ once: true, amount: 0.15 }}
      transition={{ duration: 0.5, ease: ease.out, delay: Math.min(index, 6) * 0.06 }}
    >
      <BotanicalDecoration>
        <BotanicalCorner position="top-right" className="dop__contours" />
      </BotanicalDecoration>

      <header className="dop__head">
        <div>
          <p className="dop__kicker">
            <StatusChip tone={c.hasCourier ? 'info' : 'warning'}>{c.hasCourier ? 'Courier assigned' : 'Awaiting courier'}</StatusChip>
            <code className="dop__ref">#{c.claimId.slice(0, 8).toUpperCase()}</code>
          </p>
          <h2 id={`dop-title-${c.claimId}`} className="dop__title">
            {c.donationTitle}
          </h2>
        </div>
        <div className={cn('dop__expiry', expiry.urgency === 'critical' && 'is-urgent')}>
          <span className="t-label">Expires</span>
          <time dateTime={c.expiresAtUtc}>{expiry.relative}</time>
          <span className="dop__claimed">Claimed {formatAgo(c.claimedAtUtc)}</span>
        </div>
      </header>

      <div className="dop__route">
        {/* Before pickup the courier always starts at the donor. */}
        <Route task={{ nextStep: 'VerifyPickup', donorName: c.donorName, pickupAddress: c.pickupAddress, beneficiaryName: c.beneficiaryName }} />
      </div>

      <div className="dop__dispatch">
        <div className="dop__field">
          <label htmlFor={selectId} className="dop__label">
            {c.hasCourier ? 'Reassign to' : 'Courier'}
          </label>
          <select
            ref={selectRef}
            id={selectId}
            className="adm-select dop__select"
            value={courierId}
            disabled={busy}
            aria-invalid={error || undefined}
            aria-describedby={error ? `${selectId}-error` : undefined}
            onChange={(e) => {
              setCourierId(e.target.value)
              setError(false)
            }}
          >
            <option value="">{couriers.length ? 'Select a courier…' : 'No couriers on the roster'}</option>
            {couriers.map((cr) => (
              <option key={cr.id} value={cr.id}>
                {cr.name}
              </option>
            ))}
          </select>
          {error && (
            <p id={`${selectId}-error`} className="dop__error" role="alert">
              Choose a courier before dispatching this claim.
            </p>
          )}
        </div>
        <Button variant="primary" size="sm" iconStart={<Send />} onClick={assign} loading={pending} disabled={busy} className="dop__assign">
          {c.hasCourier ? 'Reassign' : 'Assign'}
        </Button>
      </div>
    </motion.article>
  )
}
