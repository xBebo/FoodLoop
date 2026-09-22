// Expiry PRESENTATION only: turns an ISO timestamp into readable copy. No food-safety logic.

export type ExpiryUrgency = 'critical' | 'soon' | 'later' | 'past'

export type ExpiryView = {
  /** Short relative phrase: "Closes in 2h", "Today, 6:30 PM", "Tomorrow, 9:00 AM", "Fri, Sep 25". */
  relative: string
  /** Always-readable absolute date and time: "Tue, Sep 22 · 6:30 PM". */
  absolute: string
  urgency: ExpiryUrgency
  /** Visible word that carries urgency without colour. */
  urgencyLabel: string
}

const HOUR = 3_600_000
const time = new Intl.DateTimeFormat('en-US', { hour: 'numeric', minute: '2-digit' })
const day = new Intl.DateTimeFormat('en-US', { weekday: 'short', month: 'short', day: 'numeric' })

const dayKey = (d: Date) => `${d.getFullYear()}-${d.getMonth()}-${d.getDate()}`

export function formatAbsolute(iso: string) {
  const d = new Date(iso)
  return `${day.format(d)} · ${time.format(d)}`
}

export function describeExpiry(iso: string, now = new Date()): ExpiryView {
  const at = new Date(iso)
  const ms = at.getTime() - now.getTime()
  const absolute = formatAbsolute(iso)

  if (ms <= 0) return { relative: 'Closed', absolute, urgency: 'past', urgencyLabel: 'Window closed' }

  if (ms < 3 * HOUR) {
    const mins = Math.max(1, Math.round(ms / 60_000))
    const relative = mins < 60 ? `Closes in ${mins} min` : `Closes in ${Math.floor(mins / 60)}h${mins % 60 ? ` ${mins % 60}m` : ''}`
    return { relative, absolute, urgency: 'critical', urgencyLabel: 'Closing soon' }
  }

  const tomorrow = new Date(now)
  tomorrow.setDate(now.getDate() + 1)
  const urgency = ms < 24 * HOUR ? 'soon' : 'later'
  const urgencyLabel = urgency === 'soon' ? 'Within a day' : 'Plenty of time'

  if (dayKey(at) === dayKey(now)) return { relative: `Today, ${time.format(at)}`, absolute, urgency, urgencyLabel }
  if (dayKey(at) === dayKey(tomorrow)) return { relative: `Tomorrow, ${time.format(at)}`, absolute, urgency, urgencyLabel }
  return { relative: day.format(at), absolute, urgency, urgencyLabel }
}

/** "Closes in 2h" / "Closes today, 6:30 PM" / "Closes Fri, Sep 25" / "Closed". */
export function expiryPhrase({ urgency, relative }: ExpiryView) {
  if (urgency === 'critical' || urgency === 'past') return relative
  return `Closes ${relative.replace(/^(Today|Tomorrow)/, (m) => m.toLowerCase())}`
}

/** "3h ago", "2 days ago" — for updated/created stamps. */
export function formatAgo(iso: string, now = new Date()) {
  const mins = Math.round((now.getTime() - new Date(iso).getTime()) / 60_000)
  if (mins < 1) return 'Just now'
  if (mins < 60) return `${mins} min ago`
  const hours = Math.round(mins / 60)
  if (hours < 24) return `${hours}h ago`
  const days = Math.round(hours / 24)
  return days === 1 ? 'Yesterday' : `${days} days ago`
}

/** ISO → value for <input type="datetime-local"> (local time, minute precision). */
export function toLocalInput(iso: string) {
  const d = new Date(iso)
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`
}
