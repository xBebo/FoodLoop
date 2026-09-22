import { AlertCircle, ArrowLeft, CheckCircle2, ShieldCheck } from 'lucide-react'
import { useRef, useState, type FormEvent, type ReactNode } from 'react'
import { Link, useParams } from 'react-router-dom'
import { PATHS, courierTaskPath } from '../../app/routes'
import { NEXT_STEP_META, refOf } from '../../components/operations/presentation'
import { Button } from '../../components/ui/Button'
import { codeOf } from '../../lib/api/client'
import { getCourierTask, verifyHandover, type HandoverType } from '../../lib/api/courier'
import { useLoad } from '../../lib/api/useLoad'
import { cn } from '../../lib/cn'
import './courier.css'

const CODE_LENGTH = 64
const STAGES: HandoverType[] = ['Pickup', 'Delivery']

type Submit = { status: 'idle' } | { status: 'submitting' } | { status: 'verified'; type: HandoverType } | { status: 'failed'; message: string }

function verifyFailure(error: unknown): string {
  switch (codeOf(error)) {
    case 'handover.invalid_code':
      return 'That code wasn’t accepted. It may be mistyped, expired, already used, or replaced by a newer code — ask for a fresh one.'
    case 'handover.invalid_state':
    case 'task.conflict':
      return 'This task has moved on since you opened it. Check the task for its current step.'
    case 'task.not_found':
      return 'This task is no longer assigned to you.'
    case 'antiforgery.invalid':
      return 'Your session changed in the meantime. Nothing was verified — please try again.'
    case 'network':
      return 'We couldn’t reach FoodLoop. Open the task to see whether the handover was recorded before trying again.'
    default:
      return 'Something went wrong on our side and nothing was verified. Please try again in a moment.'
  }
}

/** Focus mode. The step comes from the server's nextStep; the server checks the code and records the evidence. */
export function VerifyHandover() {
  const { id = '' } = useParams()
  const load = useLoad(id, (signal) => getCourierTask(id, signal))
  const [code, setCode] = useState('')
  const [error, setError] = useState('')
  const [submit, setSubmit] = useState<Submit>({ status: 'idle' })
  const inFlight = useRef(false)
  const input = useRef<HTMLTextAreaElement>(null)

  if (codeOf(load.error) === 'task.not_found')
    return (
      <Scene>
        <h1 className="verify__title">This task isn’t on your board.</h1>
        <Button variant="on-dark" to={PATHS.courierTasks} iconStart={<ArrowLeft />}>
          Back to my tasks
        </Button>
      </Scene>
    )
  if (load.error !== undefined && !load.data)
    return (
      <Scene>
        <h1 className="verify__title">We couldn’t load this task.</h1>
        <Button variant="on-dark" onClick={load.reload}>
          Try again
        </Button>
      </Scene>
    )
  if (!load.data)
    return (
      <Scene>
        <p className="verify__hint" role="status">
          Loading task…
        </p>
      </Scene>
    )

  const task = load.data
  const stage = NEXT_STEP_META[task.nextStep].verify
  const holder = stage === 'Pickup' ? 'donor' : 'beneficiary'
  const length = code.replace(/\s/g, '').length
  const submitting = submit.status === 'submitting'

  async function onSubmit(e: FormEvent) {
    e.preventDefault()
    if (inFlight.current || !stage) return
    if (!code.trim()) {
      setError('Enter the handover code to continue.')
      input.current?.focus()
      return
    }
    setError('')
    inFlight.current = true
    setSubmit({ status: 'submitting' })
    try {
      await verifyHandover(task.claimId, stage, code)
      setSubmit({ status: 'verified', type: stage })
      setCode('')
    } catch (err) {
      setSubmit({ status: 'failed', message: verifyFailure(err) })
    } finally {
      inFlight.current = false
      load.reload()
    }
  }

  return (
    <Scene back={task.claimId}>
      <p className="verify__context">
        <strong>{task.donationTitle}</strong>
        <span>
          {task.donorName} → {task.beneficiaryName}
        </span>
      </p>

      {submit.status === 'verified' && (
        <div role="status" className="verify__result">
          <p className="ws-notice">
            <CheckCircle2 aria-hidden="true" />
            {submit.type} verified and recorded. {submit.type === 'Pickup' ? 'Head to the beneficiary next.' : 'This delivery is complete.'}
          </p>
        </div>
      )}

      {stage ? (
        <>
          <ol role="list" className="verify__stages" aria-label="Handover stages">
            {STAGES.map((s) => (
              <li key={s} className={cn('verify__stage', s === stage && 'is-current')} aria-current={s === stage ? 'step' : undefined}>
                {s}
              </li>
            ))}
          </ol>

          <h1 className="verify__title">Verify {stage.toLowerCase()}</h1>

          <form className="verify__form" noValidate onSubmit={onSubmit}>
            <label htmlFor="handover-code" className="verify__label">
              {stage} handover code
            </label>
            <p id="handover-code-hint" className="verify__hint">
              Paste or type the code the {holder} shows you. Spaces are fine.
            </p>
            <textarea
              ref={input}
              id="handover-code"
              name="handover-code"
              className="verify__input"
              rows={3}
              value={code}
              onChange={(e) => {
                setCode(e.target.value)
                if (error) setError('')
                if (submit.status === 'failed') setSubmit({ status: 'idle' })
              }}
              spellCheck={false}
              autoComplete="off"
              autoCapitalize="characters"
              autoCorrect="off"
              aria-invalid={error ? true : undefined}
              aria-describedby={cn('handover-code-hint', error && 'handover-code-error')}
            />
            {/* Presentation only: the counter never validates or blocks submission. */}
            <p className="verify__counter t-data" aria-hidden="true">
              {length} / {CODE_LENGTH}
            </p>
            {error && (
              <p id="handover-code-error" className="verify__error">
                <AlertCircle aria-hidden="true" />
                {error}
              </p>
            )}
            <Button
              type="submit"
              variant="on-dark"
              size="lg"
              iconStart={<ShieldCheck />}
              loading={submitting}
              disabled={submitting}
              className="verify__submit"
            >
              {submitting ? 'Verifying…' : `Verify ${stage.toLowerCase()}`}
            </Button>
          </form>

          <div role="status" className="verify__result">
            {submit.status === 'failed' && (
              <p className="ws-notice">
                <AlertCircle aria-hidden="true" />
                {submit.message}
              </p>
            )}
          </div>

          <p className="verify__note">FoodLoop checks the code and records the {stage.toLowerCase()} evidence for this task.</p>
        </>
      ) : (
        <>
          <h1 className="verify__title">Nothing to verify</h1>
          <p className="verify__hint">{NEXT_STEP_META[task.nextStep].detail}</p>
          <Button variant="on-dark" to={courierTaskPath(task.claimId)} iconStart={<ArrowLeft />}>
            Back to the task
          </Button>
        </>
      )}
    </Scene>
  )
}

function Scene({ back, children }: { back?: string; children: ReactNode }) {
  return (
    <div className="verify on-dark grain">
      <div className="container verify__inner">
        {back && (
          <Link to={courierTaskPath(back)} className="ws-back verify__back">
            <ArrowLeft aria-hidden="true" />
            Task <span className="t-data">{refOf(back)}</span>
          </Link>
        )}
        <div className="verify__panel">{children}</div>
      </div>
    </div>
  )
}
