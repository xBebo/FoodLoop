// Category media slots for food listings. Same contract as photography.ts:
// a slot without `src` renders the art-directed MediaArt stand-in in that category's tone.
//
// To ship category photography (this file only — no card changes):
//   import produceSrc from '../assets/photography/category-produce.jpg' → set `src`, width, height, focus.
// Brief for every slot: close, tactile, editorial food photography in warm daylight; no text, no logos,
// no faces; subject in the central 60% (cards crop to 4:3, detail hero to 4:5, list thumbnails to 1:1).
// Source: 3:2 landscape, min 1600 px wide. Local files only.
import type { PhotoAsset } from './photography'

export type CategoryMediaSlot = 'produce' | 'bakery' | 'preparedMeals' | 'dairy' | 'pantry' | 'mixed'

export const CATEGORY_MEDIA: Record<CategoryMediaSlot, PhotoAsset> = {
  produce: { alt: 'Fresh vegetables in a crate.', width: 1600, height: 1067, focus: 'center', art: 'produce' },
  bakery: { alt: 'Freshly baked loaves.', width: 1600, height: 1067, focus: 'center', art: 'bakery' },
  preparedMeals: { alt: 'Trays of prepared meals.', width: 1600, height: 1067, focus: 'center', art: 'prepared' },
  dairy: { alt: 'Bottles of milk and tubs of yogurt.', width: 1600, height: 1067, focus: 'center', art: 'dairy' },
  pantry: { alt: 'Sealed pantry staples on a shelf.', width: 1600, height: 1067, focus: 'center', art: 'pantry' },
  mixed: { alt: 'A mixed box of groceries.', width: 1600, height: 1067, focus: 'center', art: 'mixed' },
}
