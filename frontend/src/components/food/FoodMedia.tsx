import { CATEGORY_MEDIA } from '../../data/categoryMedia'
import { cn } from '../../lib/cn'
import { MediaArt } from '../brand/MediaArt'
import { Photo } from '../ui/Photo'
import type { CategoryVisual } from './presentation'

type FoodMediaProps = { visual: Pick<CategoryVisual, 'slot' | 'icon'>; imageUrl?: string; className?: string; priority?: boolean }

/**
 * Listing visual, in priority order: the listing's own photo → the category's photo slot →
 * the category's MediaArt treatment. Always decorative: the title beside it carries the meaning.
 */
export function FoodMedia({ visual, imageUrl, className, priority }: FoodMediaProps) {
  const slot = CATEGORY_MEDIA[visual.slot]
  const Icon = visual.icon

  if (imageUrl) {
    return (
      <img
        src={imageUrl}
        alt=""
        loading={priority ? 'eager' : 'lazy'}
        decoding="async"
        className={cn('photo', className)}
      />
    )
  }
  if (slot.src) return <Photo photo={slot} decorative priority={priority} className={className} />
  return <MediaArt tone={slot.art} glyph={<Icon aria-hidden="true" />} className={className} />
}
