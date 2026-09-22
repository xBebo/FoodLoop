import { motion, useReducedMotion } from 'framer-motion'
import { AlertCircle, KeyRound } from 'lucide-react'
import { useRef, useState } from 'react'
import { WORKSPACE_ROLES } from '../../app/routes'
import { FoodLoopMark } from '../../components/brand/FoodLoopMark'
import { CLAIM_STATUS_META, HANDOVER_SHOWN_BY, refOf } from '../../components/operations/presentation'
import { Button } from '../../components/ui/Button'
import { SectionEyebrow } from '../../components/ui/SectionEyebrow'
import { StatusChip } from '../../components/ui/StatusChip'
import { PageLoading, PageMessage } from '../../components/workspace/PageState'
import { useWorkspaceRole } from '../../components/workspace/role'
import { codeOf } from '../../lib/api/client'
import { getHandoverTasks, issueHandoverCode, type HandoverTask, type IssuedCode } from '../../lib/api/courier'
import { useLoad } from '../../lib/api/useLoad'
import { cn } from '../../lib/cn'
import { ease } from '../../lib/motion'
import { useSession } from '../../lib/session/context'
import { HandoverPass } from './HandoverPass'
import './handover.css'

function issueFailure(error: unknown): string {
  switch (codeOf(error)) {
    case 'handover.not_ready':
      return 'This handover isn’t ready for a code right now — a courier may not be assigned yet, or the task has moved on.'
    case 'claim.not_found':
      return 'This task is no longer available to your organization.'
    case 'claim.conflict':
      return 'This task just changed. Refresh and try again.'
    case 'antiforgery.invalid':
      return 'Your session changed in the meantime. No code was issued — please try again.'
    case 'network':
      return 'We couldn’t reach FoodLoop. Try again — any earlier code is replaced when a new one is issued.'
    default:
      return 'Something went wrong on our side and no code was issued. Please try again in a moment.'
  }
}

/**
 * Handover tasks from GET /api/handover/tasks. Issuing returns the raw code exactly once; it is held in this component's
 * memory only (never storage, the URL or history state), so leaving the page discards it and a new code must be issued.
 */
export function HandoverCodes() {
  const reduced = useReducedMotion()
  const role = useWorkspaceRole()
  const { state } = useSession()
  const organizationName = (state.status === 'authenticated' && state.session.organization?.name) || 'Your organization'
  const roleLabel = WORKSPACE_ROLES.find((r) => r.id === role)!.label
  const load = useLoad('handover', getHandoverTasks)
  const [issued, setIssued] = useState<{ task: HandoverTask; code: IssuedCode } | null>(null)
  const [pending, setPending] = useState<string | null>(null)
  const [failure, setFailure] = useState<{ claimId: string; message: string } | null>(null)
  const inFlight = useRef(false)

  async function issue(task: HandoverTask) {
    if (inFlight.current) return
    inFlight.current = true
    setPending(task.claimId)
    setFailure(null)
    try {
      setIssued({ task, code: await issueHandoverCode(task.claimId, task.type) })
    } catch (error) {
      setFailure({ claimId: task.claimId, message: issueFailure(error) })
    } finally {
      inFlight.current = false
      setPending(null)
      load.reload()
    }
  }

  if (issued) return <HandoverPass donationTitle={issued.task.donationTitle} issued={issued.code} onClose={() => setIssued(null)} />
  if (codeOf(load.error) === 'organization.not_active')
    return <PageMessage title="Handover codes aren’t available.">Your organization must be active to issue handover codes.</PageMessage>
  if (load.error !== undefined && !load.data) return <PageMessage title="We couldn’t load your handovers." onRetry={load.reload} />
  if (!load.data) return <PageLoading label="Loading handovers…" />

  // Ready tasks first; the server's canIssue decides which those are.
  const tasks = [...load.data].sort((a, b) => Number(b.canIssue) - Number(a.canIssue))

  return (
    <div className="container ws-page codes">
      <header className="ws-intro">
        <SectionEyebrow>{roleLabel} workspace</SectionEyebrow>
        <h1 className="ws-intro__title">
          Handover <em>codes</em>
        </h1>
        <p className="t-lead ws-intro__lead">
          Codes {organizationName} shows to the courier. Each one confirms a single handover and can be shown only once — issue a new
          one whenever you need it.
        </p>
      </header>

      {tasks.length > 0 ? (
        <ol role="list" className="codes-list">
          {tasks.map((t, i) => {
            const status = CLAIM_STATUS_META[t.status]
            return (
              <motion.li
                key={t.claimId}
                initial={reduced ? false : { opacity: 0, y: 22 }}
                whileInView={{ opacity: 1, y: 0 }}
                viewport={{ once: true, amount: 0.3 }}
                transition={{ duration: 0.5, ease: ease.out, delay: Math.min(i, 8) * 0.07 }}
              >
                <article className={cn('code-card', t.canIssue ? 'code-card--active' : 'code-card--used')} aria-labelledby={`code-${t.claimId}`}>
                  <div className="code-card__stub" aria-hidden="true">
                    <FoodLoopMark className="code-card__mark" />
                    <span className="code-card__type">{t.type}</span>
                  </div>
                  <div className="code-card__body">
                    <div className="code-card__top">
                      <p className="code-card__kind t-label">
                        {t.type} handover · <span className="t-data">{refOf(t.claimId)}</span>
                      </p>
                      <StatusChip tone={status.tone}>{status.label}</StatusChip>
                    </div>
                    <h2 id={`code-${t.claimId}`} className="code-card__title">
                      {t.donationTitle}
                    </h2>
                    <p className="code-card__hint">
                      {t.canIssue ? HANDOVER_SHOWN_BY[t.type] : `No ${t.type.toLowerCase()} code is needed for this claim right now.`}
                    </p>
                    {t.canIssue && (
                      <Button
                        size="sm"
                        iconStart={<KeyRound />}
                        loading={pending === t.claimId}
                        disabled={pending !== null}
                        onClick={() => issue(t)}
                        className="code-card__action"
                      >
                        {pending === t.claimId ? 'Issuing…' : `Issue ${t.type.toLowerCase()} code`}
                        <span className="visually-hidden"> for {t.donationTitle}</span>
                      </Button>
                    )}
                    {failure?.claimId === t.claimId && (
                      <p className="ws-notice" role="status">
                        <AlertCircle aria-hidden="true" />
                        {failure.message}
                      </p>
                    )}
                  </div>
                </article>
              </motion.li>
            )
          })}
        </ol>
      ) : (
        <div className="ws-empty">
          <h2>No handovers yet.</h2>
          <p>Claims involving your organization appear here. A code can be issued once a courier is assigned.</p>
        </div>
      )}
    </div>
  )
}
