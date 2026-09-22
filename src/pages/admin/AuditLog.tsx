import { AlertCircle, Search } from 'lucide-react'
import { PageMessage } from '../../components/workspace/PageState'
import { AUDIT_ACTIONS, getAudit } from '../../lib/api/admin'
import { codeOf } from '../../lib/api/client'
import { useLoad } from '../../lib/api/useLoad'
import { formatAgo } from '../../lib/expiry'
import { useMediaQuery } from '../../lib/useMediaQuery'
import { AdminIntro, Pagination } from './kit'
import { formatUtc, useQueryParams } from './presentation'
import './audit.css'

/** Backend page size for GET /api/admin/audit. */
const PAGE_SIZE = 20

/** yyyy-mm-dd (a UTC day) → the ISO instant that starts it; `next` gives the following midnight (toUtc is exclusive). */
const utcDayStart = (day: string, next = false) => {
  const d = new Date(`${day}T00:00:00Z`)
  if (next) d.setUTCDate(d.getUTCDate() + 1)
  return d.toISOString()
}

export function AuditLog() {
  const desktop = useMediaQuery('(min-width: 900px)')
  const [params, update] = useQueryParams()

  const action = params.get('action') ?? ''
  const actor = params.get('actor') ?? ''
  const from = params.get('from') ?? ''
  const to = params.get('to') ?? ''
  const requested = Math.max(1, Number(params.get('page')) || 1)
  const rangeInvalid = !!from && !!to && from > to

  const query = {
    page: requested,
    action: action || undefined,
    actor: actor || undefined,
    // An inverted range is not sent: the notice below explains it, and the list stays unfiltered by date.
    fromUtc: from && !rangeInvalid ? utcDayStart(from) : undefined,
    toUtc: to && !rangeInvalid ? utcDayStart(to, true) : undefined,
  }
  const load = useLoad(JSON.stringify(query), (signal) => getAudit(query, signal))
  const anyFilter = !!(action || actor || from || to)

  if (load.error !== undefined && !load.data && codeOf(load.error) !== 'audit.invalid_range')
    return <PageMessage title="We couldn’t load the audit log." onRetry={load.reload} />

  const result = load.data
  const items = result?.items ?? []
  const page = result?.page ?? requested
  const total = result?.totalCount ?? 0
  const pages = Math.max(1, Math.ceil(total / PAGE_SIZE))
  const startIdx = (page - 1) * PAGE_SIZE + 1

  return (
    <div className="container ws-page adm">
      <AdminIntro
        code="ADM-05"
        title={
          <>
            Audit <em>log</em>
          </>
        }
        lead="Every recorded action on the network, newest first. Filter by action, actor or date range."
        meta={[result ? `${total} entries` : 'Loading…', 'UTC timestamps', 'Details payload not shown']}
      />

      <div className="adm-toolbar audit-filters">
        <div className="adm-field">
          <label htmlFor="audit-action" className="visually-hidden">
            Filter by action
          </label>
          <select id="audit-action" className="adm-select" value={action} onChange={(e) => update({ action: e.target.value || null, page: null })}>
            <option value="">All actions</option>
            {AUDIT_ACTIONS.map((a) => (
              <option key={a} value={a}>
                {a}
              </option>
            ))}
          </select>
        </div>

        <form
          key={actor}
          role="search"
          className="adm-search adm-field"
          onSubmit={(e) => {
            e.preventDefault()
            const value = new FormData(e.currentTarget).get('actor')
            update({ actor: typeof value === 'string' && value.trim() ? value.trim() : null, page: null })
          }}
        >
          <label htmlFor="audit-actor" className="visually-hidden">
            Filter by actor name
          </label>
          <Search aria-hidden="true" />
          <input
            id="audit-actor"
            name="actor"
            type="search"
            className="adm-input"
            placeholder="Actor name, then Enter"
            autoComplete="off"
            defaultValue={actor}
          />
        </form>

        <div className="adm-field audit-daterange">
          <label htmlFor="audit-from" className="visually-hidden">
            From date (UTC)
          </label>
          <input
            id="audit-from"
            type="date"
            className="adm-input"
            aria-invalid={rangeInvalid || undefined}
            value={from}
            onChange={(e) => update({ from: e.target.value || null, page: null })}
          />
          <span className="audit-daterange__sep" aria-hidden="true">
            –
          </span>
          <label htmlFor="audit-to" className="visually-hidden">
            To date (UTC)
          </label>
          <input
            id="audit-to"
            type="date"
            className="adm-input"
            aria-invalid={rangeInvalid || undefined}
            value={to}
            onChange={(e) => update({ to: e.target.value || null, page: null })}
          />
        </div>

        {anyFilter && (
          <button type="button" className="audit-clear" onClick={() => update({ action: null, actor: null, from: null, to: null, page: null })}>
            Clear filters
          </button>
        )}
      </div>

      <div role="alert" className="adm-notice">
        {rangeInvalid && (
          <p className="ws-notice ws-notice--warning">
            <AlertCircle aria-hidden="true" />
            The “from” date is after the “to” date, so no date filter is applied. Adjust the range to filter by date.
          </p>
        )}
      </div>

      <section aria-labelledby="audit-results" id="audit-region" aria-busy={load.loading}>
        <div className="adm-resultline">
          <h2 id="audit-results" className="adm-resultline__title">
            {anyFilter ? 'Filtered entries' : 'All entries'}
          </h2>
          <p className="adm-resultline__count" role="status">
            {!result ? 'Loading…' : total ? `Showing ${startIdx}–${startIdx + items.length - 1} of ${total}` : 'No matching entries'}
          </p>
        </div>

        {result && items.length === 0 ? (
          <div className="ws-empty adm-empty">
            <Search aria-hidden="true" />
            <h3>No entries match.</h3>
            <p>Try a different action, actor or date range.</p>
          </div>
        ) : desktop ? (
          <div className="ledger-frame">
            <table className="ledger audit-ledger">
              <caption className="visually-hidden">
                Audit log, page {page} of {pages}
              </caption>
              <thead>
                <tr>
                  <th scope="col">Timestamp (UTC)</th>
                  <th scope="col">Action</th>
                  <th scope="col">Actor</th>
                  <th scope="col">Entity</th>
                </tr>
              </thead>
              <tbody>
                {items.map((e) => (
                  <tr key={e.id}>
                    <th scope="row" className="ledger__rowhead">
                      <time dateTime={e.timestampUtc} className="ledger__mono">
                        {formatUtc(e.timestampUtc)}
                      </time>
                    </th>
                    <td>
                      <code className="audit-action">{e.action}</code>
                    </td>
                    <td>
                      <span className="ledger__primary">{e.actorName}</span>
                    </td>
                    <td>
                      {e.entityType}
                      <code className="ledger__mono ledger__sub--plain">{e.entityId}</code>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : (
          <ul role="list" className="records">
            {items.map((e) => (
              <li key={e.id} className="record">
                <div className="record__head">
                  <code className="audit-action">{e.action}</code>
                  <time dateTime={e.timestampUtc} className="record__time">
                    {formatAgo(e.timestampUtc)}
                  </time>
                </div>
                <dl className="record__facts">
                  <div>
                    <dt>Actor</dt>
                    <dd>{e.actorName}</dd>
                  </div>
                  <div>
                    <dt>Entity</dt>
                    <dd>{e.entityType}</dd>
                  </div>
                  <div className="span-2">
                    <dt>Entity ID</dt>
                    <dd>
                      <code>{e.entityId}</code>
                    </dd>
                  </div>
                  <div className="span-2">
                    <dt>Timestamp (UTC)</dt>
                    <dd>
                      <code>{formatUtc(e.timestampUtc)}</code>
                    </dd>
                  </div>
                </dl>
              </li>
            ))}
          </ul>
        )}

        <Pagination page={page} pages={pages} label="Audit entries" targetId="audit-region" onPage={(n) => update({ page: String(n) })} />
      </section>
    </div>
  )
}
