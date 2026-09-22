// Photography registry. Every photo on the site is referenced from here.
//
// A slot without a `src` renders a neutral line-art media panel (MediaArt) — an honest stand-in,
// never a fake photograph. heroRescue and impactSurplus ship real local photography.
//
// To ship a photo (this file only — no component changes):
//   1. Put the file in src/assets/photography/ (e.g. hero-rescue.jpg — AVIF/WebP/JPEG all fine).
//   2. `import heroRescueSrc from '../assets/photography/hero-rescue.jpg'` and set `src: heroRescueSrc`.
//   3. Set width/height to the file's intrinsic pixel size; adjust `focus` if the subject is off-centre.
// Frames crop with object-fit: cover, so any aspect ratio works; the briefs below list the ideal source.
// Local files only — no hotlinked or stock-service URLs.

import heroRescueSrc from '../assets/photography/hero-rescue.jpg'
import impactSurplusSrc from '../assets/photography/impact-surplus.jpg'
import type { MediaTone } from '../components/brand/MediaArt'

export type PhotoAsset = {
  /** Local asset URL. Undefined = not delivered yet (renders the MediaArt stand-in). */
  src?: string
  alt: string
  width: number
  height: number
  /** CSS object-position: keeps the subject inside every crop (arch, bleed, thumbnail). */
  focus: string
  /** Tone of the stand-in art while `src` is missing. */
  art: MediaTone
}

export const PHOTOS = {
  /**
   * HERO RESCUE — Home hero (arch crop) + marketplace-card thumbnail in the product glimpse.
   * Brief: close, premium food-rescue scene; crate or basket of fresh produce and bread; hands allowed,
   * faces not required; warm natural daylight; deep forest / sage accents in the environment; editorial,
   * not stock. Subject centred with generous air on all sides — the top is cut by a semicircular arch
   * and the lower-right corner sits under the rescue ticket. No logos, no text.
   * Source: 4:5 portrait, min 1600 × 2000 px.
   */
  heroRescue: {
    src: heroRescueSrc,
    alt: 'A crate of rescued bread and fresh produce in warm evening light, ready for pickup.',
    width: 1600,
    height: 2000,
    focus: 'center center',
    art: 'forest',
  },
  /**
   * IMPACT SURPLUS — "Surplus becomes impact" editorial split (bleeds to the left viewport edge on desktop).
   * Brief: artisan bread / produce / prepared food; tactile, documentary feeling; warm paper and wood
   * tones; not generic stock. Keep the subject in the central 60% — desktop crops to a tall frame,
   * tablet to 16:10, mobile to a square. No logos, no text.
   * Source: 3:2 (or 4:3) landscape, min 1800 px wide.
   */
  impactSurplus: {
    src: impactSurplusSrc,
    alt: 'Unsold loaves and produce gathered on a wooden table at the end of the day.',
    width: 1800,
    height: 1200,
    focus: '56% 50%',
    art: 'umber',
  },
} satisfies Record<string, PhotoAsset>
