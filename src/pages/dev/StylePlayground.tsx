import { useReducedMotion } from 'framer-motion'
import { ArrowRight, ArrowUpRight, Bike, Check, HeartHandshake, Leaf, Plus, RotateCcw, Store, Trash2 } from 'lucide-react'
import { useState, type CSSProperties, type ReactNode } from 'react'
import { AmbientOrb, BotanicalBranch, BotanicalCorner, BotanicalDecoration } from '../../components/brand/Botanical'
import { FoodLoopMark, FoodLoopWordmark } from '../../components/brand/FoodLoopMark'
import { MagneticButton } from '../../components/motion/MagneticButton'
import { Reveal, RevealGroup, RevealItem } from '../../components/motion/Reveal'
import { Button } from '../../components/ui/Button'
import { SectionEyebrow } from '../../components/ui/SectionEyebrow'
import { StatusChip } from '../../components/ui/StatusChip'
import { Card, DarkPanel, Surface } from '../../components/ui/Surface'
import {
  CORE_SWATCHES,
  MOTION_TOKENS,
  NEUTRAL_SWATCHES,
  RESCUE_LOG,
  STATUS_SAMPLES,
  TYPE_SCALE,
  type Swatch,
} from '../../data/showcase'
import './playground.css'

/**
 * DEVELOPMENT-ONLY style manifesto (route: /dev/style-system). Not linked from any navigation.
 */
export function StylePlayground() {
  return (
    <>
      <Hero />
      <PaletteSection />
      <TypeSection />
      <ControlsSection />
      <SurfacesSection />
      <MotionSection />
      <BotanicalSection />
    </>
  )
}

const span = (md: number, lg: number, startLg?: number) =>
  ({ '--span-md': md, '--span-lg': lg, ...(startLg && { '--start-lg': startLg }) }) as CSSProperties

function SectionHead({ index, eyebrow, title, lead }: { index: string; eyebrow: string; title: ReactNode; lead: string }) {
  return (
    <Reveal className="pg-head">
      <SectionEyebrow index={index}>{eyebrow}</SectionEyebrow>
      <h2>{title}</h2>
      <p className="t-lead">{lead}</p>
    </Reveal>
  )
}

/* ---------------------------------------------------------------- Hero */

function Hero() {
  return (
    <section className="pg-hero" aria-labelledby="hero-title">
      <BotanicalDecoration>
        <AmbientOrb tone="mint" className="pg-hero__orb" />
        <AmbientOrb tone="sage" className="pg-hero__orb pg-hero__orb--2" />
        <BotanicalBranch className="pg-hero__branch" />
      </BotanicalDecoration>

      <RevealGroup className="container pg-hero__grid">
        <div className="pg-hero__copy">
          <RevealItem>
            <SectionEyebrow index="R1">Design system · Cinematic botanical tech</SectionEyebrow>
          </RevealItem>
          <RevealItem>
            <h1 id="hero-title" className="t-hero">
              Surplus, <em>rerouted</em> with&nbsp;care.
            </h1>
          </RevealItem>
          <RevealItem>
            <p className="t-lead pg-hero__lead">
              A working manifesto for FoodLoop’s interface — the paper, ink, light and motion every screen will be
              built from.
            </p>
          </RevealItem>
          <RevealItem className="cluster pg-hero__ctas">
            <MagneticButton>
              <Button size="lg" href="#palette" iconEnd={<ArrowRight />}>
                Explore the system
              </Button>
            </MagneticButton>
            <Button size="lg" variant="ghost" href="#motion">
              See the motion
            </Button>
          </RevealItem>
        </div>

        <RevealItem className="pg-hero__visual">
          <div className="media-frame pg-hero__media">
            <BotanicalDecoration>
              <AmbientOrb tone="sage" className="pg-hero__media-orb" />
              <BotanicalCorner position="bottom-right" className="pg-hero__media-corner" />
            </BotanicalDecoration>
            <div className="pg-hero__media-mark on-dark">
              <FoodLoopMark />
            </div>
            <p className="pg-hero__media-caption on-dark t-label">Photography slot</p>
          </div>
          <Surface tone="elevated" className="pg-hero__ticket">
            <StatusChip tone="success">Claimed</StatusChip>
            <p className="pg-hero__ticket-title">48 sourdough loaves</p>
            <p className="t-sm t-muted">Crumb &amp; Co. → Eastside Pantry · 18:30</p>
          </Surface>
          <Surface tone="mint" className="pg-hero__stat">
            <span className="t-data pg-hero__stat-num">2.4t</span>
            <span className="t-sm">rescued this week · design placeholder</span>
          </Surface>
        </RevealItem>

        <RevealItem className="pg-hero__meta">
          <dl>
            {[
              ['Canvas', 'Warm paper'],
              ['Type', 'Fraunces / Manrope'],
              ['Grid', '4 · 8 · 12 columns'],
              ['Motion', 'Reduced-motion aware'],
            ].map(([k, v]) => (
              <div key={k}>
                <dt className="t-label">{k}</dt>
                <dd>{v}</dd>
              </div>
            ))}
          </dl>
        </RevealItem>
      </RevealGroup>
    </section>
  )
}

