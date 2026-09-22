// Mock content for the R1 style playground only. Not a backend contract.
import type { StatusTone } from '../components/ui/StatusChip'

export type Swatch = { name: string; token: string; hex: string; role: string; dark?: boolean }

export const CORE_SWATCHES: Swatch[] = [
  { name: 'Forest 950', token: '--forest-950', hex: '#051F20', role: 'Night canopy — dramatic panels, footer', dark: true },
  { name: 'Forest 900', token: '--forest-900', hex: '#0B2B26', role: 'Primary actions', dark: true },
  { name: 'Forest 800', token: '--forest-800', hex: '#163832', role: 'Depth, completed states', dark: true },
  { name: 'Forest 700', token: '--forest-700', hex: '#235347', role: 'Accent, focus, links', dark: true },
  { name: 'Sage 500', token: '--sage-500', hex: '#8EB69B', role: 'Line art, accents on dark' },
  { name: 'Mint 100', token: '--mint-100', hex: '#DAF1DE', role: 'Soft fills, success' },
]

export const NEUTRAL_SWATCHES: Swatch[] = [
  { name: 'Paper', token: '--paper', hex: '#F3EFE4', role: 'Canvas' },
  { name: 'Warm white', token: '--warm-white', hex: '#FBF9F3', role: 'Raised surfaces' },
  { name: 'Stone', token: '--stone-200', hex: '#E2DCCD', role: 'Quiet fills' },
  { name: 'Muted ink', token: '--ink-500', hex: '#56635E', role: 'Secondary text', dark: true },
  { name: 'Ink', token: '--ink-900', hex: '#122420', role: 'Body text', dark: true },
]

export const TYPE_SCALE = [
  { label: 'Hero', token: '--text-hero', range: '50 → 140px', sample: 'Rescue' },
  { label: 'H1', token: '--text-h1', range: '40 → 88px', sample: 'Rerouted' },
  { label: 'H2', token: '--text-h2', range: '32 → 60px', sample: 'Within hours' },
  { label: 'H3', token: '--text-h3', range: '24 → 36px', sample: 'Pickup windows' },
  { label: 'H4', token: '--text-h4', range: '19 → 23px', sample: 'Crumb & Co. Bakery' },
] as const

export const STATUS_SAMPLES: { tone: StatusTone; label: string }[] = [
  { tone: 'neutral', label: 'Draft' },
  { tone: 'warning', label: 'Pickup soon' },
  { tone: 'info', label: 'In transit' },
  { tone: 'success', label: 'Claimed' },
  { tone: 'complete', label: 'Delivered' },
  { tone: 'danger', label: 'Expired' },
]

export const RESCUE_LOG: { item: string; donor: string; time: string; tone: StatusTone; status: string }[] = [
  { item: '48 sourdough loaves', donor: 'Crumb & Co. Bakery', time: '18:30', tone: 'warning', status: 'Pickup soon' },
  { item: '22 kg seasonal veg', donor: 'Northside Market', time: '17:05', tone: 'info', status: 'In transit' },
  { item: '60 prepared meals', donor: 'Canteen Seven', time: '15:40', tone: 'complete', status: 'Delivered' },
]

export const MOTION_TOKENS = [
  { name: 'Fast', value: '150ms', use: 'Press, colour, hover tint' },
  { name: 'UI', value: '260ms', use: 'Menus, indicators, lifts' },
  { name: 'Reveal', value: '450ms', use: 'Sections entering view' },
  { name: 'Ambient', value: '18s', use: 'Orb drift — decorative only' },
]
