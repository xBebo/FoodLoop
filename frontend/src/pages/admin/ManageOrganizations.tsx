import { motion } from 'framer-motion'
import { AlertCircle, CheckCircle2, X } from 'lucide-react'
import { PATHS } from '../../app/routes'
import { Button } from '../../components/ui/Button'
import { StatusChip } from '../../components/ui/StatusChip'
import { PageMessage } from '../../components/workspace/PageState'
import { changeOrganization, getOrganizations, type AdminOrganization } from '../../lib/api/admin'
import { useLoad } from '../../lib/api/useLoad'
import { cn } from '../../lib/cn'
import { spring } from '../../lib/motion'
import { useMediaQuery } from '../../lib/useMediaQuery'
import { ORGANIZATION_STATUSES, ORGANIZATION_TYPE_LABELS, type OrganizationStatus } from '../../types/organization'
import { AdminIntro, Pagination } from './kit'
import { ORG_ACTION, ORG_STATUS_TONE, formatDate, useQueryParams } from './presentation'
import { useAdminAction } from './useAdminAction'

/** Backend page size for GET /api/admin/organizations. */
const PAGE_SIZE = 20
const isStatus = (v: string | null): v is OrganizationStatus => ORGANIZATION_STATUSES.includes(v as OrganizationStatus)

const loadCounts = (signal: AbortSignal) =>
  Promise.all(ORGANIZATION_STATUSES.map((s) => getOrganizations(s, 1, signal).then((r) => [s, r.totalCount] as const))).then(
    (rows) => Object.fromEntries(rows) as Record<OrganizationStatus, number>,
  )

