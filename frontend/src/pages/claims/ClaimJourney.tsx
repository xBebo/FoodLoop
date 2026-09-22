import { motion, useReducedMotion } from 'framer-motion'
import { Check, X } from 'lucide-react'
import type { CSSProperties } from 'react'
import { JOURNEY_STAGES, journeyOf } from '../../components/operations/presentation'
import type { TimelineEvent } from '../../lib/api/claims'
import { cn } from '../../lib/cn'
import { formatAbsolute, formatAgo } from '../../lib/expiry'
import { revealVariants, staggerVariants } from '../../lib/motion'

const STATE_TEXT = { reached: 'Reached', current: 'Current stage', future: 'Not reached' } as const

/**
 * Lifecycle journey drawn only from the server timeline. Future stages are quiet placeholders in the track
 * (never events); a cancelled claim ends the track with a terminal marker.
 */
export function ClaimJourney({ timeline, className }: { timeline: TimelineEvent[]; className?: string }) {
  const reduced = useReducedMotion()
  const { stages, lastReached, terminal } = journeyOf(timeline)
  const current = stages[Math.max(lastReached, 0)]
  const headline = terminal ?? current.event
  const headLabel = terminal ? terminal.label : current.label

  return (
    <div className={cn('journey', terminal && 'journey--ended', className)}>
      <div className="journey__now">
        <p className="journey__kicker t-label">{terminal ? 'Journey ended' : 'Current stage'}</p>
        <p className="journey__headline">{headLabel}</p>
        {headline && (
          <p className="journey__since">
            <time dateTime={headline.atUtc}>{formatAgo(headline.atUtc)}</time>
            {!terminal && ` · stage ${lastReached + 1} of ${JOURNEY_STAGES.length}`}
          </p>
        )}
      </div>

      <motion.ol
        role="list"
        className="journey__track"
        style={{ '--stops': stages.length + (terminal ? 1 : 0) } as CSSProperties}
        variants={staggerVariants}
        initial={reduced ? false : 'hidden'}
        whileInView="visible"
        viewport={{ once: true, amount: 0.3 }}
      >
        {stages.flatMap((s, i) => {
          const items = [<Stop key={s.id} label={s.label} state={terminal && s.state === 'future' ? 'void' : s.state} event={s.event} />]
          if (terminal && i === lastReached) items.push(<Stop key="terminal" label={terminal.label} state="terminal" event={terminal} />)
          return items
        })}
      </motion.ol>
    </div>
  )
}

type StopState = 'reached' | 'current' | 'future' | 'void' | 'terminal'

function Stop({ label, state, event }: { label: string; state: StopState; event?: TimelineEvent }) {
  const [day, time] = event ? formatAbsolute(event.atUtc).split(' · ') : []
  const stateText = state === 'void' ? 'Not reached' : state === 'terminal' ? 'Journey ended here' : STATE_TEXT[state]
  return (
    <motion.li className={cn('journey__stop', `is-${state}`)} aria-current={state === 'current' ? 'step' : undefined} variants={revealVariants}>
      <span className="journey__node" aria-hidden="true">
        {state === 'terminal' ? <X /> : (state === 'reached' || state === 'current') && <Check />}
      </span>
      <span className="journey__label">{label}</span>
      <span className="journey__state">{stateText}</span>
      {event && (
        <time className="journey__time" dateTime={event.atUtc}>
          {day}
          <br />
          {time}
        </time>
      )}
    </motion.li>
  )
}