/* ---------------------------------------------------------------- Palette */

function SwatchTile({ s, className }: { s: Swatch; className?: string }) {
  return (
    <RevealItem
      className={`pg-swatch ${s.dark ? 'on-dark' : ''} ${className ?? ''}`}
      style={{ background: `var(${s.token})` }}
    >
      <span className="pg-swatch__name">{s.name}</span>
      <span className="pg-swatch__meta">
        <span className="t-data">{s.hex}</span>
        <span className="pg-swatch__role">{s.role}</span>
      </span>
    </RevealItem>
  )
}

function PaletteSection() {
  const [night, ...rest] = CORE_SWATCHES
  return (
    <section id="palette" className="section">
      <div className="container">
        <SectionHead
          index="01"
          eyebrow="Colour"
          title={
            <>
              A forest at dusk, <em>printed</em> on paper.
            </>
          }
          lead="Cream is the canvas. Forest is the drama — used in deliberate moments, never as wallpaper. Every text pairing clears WCAG AA."
        />
        <RevealGroup className="pg-palette">
          <SwatchTile s={night} className="pg-palette__hero" />
          {rest.map((s) => (
            <SwatchTile key={s.token} s={s} />
          ))}
        </RevealGroup>
        <RevealGroup className="pg-neutrals">
          {NEUTRAL_SWATCHES.map((s) => (
            <RevealItem key={s.token} className="pg-neutral">
              <span className="pg-neutral__dot" style={{ background: `var(${s.token})` }} />
              <span>
                <span className="pg-neutral__name">{s.name}</span>
                <span className="t-data t-sm t-muted">{s.hex}</span>
              </span>
            </RevealItem>
          ))}
        </RevealGroup>
      </div>
    </section>
  )
}

/* ---------------------------------------------------------------- Type */

function TypeSection() {
  return (
    <section id="type" className="section pg-type">
      <div className="container grid">
        <div style={span(8, 5)} className="pg-type__intro">
          <SectionHead
            index="02"
            eyebrow="Typography"
            title={
              <>
                Editorial voice, <em>functional</em> hands.
              </>
            }
            lead="Fraunces carries the story — soft, optical-sized, a little literary. Manrope does the work: labels, data, controls."
          />
          <Reveal className="pg-type__specimen" aria-hidden="true">
            <span className="pg-type__aa">Aa</span>
            <span className="pg-type__aa pg-type__aa--sans">Aa</span>
          </Reveal>
        </div>

        <RevealGroup style={span(8, 7, 6)} className="pg-type__scale">
          {TYPE_SCALE.map((t) => (
            <RevealItem key={t.label} className="pg-type__row">
              <div className="pg-type__row-meta">
                <span className="t-label">{t.label}</span>
                <span className="t-data t-sm t-muted">{t.range}</span>
              </div>
              <p className="display pg-type__sample" style={{ fontSize: `var(${t.token})` }}>
                {t.sample}
              </p>
            </RevealItem>
          ))}
          <RevealItem className="pg-type__row pg-type__row--sans">
            <div className="pg-type__row-meta">
              <span className="t-label">Body · UI · Data</span>
              <span className="t-data t-sm t-muted">Manrope 15–22px</span>
            </div>
            <div className="stack" style={{ '--stack-gap': 'var(--space-3)' } as CSSProperties}>
              <p className="t-lead">Lead — surplus listed before 17:00 is usually claimed within the hour.</p>
              <p>
                Body — readable at arm’s length on a phone in a loading bay: 17px, 1.6 leading, ink on paper at 14:1
                contrast.
              </p>
              <p className="cluster">
                <span className="t-data pg-type__data">1,284</span>
                <span className="t-label t-muted">meals rescued · tabular figures</span>
              </p>
            </div>
          </RevealItem>
        </RevealGroup>
      </div>
    </section>
  )
}

/* ---------------------------------------------------------------- Controls */