export function ManageOrganizations() {
  const desktop = useMediaQuery('(min-width: 1024px)')
  const [params, update] = useQueryParams()
  const rawStatus = params.get('status')
  const status = isStatus(rawStatus) ? rawStatus : undefined
  const requested = Math.max(1, Number(params.get('page')) || 1)
  const list = useLoad(`${status ?? 'all'}:${requested}`, (signal) => getOrganizations(status, requested, signal))
  const counts = useLoad('counts', loadCounts)
  const action = useAdminAction(() => {
    list.reload()
    counts.reload()
  })

  if (list.error !== undefined && !list.data) return <PageMessage title="We couldn’t load the registry." onRetry={list.reload} />

  const result = list.data
  const items = result?.items ?? []
  const page = result?.page ?? requested
  const total = result?.totalCount ?? 0
  const pages = Math.max(1, Math.ceil(total / PAGE_SIZE))
  const from = (page - 1) * PAGE_SIZE + 1
  const count = (s?: OrganizationStatus) =>
    counts.data ? (s ? counts.data[s] : Object.values(counts.data).reduce((a, b) => a + b, 0)) : undefined

  function act(o: AdminOrganization, kind: 'Suspend' | 'Reactivate') {
    action.run(o.id, o.name, () => changeOrganization(o.id, kind === 'Suspend' ? 'suspend' : 'reactivate'), kind === 'Suspend' ? 'was suspended.' : 'was reactivated.')
  }

  const rowAction = (o: AdminOrganization) => {
    // Which action a row offers is presentation; the backend re-checks the status on every command.
    const kind = ORG_ACTION[o.status]
    if (kind)
      return (
        <Button
          variant={kind === 'Suspend' ? 'outline' : 'secondary'}
          size="sm"
          className="org-action"
          loading={action.pendingId === o.id}
          disabled={action.busy}
          onClick={() => act(o, kind)}
        >
          {kind}
          <span className="visually-hidden"> {o.name}</span>
        </Button>
      )
    if (o.status === 'Pending')
      return (
        <Button variant="ghost" size="sm" className="org-action" to={PATHS.adminPending}>
          Review<span className="visually-hidden"> {o.name} in the pending queue</span>
        </Button>
      )
    return <span className="org-action--none">No action</span>
  }

  return (
    <div className="container ws-page adm">
      <AdminIntro
        code="ADM-02"
        title={
          <>
            Manage <em>organizations</em>
          </>
        }
        lead="The registry of every donor and beneficiary on FoodLoop. Suspend an organization to pause its activity, or reactivate it once resolved."
        aside={
          <dl className="readout">
            {ORGANIZATION_STATUSES.map((s) => (
              <div key={s} className={cn(s === 'Pending' && (count(s) ?? 0) > 0 && 'is-warn')}>
                <dt>{s}</dt>
                <dd>{count(s) === undefined ? '––' : String(count(s)).padStart(2, '0')}</dd>
              </div>
            ))}
          </dl>
        }
        meta={['Registry', `${count() ?? '…'} records`, `${PAGE_SIZE} per page`]}
      />

      <div className="adm-toolbar">
        <div className="adm-seg" role="group" aria-label="Filter by status">
          {[undefined, ...ORGANIZATION_STATUSES].map((s) => {
            const active = s === status
            return (
              <button
                key={s ?? 'all'}
                type="button"
                className="adm-seg__btn"
                aria-pressed={active}
                onClick={() => update({ status: s ?? null, page: null })}
              >
                {active && <motion.span layoutId="org-filter" className="adm-seg__indicator" transition={spring.indicator} />}
                <span className="adm-seg__label">{s ?? 'All'}</span>
                {count(s) !== undefined && (
                  <span className="adm-seg__count">
                    {count(s)}
                    <span className="visually-hidden"> organizations</span>
                  </span>
                )}
              </button>
            )
          })}
        </div>
      </div>

      <section aria-labelledby="org-results" id="org-region" aria-busy={list.loading}>
        <div className="adm-resultline">
          <h2 id="org-results" className="adm-resultline__title">
            {status ? `${status} organizations` : 'All organizations'}
          </h2>
          <p className="adm-resultline__count" role="status">
            {!result ? 'Loading…' : total ? `Showing ${from}–${from + items.length - 1} of ${total}` : 'No matching organizations'}
          </p>
        </div>

        <div className="adm-notice" role="status">
          {action.notice && (
            <p className="ws-notice">
              {action.notice.ok ? <CheckCircle2 aria-hidden="true" /> : <AlertCircle aria-hidden="true" />}
              {action.notice.text}
            </p>
          )}
        </div>

        {result && items.length === 0 ? (
          <div className="ws-empty adm-empty">
            <h3>No organizations here.</h3>
            <p>Nothing {status ? `with status ${status.toLowerCase()}` : 'in the registry'} yet.</p>
            {status && (
              <Button variant="outline" iconStart={<X />} onClick={() => update({ status: null, page: null })}>
                Clear filter
              </Button>
            )}
          </div>
        ) : desktop ? (
          <div className="ledger-frame">
            <table className="ledger">
              <caption className="visually-hidden">
                Organizations{status ? ` with status ${status}` : ''}, page {page} of {pages}
              </caption>
              <thead>
                <tr>
                  <th scope="col">Organization</th>
                  <th scope="col">Type</th>
                  <th scope="col">License</th>
                  <th scope="col">Status</th>
                  <th scope="col">Joined</th>
                  <th scope="col" className="num">
                    Actions
                  </th>
                </tr>
              </thead>
              <tbody>
                {items.map((o) => (
                  <tr key={o.id} data-status={o.status} className={cn(action.notice?.id === o.id && 'is-flagged')}>
                    <th scope="row" className="ledger__rowhead">
                      <span className="ledger__primary">
                        <strong>{o.name}</strong>
                      </span>
                    </th>
                    <td>{ORGANIZATION_TYPE_LABELS[o.type]}</td>
                    <td>
                      <code className="ledger__mono">{o.licenseNumber}</code>
                    </td>
                    <td>
                      <StatusChip tone={ORG_STATUS_TONE[o.status]}>{o.status}</StatusChip>
                    </td>
                    <td>
                      <time dateTime={o.createdAtUtc} className="ledger__mono">
                        {formatDate(o.createdAtUtc)}
                      </time>
                    </td>
                    <td className="num">{rowAction(o)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : (
          <ul role="list" className="records">
            {items.map((o) => (
              <li key={o.id} className={cn('record', action.notice?.id === o.id && 'is-flagged')} data-status={o.status}>
                <div className="record__head">
                  <h3 className="record__title">{o.name}</h3>
                  <StatusChip tone={ORG_STATUS_TONE[o.status]}>{o.status}</StatusChip>
                </div>
                <dl className="record__facts">
                  <div>
                    <dt>Type</dt>
                    <dd>{ORGANIZATION_TYPE_LABELS[o.type]}</dd>
                  </div>
                  <div>
                    <dt>Joined</dt>
                    <dd>
                      <time dateTime={o.createdAtUtc}>{formatDate(o.createdAtUtc)}</time>
                    </dd>
                  </div>
                  <div>
                    <dt>License</dt>
                    <dd>
                      <code>{o.licenseNumber}</code>
                    </dd>
                  </div>
                </dl>
                {rowAction(o)}
              </li>
            ))}
          </ul>
        )}

        <Pagination page={page} pages={pages} label="Organizations" targetId="org-region" onPage={(n) => update({ page: String(n) })} />
      </section>
    </div>
  )
}
