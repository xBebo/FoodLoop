// Home page copy. Presentational only — no metrics, no backend contract.

export type LoopStepId = 'donor' | 'foodloop' | 'courier' | 'community'

export const LOOP_STEPS: { id: LoopStepId; role: string; title: string; body: string }[] = [
  {
    id: 'donor',
    role: 'Donor',
    title: 'Surplus is listed',
    body: 'Restaurants, grocers and producers share what they can’t sell, with pickup windows and handling notes.',
  },
  {
    id: 'foodloop',
    role: 'FoodLoop',
    title: 'The right match',
    body: 'FoodLoop offers each listing to nearby community organizations and keeps every party in sync.',
  },
  {
    id: 'courier',
    role: 'Courier',
    title: 'Moved with care',
    body: 'A courier collects the food and carries it safely, with every handoff confirmed along the way.',
  },
  {
    id: 'community',
    role: 'Community',
    title: 'Food reaches people',
    body: 'Pantries, shelters and kitchens receive food their people can use — and the loop begins again.',
  },
]

export type RoleId = 'donor' | 'beneficiary' | 'courier' | 'admin'

export const ROLES: { id: RoleId; name: string; purpose: string; capabilities: string[] }[] = [
  {
    id: 'donor',
    name: 'Donor',
    purpose: 'Share surplus before it goes to waste.',
    capabilities: [
      'List surplus with photos, quantities and a pickup window.',
      'See who claimed it and when the courier is on the way.',
      'Keep a clear record of everything you have shared.',
    ],
  },
  {
    id: 'beneficiary',
    name: 'Beneficiary',
    purpose: 'Discover food available to your organization.',
    capabilities: [
      'Browse nearby listings that fit what you can store and serve.',
      'Claim food in a few steps and choose pickup or delivery.',
      'Tell donors what your community needs most.',
    ],
  },
  {
    id: 'courier',
    name: 'Courier',
    purpose: 'Move each rescue safely from pickup to delivery.',
    capabilities: [
      'Accept runs that fit your route and your schedule.',
      'Follow clear pickup and drop-off instructions.',
      'Confirm every handoff with a one-time code.',
    ],
  },
  {
    id: 'admin',
    name: 'Admin',
    purpose: 'Coordinate organizations and operations.',
    capabilities: [
      'Review and approve organizations joining the network.',
      'Oversee listings, claims and deliveries in one place.',
      'Step in when something needs a human decision.',
    ],
  },
]

// Sample presentation data for the product glimpse. Clearly labelled as a preview in the UI.
export const GLIMPSE = {
  listing: {
    title: 'Sourdough & morning pastries',
    donor: 'Neighbourhood bakery',
    window: 'Pickup today · 17:00 – 19:00',
    tags: ['Bakery', 'Ambient'],
  },
  claim: {
    org: 'Eastside Community Pantry',
    steps: [
      { label: 'Listed', state: 'done' },
      { label: 'Claimed', state: 'done' },
      { label: 'Courier assigned', state: 'current' },
      { label: 'Delivered', state: 'todo' },
    ],
  },
  ticket: {
    pickup: 'Neighbourhood bakery',
    dropoff: 'Eastside Community Pantry',
  },
} as const
