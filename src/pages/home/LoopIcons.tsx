import { FoodLoopMark } from '../../components/brand/FoodLoopMark'
import type { LoopStepId } from './content'

// Original line illustrations for the loop. 48×48, stroke = currentColor, decorative.
const common = {
  viewBox: '0 0 48 48',
  fill: 'none',
  stroke: 'currentColor',
  strokeWidth: 1.6,
  strokeLinecap: 'round',
  strokeLinejoin: 'round',
  'aria-hidden': true,
} as const

function DonorIcon() {
  // Crate with a loaf and a sprouting leaf
  return (
    <svg {...common}>
      <path d="M8 24h32l-3 16H11z" />
      <path d="M10.5 31h27" />
      <path d="M15 24c0-5 4-8 9-8s9 3 9 8" />
      <path d="M19 19.5l1.5 2.5M24 18.5v3M29 19.5l-1.5 2.5" />
      <path d="M31 12c2-4 7-5 9-3-1 4-6 5-9 3zM31 12c-1 1-2 3-2 5" />
    </svg>
  )
}

function CourierIcon() {
  // Bicycle with a delivery box
  return (
    <svg {...common}>
      <circle cx="13" cy="33" r="6.5" />
      <circle cx="35" cy="33" r="6.5" />
      <path d="M13 33l7-11h10l5 11M20 22l-3-5h-3M30 22l-4 11h-6" />
      <rect x="27" y="11" width="10" height="8" rx="1.5" />
      <path d="M32 11v8" />
    </svg>
  )
}

function CommunityIcon() {
  // Home with a leaf-heart
  return (
    <svg {...common}>
      <path d="M8 22L24 9l16 13" />
      <path d="M12 19v20h24V19" />
      <path d="M24 34c-5-3-7-6-7-8.5a3.5 3.5 0 0 1 7-1 3.5 3.5 0 0 1 7 1c0 2.5-2 5.5-7 8.5z" />
    </svg>
  )
}

export function LoopIcon({ id }: { id: LoopStepId }) {
  if (id === 'donor') return <DonorIcon />
  if (id === 'courier') return <CourierIcon />
  if (id === 'community') return <CommunityIcon />
  return <FoodLoopMark />
}
