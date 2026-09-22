import { ArrowLeft } from 'lucide-react'
import type { CSSProperties } from 'react'
import { PATHS } from '../app/routes'
import { BotanicalCorner, BotanicalDecoration } from '../components/brand/Botanical'
import { Button } from '../components/ui/Button'
import { SectionEyebrow } from '../components/ui/SectionEyebrow'

/** Catch-all for routes that do not exist yet (legal pages, future phases). */
export function NotFound() {
  return (
    <section className="section not-found" aria-labelledby="not-found-title">
      <BotanicalDecoration>
        <BotanicalCorner position="top-left" />
      </BotanicalDecoration>
      <div className="container container--text stack" style={{ '--stack-gap': 'var(--space-5)' } as CSSProperties}>
        <SectionEyebrow index="404">Not in the loop yet</SectionEyebrow>
        <h1 id="not-found-title">
          This page is still <em>growing</em>.
        </h1>
        <p className="t-lead">It doesn’t exist yet, or it has moved. Everything live today starts from the home page.</p>
        <div className="cluster">
          <Button to={PATHS.home} iconStart={<ArrowLeft />}>
            Back to home
          </Button>
        </div>
      </div>
    </section>
  )
}
