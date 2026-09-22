import { motion, useReducedMotion } from 'framer-motion'
import { TIMELINE_PHASE } from '../../components/operations/presentation'
import type { TimelineEvent } from '../../lib/api/claims'
import { cn } from '../../lib/cn'
import { formatAbsolute, formatAgo } from '../../lib/expiry'
import { revealVariants } from '../../lib/motion'

/** The server timeline, oldest first, exactly as recorded. Events reveal one after another when the list enters view. */
export function EventTimeline({ events }: { events: TimelineEvent[] }) {
  const reduced = useReducedMotion()
  return (
    <motion.ol
      role="list"
      className="timeline"
      initial={reduced ? false : 'hidden'}
      whileInView="visible"
      viewport={{ once: true, amount: 0.2 }}
      variants={{ hidden: {}, visible: { transition: { staggerChildren: 0.12, delayChildren: 0.1 } } }}
    >
      {events.map((e, i) => {
        const [day, time] = formatAbsolute(e.atUtc).split(' · ')
        const latest = i === events.length - 1
        return (
          <motion.li
            key={`${e.kind}-${e.atUtc}-${i}`}
            className={cn('timeline__event', `timeline__event--${TIMELINE_PHASE[e.kind] ?? 'active'}`, latest && 'is-latest')}
            variants={revealVariants}
          >
            <time className="timeline__when" dateTime={e.atUtc}>
              <span className="timeline__time t-data">{time}</span>
              <span className="timeline__day">{day}</span>
            </time>
            <span className="timeline__node" aria-hidden="true" />
            <div className="timeline__body">
              <p className="timeline__type t-label">
                {e.label}
                {latest && <span className="timeline__latest">Latest</span>}
              </p>
              <p className="timeline__ago">{formatAgo(e.atUtc)}</p>
            </div>
          </motion.li>
        )
      })}
    </motion.ol>
  )
}
