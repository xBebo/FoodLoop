import { motion, useReducedMotion } from 'framer-motion'
import { ArrowRight } from 'lucide-react'
import { courierTaskPath } from '../../app/routes'
import { FoodLoopMark } from '../../components/brand/FoodLoopMark'
import { CLAIM_STATUS_META, NEXT_STEP_META, TASK_LANES, refOf } from '../../components/operations/presentation'
import { Button } from '../../components/ui/Button'
import { SectionEyebrow } from '../../components/ui/SectionEyebrow'
import { StatusChip } from '../../components/ui/StatusChip'
import { PageLoading, PageMessage } from '../../components/workspace/PageState'
import { getCourierTasks, type CourierTask } from '../../lib/api/courier'
import { useLoad } from '../../lib/api/useLoad'
import { cn } from '../../lib/cn'
import { describeExpiry } from '../../lib/expiry'
import { ease } from '../../lib/motion'
import { Route } from './Route'
import './courier.css'

export function MyTasks() {
  const load = useLoad('tasks', getCourierTasks)
  if (load.error !== undefined && !load.data) return <PageMessage title="We couldn’t load your tasks." onRetry={load.reload} />
  if (!load.data) return <PageLoading label="Loading your tasks…" />
  const tasks = load.data
  const lanes = TASK_LANES.map((l) => ({ ...l, tasks: tasks.filter((t) => NEXT_STEP_META[t.nextStep].lane === l.id) }))

  return (
    <div className="courier-floor on-dark grain">
      <div className="container ws-page">
        <header className="courier-intro">
          <div className="courier-intro__text">
            <SectionEyebrow>Courier workspace</SectionEyebrow>
            <h1 className="ws-intro__title">
              My <em>tasks</em>
            </h1>
            <p className="t-lead ws-intro__lead">Your dispatch board. Collect with the donor’s code, deliver with the beneficiary’s.</p>
          </div>
          {/* Departure-board summary: counted from the tasks below. */}
          <dl className="manifest">
            {lanes.map((l) => (
              <div key={l.id} className={cn('manifest__cell', `manifest__cell--${l.id}`)}>
                <dt>{l.label}</dt>
                <dd className="t-data">{String(l.tasks.length).padStart(2, '0')}</dd>
              </div>
            ))}
          </dl>
        </header>

        <div className="board">
          {lanes.map((l) => (
            <section key={l.id} className={cn('lane', `lane--${l.id}`)} aria-labelledby={`lane-${l.id}`}>
              <h2 id={`lane-${l.id}`} className="lane__title">
                {l.label}
                <span className="lane__count t-data">
                  {l.tasks.length}
                  <span className="visually-hidden"> {l.tasks.length === 1 ? 'task' : 'tasks'}</span>
                </span>
              </h2>
              {l.tasks.length > 0 ? (
                <ol role="list" className="lane__list">
                  {l.tasks.map((t, i) => (
                    <li key={t.claimId}>
                      <DispatchTicket task={t} index={i} />
                    </li>
                  ))}
                </ol>
              ) : (
                <p className="lane__empty">Nothing here right now.</p>
              )}
            </section>
          ))}
        </div>
      </div>
    </div>
  )
}

function DispatchTicket({ task: t, index }: { task: CourierTask; index: number }) {
  const reduced = useReducedMotion()
  const status = CLAIM_STATUS_META[t.status]
  const step = NEXT_STEP_META[t.nextStep]
  const expiry = describeExpiry(t.expiresAtUtc)
  const done = step.lane === 'done'

  return (
    <motion.article
      className={cn('dispatch', `dispatch--${step.lane}`)}
      aria-labelledby={`task-${t.claimId}`}
      initial={reduced ? false : { opacity: 0, y: 28 }}
      whileInView={{ opacity: 1, y: 0 }}
      viewport={{ once: true, amount: 0.2 }}
      transition={{ duration: 0.55, ease: ease.out, delay: index * 0.08 }}
    >
      <header className="dispatch__stub">
        <span className="dispatch__ref t-data">{refOf(t.claimId)}</span>
        <StatusChip tone={status.tone}>{status.label}</StatusChip>
        <FoodLoopMark className="dispatch__mark" />
      </header>

      <div className="dispatch__body">
        <h3 id={`task-${t.claimId}`} className="dispatch__title">
          {t.donationTitle}
        </h3>
        <Route task={t} />
      </div>

      <div className="dispatch__tear" aria-hidden="true" />

      <dl className="dispatch__facts">
        <div>
          <dt>Expiry</dt>
          <dd className={cn(!done && expiry.urgency === 'critical' && 'is-urgent')}>
            <time dateTime={t.expiresAtUtc}>{done ? expiry.absolute : expiry.relative}</time>
          </dd>
        </div>
      </dl>

      <div className="dispatch__next">
        <p className="dispatch__step">
          <span className="t-label">Next step</span>
          <strong>{step.label}</strong>
        </p>
        <Button variant={done ? 'outline' : 'primary'} size="sm" to={courierTaskPath(t.claimId)} iconEnd={<ArrowRight />}>
          Open task<span className="visually-hidden">: {t.donationTitle}</span>
        </Button>
      </div>
    </motion.article>
  )
}