function ControlsSection() {
  const [loading, setLoading] = useState(false)

  function simulate() {
    setLoading(true)
    window.setTimeout(() => setLoading(false), 1600)
  }

  return (
    <section id="controls" className="section">
      <div className="container">
        <SectionHead
          index="03"
          eyebrow="Controls"
          title={
            <>
              Actions that feel <em>certain</em>.
            </>
          }
          lead="Pill geometry, confident weight, a 3% press. Focus rings are always visible to keyboards and flip to mint on dark."
        />

        <div className="grid pg-controls">
          <RevealGroup style={span(8, 7)} className="stack pg-controls__col">
            <RevealItem>
              <Card tone="paper" eyebrow="Variants" className="pg-demo">
                <div className="cluster">
                  <Button>Primary</Button>
                  <Button variant="secondary">Secondary</Button>
                  <Button variant="outline">Outline</Button>
                  <Button variant="ghost">Ghost</Button>
                  <Button variant="danger" iconStart={<Trash2 />}>
                    Remove
                  </Button>
                </div>
              </Card>
            </RevealItem>
            <RevealItem>
              <Card tone="paper" eyebrow="Sizes · icons · states" className="pg-demo">
                <div className="cluster">
                  <Button size="sm" iconStart={<Plus />}>
                    Small
                  </Button>
                  <Button size="md" iconEnd={<ArrowRight />}>
                    Medium
                  </Button>
                  <Button size="lg" variant="secondary" iconStart={<Leaf />}>
                    Large
                  </Button>
                </div>
                <div className="cluster">
                  <Button variant="outline" loading={loading} onClick={simulate}>
                    {loading ? 'Saving…' : 'Try loading'}
                  </Button>
                  <Button disabled>Disabled</Button>
                  <Button variant="outline" disabled>
                    Disabled outline
                  </Button>
                </div>
              </Card>
            </RevealItem>
            <RevealItem>
              <DarkPanel className="pg-demo pg-demo--dark" glow={false}>
                <p className="t-label pg-demo__label">On dark</p>
                <div className="cluster">
                  <Button variant="on-dark" iconEnd={<ArrowUpRight />}>
                    Donate surplus
                  </Button>
                  <Button variant="outline">Outline</Button>
                  <Button variant="ghost">Ghost</Button>
                </div>
              </DarkPanel>
            </RevealItem>
          </RevealGroup>

          <RevealGroup style={span(8, 5)} className="stack pg-controls__col">
            <RevealItem>
              <Card tone="sunken" eyebrow="Status chips" className="pg-demo">
                <div className="cluster">
                  {STATUS_SAMPLES.map((s) => (
                    <StatusChip key={s.tone} tone={s.tone}>
                      {s.label}
                    </StatusChip>
                  ))}
                </div>
                <ul role="list" className="pg-log">
                  {RESCUE_LOG.map((r) => (
                    <li key={r.item} className="pg-log__row">
                      <span className="pg-log__time t-data">{r.time}</span>
                      <span className="pg-log__what">
                        <span className="pg-log__item">{r.item}</span>
                        <span className="t-sm t-muted">{r.donor}</span>
                      </span>
                      <StatusChip tone={r.tone}>{r.status}</StatusChip>
                    </li>
                  ))}
                </ul>
              </Card>
            </RevealItem>
            <RevealItem>
              <Card tone="mint" leaf eyebrow="Pointer-aware CTA" className="pg-demo pg-magnetic">
                <p className="t-sm">
                  Leans up to 6px toward a mouse. Ignores touch and pen, and holds still under reduced motion.
                </p>
                <MagneticButton>
                  <Button size="lg" iconEnd={<ArrowRight />}>
                    Hover near me
                  </Button>
                </MagneticButton>
              </Card>
            </RevealItem>
          </RevealGroup>
        </div>
      </div>
    </section>
  )
}

/* ---------------------------------------------------------------- Surfaces */

const ROLES = [
  { icon: Store, title: 'Donors', copy: 'List surplus in under a minute, set a pickup window, done.' },
  { icon: HeartHandshake, title: 'Organisations', copy: 'Claim what your community needs before it spoils.' },
  { icon: Bike, title: 'Couriers', copy: 'Short, well-timed runs that close the loop.' },
]

