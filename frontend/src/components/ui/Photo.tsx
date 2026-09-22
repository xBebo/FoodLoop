import type { ImgHTMLAttributes } from 'react'
import type { PhotoAsset } from '../../data/photography'
import { cn } from '../../lib/cn'
import { MediaArt } from '../brand/MediaArt'

type PhotoProps = Omit<ImgHTMLAttributes<HTMLImageElement>, 'src' | 'alt' | 'width' | 'height'> & {
  photo: PhotoAsset
  /** Above-the-fold: eager + high fetch priority. Everything else lazy-loads. */
  priority?: boolean
  /** Decorative reuse of a photo already described elsewhere. */
  decorative?: boolean
}

/**
 * <img> with intrinsic dimensions (no layout shift) and loading hints; crop via the registry's `focus`.
 * Slots without a delivered file render the MediaArt stand-in in the same box.
 */
export function Photo({ photo, priority = false, decorative = false, className, style, ...rest }: PhotoProps) {
  if (!photo.src) return <MediaArt tone={photo.art} className={className} />
  return (
    <img
      src={photo.src}
      alt={decorative ? '' : photo.alt}
      width={photo.width}
      height={photo.height}
      loading={priority ? 'eager' : 'lazy'}
      fetchPriority={priority ? 'high' : 'auto'}
      decoding="async"
      className={cn('photo', className)}
      style={{ objectPosition: photo.focus, ...style }}
      {...rest}
    />
  )
}
