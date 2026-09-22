import { motion, useReducedMotion } from 'framer-motion'
import { Ban, BadgeCheck, Clock, Lock, MapPin, ShieldAlert, type LucideIcon } from 'lucide-react'
import type { ReactNode } from 'react'
import { BotanicalBranch, BotanicalCorner, BotanicalDecoration } from '../../components/brand/Botanical'
import { SectionEyebrow } from '../../components/ui/SectionEyebrow'
import { StatusChip, type StatusTone } from '../../components/ui/StatusChip'
import { PageLoading, PageMessage } from '../../components/workspace/PageState'
import { codeOf } from '../../lib/api/client'
import { getMyOrganization } from '../../lib/api/organization'
import { useLoad } from '../../lib/api/useLoad'
import { cn } from '../../lib/cn'
import { ease } from '../../lib/motion'
import { ORGANIZATION_TYPE_LABELS, type OrganizationStatus } from '../../types/organization'
import './organization.css'

type StatusView = { tone: StatusTone; icon: LucideIcon; title: string; body: string; steps: ('done' | 'current' | 'stopped' | 'todo')[] }

// Presentation for each account status. Read-only: status changes are made by FoodLoop admins, never here.
const STATUS_VIEW: Record<OrganizationStatus, StatusView> = {
  Active: {
    tone: 'success',
    icon: BadgeCheck,
    title: 'Verified and active',
    body: 'Your organization is approved and can use every FoodLoop workflow.',
    steps: ['done', 'done', 'done'],
  },
  Pending: {
    tone: 'warning',
    icon: Clock,
    title: 'Under review',
    body: 'An administrator is checking your registration and licence.',
    steps: ['done', 'current', 'todo'],
  },
  Suspended: {
    tone: 'danger',
    icon: ShieldAlert,
    title: 'Temporarily suspended',
    body: 'New activity is paused while an administrator looks into your account. Your existing records stay visible, read-only.',
    steps: ['done', 'done', 'stopped'],
  },
  Rejected: {
    tone: 'danger',
    icon: Ban,
    title: 'Application not approved',
    body: 'Your application could not be verified. Contact FoodLoop support to understand what is needed.',
    steps: ['done', 'stopped', 'todo'],
  },
}
const STEP_LABELS = ['Application received', 'Registration reviewed', 'Workspace enabled']
const STEP_WORD = { done: 'Done', current: 'In progress', stopped: 'Stopped', todo: 'Not yet' } as const

const dateFmt = new Intl.DateTimeFormat('en-US', { day: 'numeric', month: 'long', year: 'numeric' })

/** GET /api/organization — the signed-in member's organization, read-only by design. */
export function MyOrganization() {
  const reduced = useReducedMotion()
  const load = useLoad('organization', getMyOrganization)

  if (codeOf(load.error) === 'organization.not_found')
    return <PageMessage title="No organization profile to show.">This account isn’t linked to an active organization.</PageMessage>
  if (load.error !== undefined && !load.data) return <PageMessage title="We couldn’t load your organization." onRetry={load.reload} />
  if (!load.data) return <PageLoading label="Loading your organization…" />

  const org = load.data
  const view = STATUS_VIEW[org.status]
  const StatusIcon = view.icon
  const initials = org.name
    .split(' ')
    .slice(0, 2)
    .map((w) => w[0])
    .join('')

  const enter = (delay: number) => ({
    initial: reduced ? false : { opacity: 0, y: 16 },
    animate: { opacity: 1, y: 0 },
    transition: { duration: 0.55, ease: ease.out, delay },
  })

  return (
    <div className="container ws-page org">
      <div className="org__grid">
        {/* ---- Identity ---- */}
        <motion.section className="org-id on-dark grain" aria-labelledby="org-name" {...enter(0)}>
          <BotanicalDecoration>
            <BotanicalCorner position="top-right" className="org-id__contours" />
            <BotanicalBranch className="org-id__branch" draw={false} />
          </BotanicalDecoration>

          <div className="org-id__emblem" aria-hidden="true">
            {initials}
          </div>
          <SectionEyebrow className="org-id__eyebrow">My organization</SectionEyebrow>
          <h1 id="org-name" className="org-id__name">
            {org.name}
          </h1>
          <p className="org-id__type">{ORGANIZATION_TYPE_LABELS[org.type]} organization</p>
          <div className="org-id__status">
            <StatusChip tone={view.tone} icon={<StatusIcon />}>
              {org.status}
            </StatusChip>
          </div>
          <dl className="org-id__facts">
            <div>
              <dt>Member since</dt>
              <dd>{dateFmt.format(new Date(org.createdAt))}</dd>
            </div>
            <div>
              <dt>Access</dt>
              <dd>{org.isReadOnly ? 'Read-only' : 'Full'}</dd>
            </div>
          </dl>
        </motion.section>

        <div className="org__content">
          {/* ---- Status ---- */}
          <motion.section className={cn('org-status', `org-status--${view.tone}`)} aria-labelledby="status-title" {...enter(0.08)}>
            <div className="org-status__head">
              <span className="org-status__icon" aria-hidden="true">
                <StatusIcon />
              </span>
              <div>
                <h2 id="status-title" className="org-status__title">
                  {view.title}
                </h2>
                <p className="org-status__body">{view.body}</p>
              </div>
            </div>
            <ol role="list" className="org-steps">
              {STEP_LABELS.map((label, i) => (
                <li key={label} className={`org-step is-${view.steps[i]}`}>
                  <span className="org-step__dot" aria-hidden="true" />
                  <span className="org-step__label">{label}</span>
                  <span className="org-step__state">{STEP_WORD[view.steps[i]]}</span>
                </li>
              ))}
            </ol>
          </motion.section>

          {/* ---- Registration (authoritative, read-only) ---- */}
          <motion.section className="org-block" aria-labelledby="reg-title" {...enter(0.14)}>
            <header className="org-block__head">
              <h2 id="reg-title" className="org-block__title">
                Registration &amp; licence
              </h2>
              <p className="org-block__note">
                <Lock aria-hidden="true" />
                Verified by FoodLoop · read-only
              </p>
            </header>
            <dl className="org-record">
              <ReadOnly label="Licence number" value={org.licenseNumber} mono />
              <ReadOnly label="Organization type" value={ORGANIZATION_TYPE_LABELS[org.type]} />
              <ReadOnly label="Account status" value={org.status} />
            </dl>
          </motion.section>

          {/* ---- Location ---- */}
          <motion.section className="org-block" aria-labelledby="contact-title" {...enter(0.2)}>
            <header className="org-block__head">
              <h2 id="contact-title" className="org-block__title">
                Location
              </h2>
            </header>
            <dl className="org-contact">
              <Info icon={MapPin} label="Address" value={org.address || '—'} />
            </dl>
          </motion.section>
        </div>
      </div>
    </div>
  )
}

function ReadOnly({ label, value, mono }: { label: string; value: string; mono?: boolean }) {
  return (
    <div className="org-record__row">
      <dt>{label}</dt>
      <dd className={cn(mono && 't-data org-record__mono')}>
        {value}
        <Lock aria-hidden="true" className="org-record__lock" />
      </dd>
    </div>
  )
}

function Info({ icon: Icon, label, value }: { icon: LucideIcon; label: string; value: ReactNode }) {
  return (
    <div className="org-contact__row">
      <dt>
        <Icon aria-hidden="true" />
        {label}
      </dt>
      <dd>{value}</dd>
    </div>
  )
}
