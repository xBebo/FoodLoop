import { AnimatePresence, motion, useReducedMotion } from 'framer-motion'
import { ArrowLeft, ArrowRight, RotateCcw, Search, X } from 'lucide-react'
import { useEffect, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { BotanicalCorner, BotanicalDecoration } from '../../components/brand/Botanical'
import { DonationCard } from '../../components/food/DonationCard'
import { listingOf } from '../../components/food/presentation'
import { Button } from '../../components/ui/Button'
import { SectionEyebrow } from '../../components/ui/SectionEyebrow'
import { ApiError } from '../../lib/api/client'
import { getCategories, getMarketplace } from '../../lib/api/marketplace'
import { useLoad } from '../../lib/api/useLoad'
import { cn } from '../../lib/cn'
import { duration, ease, spring } from '../../lib/motion'
import './marketplace.css'

const GUID = /^[0-9a-f]{8}-(?:[0-9a-f]{4}-){3}[0-9a-f]{12}$/i

export function Marketplace() {
  const reduced = useReducedMotion()
  // Filters and page live in the URL so back/forward and "back from details" restore them.
  const [params, setParams] = useSearchParams()
  const query = params.get('q') ?? ''
  const rawCategory = params.get('category')
  const categoryId = rawCategory && GUID.test(rawCategory) ? rawCategory : null
  const page = Math.max(1, Math.floor(Number(params.get('page'))) || 1)

  // Typing waits a moment before asking the server; filters and paging ask at once.
  // The server does the searching (title only) and the paging.
  const [search, setSearch] = useState(query.trim())
  useEffect(() => {
    const timer = window.setTimeout(() => setSearch(query.trim()), 250)
    return () => window.clearTimeout(timer)
  }, [query])

  const categories = useLoad('categories', getCategories)
  const listings = useLoad(`${search}|${categoryId ?? ''}|${page}`, (signal) =>
    getMarketplace({ search, categoryId: categoryId ?? undefined, page }, signal),
  )

  function update(changes: Partial<Record<'q' | 'category' | 'page', string | null>>) {
    // Read the live URL: the router's `prev` can be stale when two updates land in quick succession.
    const next = new URLSearchParams(window.location.search)
    for (const [key, value] of Object.entries(changes)) {
      if (value) next.set(key, value)
      else next.delete(key)
    }
    // A new search or category starts again from the first page. Page moves are real history entries.
    const paging = 'page' in changes
    if (!paging) next.delete('page')
    setParams(next, { replace: !paging, preventScrollReset: !paging })
  }

  const categoryName = categories.data?.find((c) => c.id === categoryId)?.name
  const result = listings.error === undefined ? listings.data : undefined
  const items = result?.items.map(listingOf) ?? []
  const forbidden = listings.error instanceof ApiError && listings.error.code === 'organization.not_active'

  return (
    <div className="market">
      <section className="market-hero on-dark grain" aria-labelledby="market-title">
        <BotanicalDecoration>
          <BotanicalCorner position="top-right" className="market-hero__contours" />
        </BotanicalDecoration>

        <div className="container market-hero__grid">
          <motion.div
            className="market-hero__copy"
            initial={reduced ? false : { opacity: 0, y: 18 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ duration: 0.6, ease: ease.out }}
          >
            <SectionEyebrow>Marketplace · Open listings</SectionEyebrow>
            <h1 id="market-title" className="market-hero__title">
              Available <em>food</em>
            </h1>
            <p className="t-lead market-hero__lead">
              Surplus listed by donors across the city, soonest to close first. Claim what your organization can
              store and serve — the donor sees your claim straight away.
            </p>
          </motion.div>
        </div>
      </section>

      {/* Control deck: overlaps the dark band, so search reads as the page's primary tool. */}
      <div className="container">
        <div className="market-deck">
          <form role="search" className="market-search" onSubmit={(e) => e.preventDefault()}>
            <label htmlFor="market-q" className="visually-hidden">
              Search available food by title
            </label>
            <Search aria-hidden="true" className="market-search__icon" />
            <input
              id="market-q"
              type="search"
              className="market-search__input"
              placeholder="Search by food title"
              value={query}
              onChange={(e) => update({ q: e.target.value })}
              autoComplete="off"
            />
            {query && (
              <button type="button" className="market-search__clear" aria-label="Clear search" onClick={() => update({ q: null })}>
                <X aria-hidden="true" />
              </button>
            )}
          </form>

          {categories.data ? (
            <div className="market-cats" role="group" aria-label="Filter by category">
              {[null, ...categories.data].map((c) => {
                const active = (c?.id ?? null) === categoryId
                return (
                  <button
                    key={c?.id ?? 'all'}
                    type="button"
                    className={cn('market-cat', active && 'is-active')}
                    aria-pressed={active}
                    onClick={() => update({ category: c?.id ?? null })}
                  >
                    {active && <motion.span layoutId="market-cat-indicator" className="market-cat__indicator" transition={spring.indicator} />}
                    <span className="market-cat__label">{c ? c.name : 'All'}</span>
                  </button>
                )
              })}
            </div>
          ) : (
            categories.error !== undefined && (
              <p className="market-cats__note">
                Categories couldn’t load, so filtering is off for now.{' '}
                <button type="button" className="link-underline" onClick={categories.reload}>
                  Try again
                </button>
              </p>
            )
          )}
        </div>
      </div>

      <section className="container market-results" aria-labelledby="results-title" aria-busy={listings.loading}>
        <div className="market-results__head">
          <h2 id="results-title" className="market-results__title">
            {categoryName ?? 'All listings'}
          </h2>
          <p className="market-results__count" role="status">
            {listings.loading
              ? 'Loading listings…'
              : result
                ? `${items.length} ${items.length === 1 ? 'listing' : 'listings'}${result.hasPrevious || result.hasNext ? ` on page ${result.page}` : ''}`
                : ''}
          </p>
        </div>

        {listings.error !== undefined ? (
          <div className="ws-empty">
            <h3 className="t-h3">{forbidden ? 'The marketplace isn’t open to your organization.' : 'We couldn’t load listings.'}</h3>
            <p>
              {forbidden
                ? 'Only active beneficiary organizations can browse and claim food. Contact the FoodLoop team if this looks wrong.'
                : 'Check your connection and try again.'}
            </p>
            {!forbidden && (
              <Button variant="outline" iconStart={<RotateCcw />} onClick={listings.reload}>
                Try again
              </Button>
            )}
          </div>
        ) : !result ? (
          <div className="market-grid" aria-hidden="true">
            {[0, 1, 2].map((i) => (
              <div key={i} className="market-skeleton" />
            ))}
          </div>
        ) : items.length > 0 ? (
          <ul role="list" className="market-grid">
            <AnimatePresence mode="popLayout" initial={!reduced}>
              {items.map((d, i) => (
                <motion.li
                  key={d.id}
                  layout="position"
                  className={cn('market-grid__item', i === 0 && items.length > 2 && 'is-feature')}
                  initial={reduced ? false : { opacity: 0, y: 28 }}
                  animate={{ opacity: 1, y: 0 }}
                  exit={{ opacity: 0, scale: 0.97, transition: { duration: duration.fast } }}
                  transition={{ duration: 0.5, ease: ease.out, delay: Math.min(i, 8) * 0.05 }}
                >
                  <DonationCard donation={d} feature={i === 0 && items.length > 2} />
                </motion.li>
              ))}
            </AnimatePresence>
          </ul>
        ) : (
          <div className="ws-empty">
            <h3 className="t-h3">Nothing matches that yet.</h3>
            <p>
              No open listings {query.trim() ? `with “${query.trim()}” in the title` : 'match this filter'}
              {categoryName ? ` in ${categoryName.toLowerCase()}` : ''}. New surplus is listed throughout the day.
            </p>
            {(query || categoryId || page > 1) && (
              <Button variant="outline" onClick={() => setParams({}, { replace: true, preventScrollReset: true })}>
                Clear search and filters
              </Button>
            )}
          </div>
        )}

        {result && (result.hasPrevious || result.hasNext) && (
          <nav className="market-pager" aria-label="Listing pages">
            <Button
              variant="outline"
              iconStart={<ArrowLeft />}
              disabled={!result.hasPrevious || listings.loading}
              onClick={() => update({ page: result.page > 2 ? String(result.page - 1) : null })}
            >
              Previous<span className="visually-hidden"> page</span>
            </Button>
            <span className="market-pager__page t-data">Page {result.page}</span>
            <Button
              variant="outline"
              iconEnd={<ArrowRight />}
              disabled={!result.hasNext || listings.loading}
              onClick={() => update({ page: String(result.page + 1) })}
            >
              Next<span className="visually-hidden"> page</span>
            </Button>
          </nav>
        )}
      </section>
    </div>
  )
}
