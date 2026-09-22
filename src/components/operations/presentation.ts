// UI presentation for claims, courier tasks and handovers: labels, tones, groupings.
// Nothing here decides lifecycle: it only arranges what the API already says (status, server timeline, nextStep).
import type { TimelineEvent, TimelineKind } from '../../lib/api/claims'
import type { CourierNextStep, HandoverType } from '../../lib/api/courier'
import type { ClaimStatus } from '../../types/claim'
import type { StatusTone } from '../ui/StatusChip'

export type ClaimPhase = 'active' | 'done' | 'ended'

export const CLAIM_STATUS_META: Record<ClaimStatus, { label: string; tone: StatusTone; phase: ClaimPhase }> = {
  Booked: { label: 'Booked', tone: 'success', phase: 'active' },
  PickupPending: { label: 'Pickup pending', tone: 'warning', phase: 'active' },
  PickedUp: { label: 'Picked up', tone: 'info', phase: 'active' },
  InTransit: { label: 'In transit', tone: 'info', phase: 'active' },
  Delivered: { label: 'Delivered', tone: 'complete', phase: 'done' },
  Closed: { label: 'Closed', tone: 'complete', phase: 'done' },
  Cancelled: { label: 'Cancelled', tone: 'neutral', phase: 'ended' },
  Failed: { label: 'Failed', tone: 'danger', phase: 'ended' },
}

/** Tone family for a server timeline event (the event's own label is the server's). */
export const TIMELINE_PHASE: Record<TimelineKind, ClaimPhase> = {
  Claimed: 'active',
  CourierAssigned: 'active',
  CourierReassigned: 'active',
  PickupVerified: 'active',
  DeliveryVerified: 'done',
  Closed: 'done',
  Cancelled: 'ended',
}

/** The journey stages, in order, each reached only by the server timeline events listed. Cancelled is an ending, not a stage. */
export const JOURNEY_STAGES: { id: string; label: string; kinds: TimelineKind[] }[] = [
  { id: 'claimed', label: 'Claimed', kinds: ['Claimed'] },
  { id: 'courier', label: 'Courier assigned', kinds: ['CourierAssigned', 'CourierReassigned'] },
  { id: 'pickup', label: 'Picked up', kinds: ['PickupVerified'] },
  { id: 'delivery', label: 'Delivered', kinds: ['DeliveryVerified'] },
  { id: 'closed', label: 'Closed', kinds: ['Closed'] },
]

export type StageView = { id: string; label: string; event?: TimelineEvent; state: 'reached' | 'current' | 'future' }

/**
 * Journey read straight from the server timeline: a stage is reached only if one of its events exists.
 * The latest reached stage is "current" unless the claim was cancelled (then `terminal` carries that event).
 */
export function journeyOf(timeline: TimelineEvent[]) {
  const terminal = timeline.find((e) => e.kind === 'Cancelled')
  const eventOf = (kinds: TimelineKind[]) => timeline.findLast((e) => kinds.includes(e.kind))
  const lastReached = JOURNEY_STAGES.reduce((acc, s, i) => (eventOf(s.kinds) ? i : acc), -1)
  const stages: StageView[] = JOURNEY_STAGES.map((s, i) => ({
    id: s.id,
    label: s.label,
    event: eventOf(s.kinds),
    state: i < lastReached || (i === lastReached && terminal) ? 'reached' : i === lastReached ? 'current' : 'future',
  }))
  return { stages, lastReached, terminal }
}

export type TaskLane = 'collect' | 'deliver' | 'done'

export const TASK_LANES: { id: TaskLane; label: string }[] = [
  { id: 'collect', label: 'To collect' },
  { id: 'deliver', label: 'To deliver' },
  { id: 'done', label: 'Completed' },
]

/** Copy for the backend's nextStep. The server decides the step; this only words it. */
export const NEXT_STEP_META: Record<CourierNextStep, { label: string; detail: string; lane: TaskLane; verify?: HandoverType; action?: string }> = {
  VerifyPickup: {
    label: 'Verify pickup',
    detail: 'At the donor, ask for their pickup handover code and enter it to record the pickup.',
    lane: 'collect',
    verify: 'Pickup',
    action: 'Verify pickup',
  },
  VerifyDelivery: {
    label: 'Verify delivery',
    detail: 'Pickup is verified. At the beneficiary, ask for their delivery handover code and enter it to record the delivery.',
    lane: 'deliver',
    verify: 'Delivery',
    action: 'Verify delivery',
  },
  Completed: { label: 'Completed', detail: 'Both handovers are verified. Nothing left to do on this task.', lane: 'done' },
  None: { label: 'No action required', detail: 'This task needs nothing from you right now.', lane: 'done' },
}

/** Where the courier stands on the route, from the server's nextStep. 0 at donor · 1 on the road · 2 at beneficiary. */
export const routeLegOf = (step: CourierNextStep) => (step === 'Completed' ? 2 : step === 'VerifyDelivery' ? 1 : 0)

/** Who presents a code, and when. */
export const HANDOVER_SHOWN_BY: Record<HandoverType, string> = {
  Pickup: 'Show this code to the courier when they collect the donation.',
  Delivery: 'Show this code to the courier when they deliver the donation.',
}

/** "#3F2A9C1D" style short reference for display. */
export const refOf = (id: string) => `#${id.slice(0, 8).toUpperCase()}`
