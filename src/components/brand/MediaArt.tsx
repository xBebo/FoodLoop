import type { ReactNode } from 'react'
import { cn } from '../../lib/cn'
import { BotanicalBranch } from './Botanical'
import './brand.css'

// Topographic field: perturbed rings around a low focal point. Computed once at module load.
const RINGS = Array.from({ length: 10 }, (_, i) => {
  const R = 60 + i * 42
  const pts: string[] = []
  for (let k = 0; k <= 120; k++) {
    const th = (k / 120) * Math.PI * 2
    const r = R * (1 + 0.05 * Math.sin(3 * th + i * 0.8) + 0.025 * Math.sin(7 * th - i * 1.1))
    pts.push(`${(200 + r * Math.cos(th)).toFixed(1)} ${(330 + r * 0.82 * Math.sin(th)).toFixed(1)}`)
  }
  return `M${pts.join('L')}Z`
})

/** Brand tones plus one per food category (so listings never all look the same). */
export type MediaTone = 'forest' | 'umber' | 'produce' | 'bakery' | 'prepared' | 'dairy' | 'pantry' | 'mixed'

/**
 * Neutral stand-in for photography that has not been delivered yet: light, contour field and one
 * botanical sprig (or a category glyph) in the brand's line language. Deliberately graphic — it never
 * imitates a photograph. Decorative (aria-hidden); fills whatever frame it is placed in.
 */
export function MediaArt({ tone = 'forest', glyph, className }: { tone?: MediaTone; glyph?: ReactNode; className?: string }) {
  return (
    <div className={cn('media-art', `media-art--${tone}`, className)} aria-hidden="true" data-photo-pending="">
      <svg viewBox="0 0 400 500" preserveAspectRatio="xMidYMid slice" className="media-art__field">
        {RINGS.map((d, i) => (
          <path key={i} d={d} strokeOpacity={0.5 - i * 0.04} />
        ))}
      </svg>
      {glyph ? <div className="media-art__glyph">{glyph}</div> : <BotanicalBranch className="media-art__sprig" draw={false} />}
    </div>
  )
}