function SurfacesSection() {
  return (
    <section id="surfaces" className="section pg-surfaces">
      <div className="container">
        <SectionHead
          index="04"
          eyebrow="Surfaces"
          title={
            <>
              Layered like <em>pressed</em> paper.
            </>
          }
          lead="Paper, mint, sunken, elevated and dark. The leaf corner is a signature — use it once or twice per view, not everywhere."
        />

        <RevealGroup className="pg-bento">
          <RevealItem className="pg-bento__listing">
            <Card tone="elevated" leaf eyebrow="Elevated · leaf" title="Sourdough & rye, 48 loaves" className="pg-listing">
              <div className="cluster">
                <StatusChip tone="warning">Pickup soon</StatusChip>
                <StatusChip tone="neutral" dot={false}>
                  Bakery
                </StatusChip>
              </div>
              <dl className="pg-listing__facts">
                <div>
                  <dt className="t-label t-muted">Donor</dt>
                  <dd>Crumb &amp; Co.</dd>
                </div>
                <div>
                  <dt className="t-label t-muted">Window</dt>
                  <dd className="t-data">17:30 – 18:30</dd>
                </div>
                <div>
                  <dt className="t-label t-muted">Weight</dt>
                  <dd className="t-data">31 kg</dd>
                </div>
              </dl>
              <div className="cluster">
                <Button size="sm" iconStart={<Check />}>
                  Claim
                </Button>
                <Button size="sm" variant="ghost">
                  Details
                </Button>
              </div>
            </Card>
          </RevealItem>

          <RevealItem className="pg-bento__stat">
            <Surface tone="mint" className="pg-stat">
              <span className="t-label">This week</span>
              <span className="pg-stat__num t-data">2.4t</span>
              <span className="t-sm">of good food kept in circulation across 38 partners.</span>
              <BotanicalCorner position="bottom-right" className="pg-stat__corner" />
            </Surface>
          </RevealItem>

          <RevealItem className="pg-bento__dark">
            <DarkPanel className="pg-quote">
              <p className="t-label pg-demo__label">Dark panel</p>
              <p className="display pg-quote__text">
                Dark forest is a <em>moment</em>, not a background.
              </p>
            </DarkPanel>
          </RevealItem>

          {ROLES.map(({ icon: Icon, title, copy }) => (
            <RevealItem key={title} className="pg-bento__role">
              <Card as="article" tone="paper" interactive className="pg-role">
                <span className="pg-role__icon" aria-hidden="true">
                  <Icon />
                </span>
                <h3 className="t-h4">
                  <a href="#surfaces" className="pg-role__link">
                    {title}
                  </a>
                </h3>
                <p className="t-sm t-muted">{copy}</p>
                <ArrowUpRight className="pg-role__arrow" aria-hidden="true" />
              </Card>
            </RevealItem>
          ))}
        </RevealGroup>
      </div>
    </section>
  )
}

/* ---------------------------------------------------------------- Motion */

function MotionSection() {
  const reduced = useReducedMotion()
  const [run, setRun] = useState(0)

  return (
    <section id="motion" className="section">
      <div className="container grid">
        <div style={span(8, 5)}>
          <SectionHead
            index="05"
            eyebrow="Motion"
            title={
              <>
                Quiet, quick, <em>never</em> bouncy.
              </>
            }
            lead="Transform and opacity only. Springs are damped to settle without wobble. Everything collapses to instant when the system asks for reduced motion."
          />
          <Reveal className="cluster pg-motion__status">
            <StatusChip tone={reduced ? 'info' : 'success'}>
              {reduced ? 'Reduced motion is on' : 'Full motion'}
            </StatusChip>
            <Button variant="outline" size="sm" iconStart={<RotateCcw />} onClick={() => setRun((r) => r + 1)}>
              Replay reveal
            </Button>
          </Reveal>
        </div>

        <RevealGroup key={run} style={span(8, 6, 7)} className="pg-motion__tokens">
          {MOTION_TOKENS.map((m, i) => (
            <RevealItem key={m.name} className={`pg-token pg-token--${i}`}>
              <span className="t-label t-muted">{m.name}</span>
              <span className="pg-token__value t-data">{m.value}</span>
              <span className="t-sm">{m.use}</span>
              <span className="pg-token__bar" style={{ '--i': i } as CSSProperties} aria-hidden="true" />
            </RevealItem>
          ))}
        </RevealGroup>
      </div>
    </section>
  )
}

/* ---------------------------------------------------------------- Botanical + brand */

function BotanicalSection() {
  return (
    <section id="botanical" className="section section--tight">
      <div className="container">
        <Reveal>
          <DarkPanel className="pg-botanical">
            <BotanicalDecoration>
              <BotanicalCorner position="top-left" className="pg-botanical__corner" />
              <BotanicalBranch className="pg-botanical__branch" />
            </BotanicalDecoration>

            <div className="pg-botanical__copy">
              <SectionEyebrow index="06">Brand &amp; botanicals</SectionEyebrow>
              <h2>
                A loop that closes <em>with a leaf</em>.
              </h2>
              <p className="t-lead">
                The mark is a rescue cycle left open, then completed by growth. Line art stays hairline-thin: stems,
                contours, seeds — editorial, never cartoon.
              </p>
            </div>

            <div className="pg-marks">
              <div className="pg-marks__row" aria-label="Mark at 16, 24, 32, 48 and 88 pixels" role="img">
                {[16, 24, 32, 48, 88].map((s) => (
                  <FoodLoopMark key={s} style={{ width: s, height: s }} />
                ))}
              </div>
              <div className="pg-marks__lockups">
                <FoodLoopWordmark className="pg-marks__wordmark" />
                <div className="pg-marks__paper">
                  <FoodLoopWordmark />
                  <img src="/favicon.svg" alt="FoodLoop favicon" width="32" height="32" />
                </div>
              </div>
            </div>
          </DarkPanel>
        </Reveal>
      </div>
    </section>
  )
}
