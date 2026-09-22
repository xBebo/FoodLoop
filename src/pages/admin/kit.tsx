// Shared admin page parts: intro + console rule, pager. Kept small — every admin chunk imports it.
import { ChevronLeft, ChevronRight } from 'lucide-react'
import type { ReactNode } from 'react'
import { SectionEyebrow } from '../../components/ui/SectionEyebrow'
import { cn } from '../../lib/cn'
import { formatUtcTime } from './presentation'
import './admin.css'

type IntroProps = {
  /** Console section code, e.g. "ADM-02". */
  code: string
  title: ReactNode
  lead: string
  /** Readout beside the title (desktop) / below it (mobile). */
  aside?: ReactNode
  /** Metadata along the console rule. */
  meta: ReactNode[]
  className?: string
}

/** Page header: eyebrow, H1, lead, optional readout, then a ruled strip of monospaced metadata. */
export function AdminIntro({ code, title, lead, aside, meta, className }: IntroProps) {
  const snapshot = new Date().toISOString()
  return (
    <header className={cn('adm-intro', className)}>
      <div className="adm-intro__grid">
        <div className="adm-intro__text">
          <SectionEyebrow>Admin workspace</SectionEyebrow>
          <h1 className="adm-intro__title">{title}</h1>
          <p className="t-lead adm-intro__lead">{lead}</p>
        </div>
        {aside}
      </div>
      <ul role="list" className="adm-console">
        <li className="adm-console__code">{code}</li>
        {meta.map((m, i) => (
          <li key={i}>{m}</li>
        ))}
        <li className="adm-console__end">
          Snapshot <time dateTime={snapshot}>{formatUtcTime(snapshot)} UTC</time> · live
        </li>
      </ul>
    </header>
  )
}

type PagerProps = {
  page: number
  pages: number
  /** What is being paged, e.g. "Organizations". */
  label: string
  onPage: (page: number) => void
  /** Region to bring back into view after paging from below it. */
  targetId: string
}

export function Pagination({ page, pages, label, onPage, targetId }: PagerProps) {
  if (pages <= 1) return null
  function go(n: number) {
    onPage(n)
    const target = document.getElementById(targetId)
    if (target && target.getBoundingClientRect().top < 0) target.scrollIntoView({ block: 'start' })
  }
  return (
    <nav className="adm-pager" aria-label={`${label} pages`}>
      <button type="button" className="adm-pager__step" onClick={() => go(page - 1)} disabled={page === 1}>
        <ChevronLeft aria-hidden="true" />
        <span>Previous</span>
      </button>
      <ol role="list" className="adm-pager__list">
        {/* First, last and two either side of the current page, so long real lists never overflow. */}
        {Array.from({ length: pages }, (_, i) => i + 1)
          .filter((n) => n === 1 || n === pages || Math.abs(n - page) <= 2)
          .map((n) => (
          <li key={n}>
            <button
              type="button"
              className="adm-pager__page t-data"
              aria-label={`Page ${n} of ${pages}`}
              aria-current={n === page ? 'page' : undefined}
              onClick={() => go(n)}
            >
              {String(n).padStart(2, '0')}
            </button>
          </li>
        ))}
      </ol>
      <button type="button" className="adm-pager__step" onClick={() => go(page + 1)} disabled={page === pages}>
        <span>Next</span>
        <ChevronRight aria-hidden="true" />
      </button>
    </nav>
  )
}
