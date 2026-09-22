import { motion, useReducedMotion } from 'framer-motion'
import { ArrowLeft, ArrowRight, CircleCheckBig, CircleDashed, ShieldCheck } from 'lucide-react'
import { Link, useParams } from 'react-router-dom'
import { PATHS, verifyHandoverPath } from '../../app/routes'
import { CLAIM_STATUS_META, NEXT_STEP_META, refOf } from '../../components/operations/presentation'
import { Button } from '../../components/ui/Button'
import { StatusChip } from '../../components/ui/StatusChip'
import { PageLoading, PageMessage } from '../../components/workspace/PageState'
import { codeOf } from '../../lib/api/client'
import { getCourierTask, type HandoverType } from '../../lib/api/courier'
import { useLoad } from '../../lib/api/useLoad'
import { cn } from '../../lib/cn'
import { describeExpiry, formatAbsolute } from '../../lib/expiry'
import { ease, revealVariants, staggerVariants } from '../../lib/motion'
import { Route } from './Route'
import './courier.css'

const STAGES: HandoverType[] = ['Pickup', 'Delivery']

export function TaskDetails() {
  const { id = '' } = useParams()
  const reduced = useReducedMotion()
  const load = useLoad(id, (signal) => getCourierTask(id, signal))

  if (codeOf(load.error) === 'task.not_found') return <MissingTask />
  if (load.error !== undefined && !load.data) return <PageMessage title="We couldn’t load this task." onRetry={load.reload} />
  if (!load.data) return <PageLoading label="Loading task…" />

  const t = load.data
  const status = CLAIM_STATUS_META[t.status]
  const step = NEXT_STEP_META[t.nextStep]
  const expiry = describeExpiry(t.expiresAtUtc)
  const done = step.lane === 'done'
  const enter = (delay: number) => ({
    initial: reduced ? false : { opacity: 0, y: 20 },
    animate: { opacity: 1, y: 0 },
    transition: { duration: 0.6, ease: ease.out, delay },
  })

  return (
    <div className="container ws-page task">
      <Link to={PATHS.courierTasks} className="ws-back">
        <ArrowLeft aria-hidden="true" />
        My tasks
      </Link>

      <header className="task__head">
        <p className="task__kicker t-label">
          Dispatch ticket <span className="t-data">{refOf(t.claimId)}</span> · {t.donorName} → {t.beneficiaryName}
        </p>
        <h1 className="task__title">{t.donationTitle}</h1>
        <div className="task__status">
          <StatusChip tone={status.tone}>{status.label}</StatusChip>
        </div>
      </header>

      <div className="task__grid">
        <motion.section className="task__route on-dark grain" aria-labelledby="route-title" {...enter(0.1)}>
          <h2 id="route-title" className="task__panel-title t-label">
            Route
          </h2>
          <Route task={t} size="large" />
          <div className="dispatch__tear task__tear" aria-hidden="true" />
          <dl className="task__facts">
            <div>
              <dt>Expiry</dt>
              <dd>
                {!done && <span className={cn('task__expiry', expiry.urgency === 'critical' && 'is-urgent')}>{expiry.relative}</span>}
                <time dateTime={t.expiresAtUtc}>{expiry.absolute}</time>
              </dd>
            </div>
          </dl>
        </motion.section>

        <div className="task__side">
          <motion.section className={cn('next-step', done && 'next-step--done')} aria-labelledby="next-title" {...enter(0.2)}>
            <h2 id="next-title" className="next-step__kicker t-label">
              Next step
            </h2>
            <p className="next-step__label">
              {done && <CircleCheckBig aria-hidden="true" />}
              {step.label}
            </p>
            <p className="next-step__detail">{step.detail}</p>
            {step.verify && step.action && (
              <Button
                size="lg"
                variant="primary"
                to={verifyHandoverPath(t.claimId)}
                iconEnd={<ArrowRight />}
                className="next-step__action"
              >
                {step.action}
              </Button>
            )}
          </motion.section>

          <section className="evidence" aria-labelledby="evidence-title">
            <h2 id="evidence-title" className="evidence__title">
              Handover evidence
            </h2>
            <motion.ul
              role="list"
              className="evidence__list"
              variants={staggerVariants}
              initial={reduced ? false : 'hidden'}
              whileInView="visible"
              viewport={{ once: true }}
            >
              {STAGES.map((type) => {
                const ev = t.evidence.find((e) => e.type === type)
                return (
                  <motion.li key={type} className={cn('evidence__item', ev && 'is-verified')} variants={revealVariants}>
                    <span className="evidence__icon" aria-hidden="true">
                      {ev ? <ShieldCheck /> : <CircleDashed />}
                    </span>
                    <span className="evidence__text">
                      <span className="evidence__label">{ev ? `${type} verified` : `${type} evidence`}</span>
                      {ev ? (
                        <time className="evidence__state" dateTime={ev.verifiedAtUtc}>
                          {formatAbsolute(ev.verifiedAtUtc)}
                        </time>
                      ) : (
                        <span className="evidence__state">Not yet recorded</span>
                      )}
                    </span>
                  </motion.li>
                )
              })}
            </motion.ul>
          </section>
        </div>
      </div>
    </div>
  )
}

function MissingTask() {
  return (
    <div className="container ws-page">
      <div className="ws-empty">
        <h1 className="t-h2">This task isn’t on your board.</h1>
        <p>It may have been reassigned, or the link is incomplete.</p>
        <Button to={PATHS.courierTasks} iconStart={<ArrowLeft />}>
          Back to my tasks
        </Button>
      </div>
    </div>
  )
}
