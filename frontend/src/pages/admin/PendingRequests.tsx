import { motion, useReducedMotion } from 'framer-motion'
import { AlertCircle, Check, CheckCircle2, X } from 'lucide-react'
import { PATHS } from '../../app/routes'
import { Button } from '../../components/ui/Button'
import { StatusChip } from '../../components/ui/StatusChip'
import { PageLoading, PageMessage } from '../../components/workspace/PageState'
import { changeOrganization, getPendingOrganizations, type PendingOrganization } from '../../lib/api/admin'
import { useLoad } from '../../lib/api/useLoad'
import { cn } from '../../lib/cn'
import { formatAgo } from '../../lib/expiry'
import { ease } from '../../lib/motion'
import { ORGANIZATION_TYPE_LABELS } from '../../types/organization'
import { AdminIntro } from './kit'
import { daysSince, formatDate, formatUtc, pad2 } from './presentation'
import { useAdminAction, type ActionNotice } from './useAdminAction'
import './review.css'

const waited = (iso: string) => {
  const d = daysSince(iso)
  return d === 0 ? 'Since today' : `${d} ${d === 1 ? 'day' : 'days'}`
}

export function PendingRequests() {
  const load = useLoad('pending', getPendingOrganizations)
  const action = useAdminAction(load.reload)

  if (load.error !== undefined && !load.data) return <PageMessage title="We couldn’t load the review queue." onRetry={load.reload} />
  if (!load.data) return <PageLoading label="Loading the review queue…" />
  const requests = load.data

  const decide = (r: PendingOrganization, approve: boolean) =>
    action.run(
      r.id,
      r.name,
      () => changeOrganization(r.id, approve ? 'approve' : 'reject'),
      approve ? 'was approved and can now sign in.' : 'was rejected.',
    )

  return (
    <div className="container ws-page adm">
      <AdminIntro
        code="ADM-03"
        title={
          <>
            Pending <em>requests</em>
          </>
        }
        lead="Organizations waiting to join FoodLoop, oldest first. Check the licence and registration, then approve or reject."
        aside={
          requests.length > 0 && (
            <dl className="readout">
              <div className="is-warn">
                <dt>In queue</dt>
                <dd>{pad2(requests.length)}</dd>
              </div>
              <div>
                <dt>Oldest wait</dt>
                <dd>{daysSince(requests[0].createdAtUtc)}d</dd>
              </div>
            </dl>
          )
        }
        meta={['Review queue', 'First in, first reviewed', `${requests.length} awaiting decision`]}
      />

      <div className="adm-notice" role="status">
        {action.notice && <Notice notice={action.notice} />}
      </div>

      {requests.length === 0 ? (
        <div className="ws-empty queue-empty">
          <h2>The queue is clear.</h2>
          <p>No organization is waiting for review. New registrations appear here as they arrive.</p>
          <Button variant="outline" to={PATHS.adminOrganizations}>
            Open the registry
          </Button>
        </div>
      ) : (
        <ol role="list" className="queue" aria-label="Review queue, oldest first">
          {requests.map((r, i) => (
            <li key={r.id}>
              <ReviewCard
                request={r}
                position={i + 1}
                pending={action.pendingId === r.id}
                busy={action.busy}
                onDecide={(approve) => decide(r, approve)}
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

type CardProps = {
  request: PendingOrganization
  position: number
  pending: boolean
  busy: boolean
  onDecide: (approve: boolean) => void
}

function ReviewCard({ request: r, position, pending, busy, onDecide }: CardProps) {
  const reduced = useReducedMotion()
  const next = position === 1
  const titleId = `req-${r.id}`

  return (
    <motion.article
      className={cn('review', next && 'is-next')}
      aria-labelledby={titleId}
      initial={reduced ? false : { opacity: 0, y: 20 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.5, ease: ease.out, delay: 0.1 + position * 0.06 }}
    >
      <div className="review__pos">
        <span className="review__pos-label t-label">{next ? 'Next up' : 'Queue'}</span>
        <span className="review__num">
          <span className="visually-hidden">Position </span>
          {pad2(position)}
        </span>
        <span className="review__wait">
          <span className="t-label">Waiting</span>
          {waited(r.createdAtUtc)}
        </span>
      </div>

      <div className="review__body">
        <p className="review__kicker">
          <StatusChip tone="warning">Pending</StatusChip>
          <span>{ORGANIZATION_TYPE_LABELS[r.type]}</span>
        </p>
        <h2 id={titleId} className="review__name">
          {r.name}
        </h2>
        <dl className="review__facts">
          <div>
            <dt>License</dt>
            <dd>
              <code>{r.licenseNumber}</code>
            </dd>
          </div>
          <div>
            <dt>Submitted</dt>
            <dd>
              <time dateTime={r.createdAtUtc}>
                {formatDate(r.createdAtUtc)} <span className="review__ago">· {formatAgo(r.createdAtUtc)}</span>
              </time>
              <code className="review__utc">{formatUtc(r.createdAtUtc)} UTC</code>
            </dd>
          </div>
        </dl>
      </div>

      <div className={cn('review__decide', next && 'on-dark')}>
        <p className="review__decide-label t-label">Decision</p>
        <div className="review__buttons">
          <Button
            variant={next ? 'on-dark' : 'primary'}
            size="sm"
            iconStart={<Check />}
            loading={pending}
            disabled={busy}
            onClick={() => onDecide(true)}
          >
            Approve<span className="visually-hidden"> {r.name}</span>
          </Button>
          <Button
            variant="outline"
            size="sm"
            iconStart={<X />}
            disabled={busy}
            onClick={() => window.confirm(`Reject ${r.name}? They will not be able to sign in.`) && onDecide(false)}
          >
            Reject<span className="visually-hidden"> {r.name}</span>
          </Button>
        </div>
      </div>
    </motion.article>
  )
}
