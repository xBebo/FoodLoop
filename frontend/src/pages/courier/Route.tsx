import { motion, useReducedMotion } from 'framer-motion'
import { MapPin, Truck } from 'lucide-react'
import { routeLegOf } from '../../components/operations/presentation'
import { cn } from '../../lib/cn'
import { ease } from '../../lib/motion'
import type { CourierTask } from '../../lib/api/courier'
import './route.css'

type Props = {
  task: Pick<CourierTask, 'nextStep' | 'donorName' | 'pickupAddress' | 'beneficiaryName'>
  size?: 'ticket' | 'large'
}

/**
 * Donor → beneficiary, with the pickup point on the donor stop. No map, distance or ETA —
 * only the two organizations, the pickup address, and where the courier stands (from the server's nextStep).
 */
export function Route({ task, size = 'ticket' }: Props) {
  const reduced = useReducedMotion()
  const leg = routeLegOf(task.nextStep)
  const donorState = leg === 0 ? 'Next stop' : 'Collected'
  const beneficiaryState = leg === 2 ? 'Delivered' : leg === 1 ? 'Next stop' : 'After pickup'

  return (
    // The wrapper watches the viewport: a fully clipped element never counts as intersecting.
    <motion.div
      className={cn('route', `route--${size}`)}
      initial={reduced ? false : 'hidden'}
      whileInView="visible"
      viewport={{ once: true, amount: 0.25 }}
    >
      {/* The route draws in top → bottom once; solid segments are legs already covered. */}
      <motion.ol
        role="list"
        className="route__stops"
        aria-label="Route"
        variants={{ hidden: { clipPath: 'inset(0 0 100% 0)' }, visible: { clipPath: 'inset(0 0 0% 0)' } }}
        transition={{ duration: 0.9, ease: ease.inOut, delay: 0.15 }}
      >
        <li className={cn('route__stop', leg === 0 && 'is-next', leg > 0 && 'is-done')}>
          <span className="route__pin" aria-hidden="true" />
          <span className="route__role t-label">Donor · pickup point</span>
          <span className="route__name">{task.donorName}</span>
          <span className="route__addr">
            <MapPin aria-hidden="true" />
            {task.pickupAddress}
          </span>
          <span className="route__state">{donorState}</span>
        </li>
        {leg === 1 && (
          <li className="route__transit">
            <span className="route__truck" aria-hidden="true">
              <Truck />
            </span>
            <span>On the road</span>
          </li>
        )}
        <li className={cn('route__stop route__stop--end', leg === 1 && 'is-next', leg === 2 && 'is-done')}>
          <span className="route__pin" aria-hidden="true" />
          <span className="route__role t-label">Beneficiary · drop-off</span>
          <span className="route__name">{task.beneficiaryName}</span>
          <span className="route__state">{beneficiaryState}</span>
        </li>
      </motion.ol>
    </motion.div>
  )
}
