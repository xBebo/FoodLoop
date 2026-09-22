import { motion, useReducedMotion, useScroll, useTransform, type Variants } from 'framer-motion'
import { ArrowDown, ArrowRight, ArrowUpRight } from 'lucide-react'
import { useRef } from 'react'
import { EXPLORE_FOOD_PATH, PATHS, SECTIONS } from '../../app/routes'
import { AmbientOrb, BotanicalBranch, BotanicalCorner, BotanicalDecoration } from '../../components/brand/Botanical'
import { MagneticButton } from '../../components/motion/MagneticButton'
import { MediaReveal } from '../../components/motion/MediaReveal'
import { Reveal } from '../../components/motion/Reveal'
import { Button } from '../../components/ui/Button'
import { Photo } from '../../components/ui/Photo'
import { SectionEyebrow } from '../../components/ui/SectionEyebrow'
import { PHOTOS } from '../../data/photography'
import { ease, revealVariants } from '../../lib/motion'
import { PARALLAX_QUERY, useMediaQuery } from '../../lib/useMediaQuery'
import { usePointerParallax } from '../../lib/usePointerParallax'
import { HeroVisual } from './HeroVisual'
import { LoopSection } from './LoopSection'
import { ProductGlimpse } from './ProductGlimpse'
import { RoleExperience } from './RoleExperience'
import './home.css'

export function Home() {
  return (
    <>
      <Hero />
      <LoopSection />
      <ImpactSection />
      <RoleExperience />
      <ProductGlimpse />
      <FinalCta />
    </>
  )
}

/* ------------------------------------------------------------------ 01 Hero */

const heroStagger: Variants = { hidden: {}, visible: { transition: { staggerChildren: 0.11, delayChildren: 0.05 } } }
// Headline lines rise out of a mask.
const lineVariants: Variants = {
  hidden: { y: '108%' },
  visible: { y: '0%', transition: { duration: 0.95, ease: ease.out } },
}

function Hero() {
  const { px, py, handlers } = usePointerParallax()
  const reduced = useReducedMotion()

  return (
    <section className="hero" aria-labelledby="hero-title" {...handlers}>
      <BotanicalDecoration>
        <BotanicalCorner position="top-left" className="hero__contours" />
      </BotanicalDecoration>

      <div className="container hero__grid">
        <motion.div
          className="hero__copy"
          variants={heroStagger}
          initial={reduced ? false : 'hidden'}
          animate="visible"
        >
          <motion.div variants={revealVariants}>
            <SectionEyebrow index="01">Surplus food, rerouted</SectionEyebrow>
          </motion.div>
          <h1 id="hero-title" className="hero__title">
            <span className="hero__line">
              <motion.span variants={lineVariants}>Good food.</motion.span>
            </span>
            <span className="hero__line">
              <motion.span variants={lineVariants}>
                <em>Greater impact.</em>
              </motion.span>
            </span>
          </h1>
          <motion.p variants={revealVariants} className="t-lead hero__lead">
            FoodLoop connects surplus food with communities that can use it, coordinating donors, beneficiaries and
            couriers through one trusted loop.
          </motion.p>
          <motion.div variants={revealVariants} className="cluster hero__ctas">
            <MagneticButton>
              <Button size="lg" to={EXPLORE_FOOD_PATH} iconEnd={<ArrowRight />}>
                Explore available food
              </Button>
            </MagneticButton>
            <Button size="lg" variant="ghost" section={SECTIONS.howItWorks} iconEnd={<ArrowDown />} className="hero__how">
              See how FoodLoop works
            </Button>
          </motion.div>
          <motion.p variants={revealVariants} className="hero__ops">
            <span className="hero__ops-pulse" aria-hidden="true" />
            <span>
              Listing <span aria-hidden="true">→</span> claim <span aria-hidden="true">→</span> pickup{' '}
              <span aria-hidden="true">→</span> delivery. Every handoff visible in one loop.
            </span>
          </motion.p>
        </motion.div>

        <div className="hero__visual-col">
          <HeroVisual px={px} py={py} />
        </div>
      </div>
    </section>
  )
}

/* ------------------------------------------------------------------ 03 Surplus becomes impact */

function ImpactSection() {
  const ref = useRef<HTMLElement>(null)
  const reduced = useReducedMotion()
  const depth = useMediaQuery(PARALLAX_QUERY) && !reduced
  const { scrollYProgress } = useScroll({ target: ref, offset: ['start end', 'end start'] })
  const imgY = useTransform(scrollYProgress, [0, 1], depth ? ['-5%', '5%'] : ['0%', '0%'])
  const contourY = useTransform(scrollYProgress, [0, 1], depth ? [40, -40] : [0, 0])
  const contourRotate = useTransform(scrollYProgress, [0, 1], depth ? [-4, 4] : [0, 0])

  return (
    <section ref={ref} className="impact" aria-labelledby="impact-title">
      <div className="container impact__grid">
        <div className="impact__media-col">
          <MediaReveal from="left" className="impact__media media-frame">
            <motion.div className="impact__img-wrap" style={{ y: imgY }}>
              <Photo photo={PHOTOS.impactSurplus} className="impact__img" />
            </motion.div>
          </MediaReveal>
          <p className="impact__caption t-label" aria-hidden="true">
            End of day · before close
          </p>
        </div>

        <div className="impact__copy">
          <motion.div className="impact__contours" style={{ y: contourY, rotate: contourRotate }} aria-hidden="true">
            <BotanicalCorner position="top-right" />
          </motion.div>
          <Reveal>
            <SectionEyebrow index="03">Surplus becomes impact</SectionEyebrow>
          </Reveal>
          <Reveal delay={0.06}>
            <h2 id="impact-title" className="impact__statement">
              <span className="impact__mark" aria-hidden="true">
                “
              </span>
              Surplus isn’t waste <em>until we let it become&nbsp;waste.</em>
            </h2>
          </Reveal>
          <Reveal delay={0.12} className="impact__body">
            <p>
              Every evening, good food is left over — the last loaves, a case of ripe fruit, a tray that was never
              served. It’s still food. What it lacks is a route to someone who needs it.
            </p>
            <p>
              FoodLoop is that route. Donors list what they have, community organizations claim what they can use, and
              couriers close the distance before the food’s window closes.
            </p>
          </Reveal>
          <Reveal delay={0.18}>
            <p className="impact__sign">The idea FoodLoop is built on</p>
          </Reveal>
        </div>
      </div>
    </section>
  )
}

/* ------------------------------------------------------------------ 06 Final CTA */

function FinalCta() {
  return (
    <section className="final-cta on-dark grain" aria-labelledby="final-title">
      <BotanicalDecoration>
        <AmbientOrb tone="sage" className="final-cta__orb" />
        <BotanicalCorner position="bottom-left" className="final-cta__contours" />
        <BotanicalBranch className="final-cta__branch" />
      </BotanicalDecoration>

      <div className="container final-cta__inner">
        <Reveal>
          <SectionEyebrow index="06">Join the loop</SectionEyebrow>
        </Reveal>
        <Reveal delay={0.06}>
          <h2 id="final-title" className="final-cta__title">
            Food worth saving.
            <br />
            <em>Connections worth building.</em>
          </h2>
        </Reveal>
        <Reveal delay={0.12} className="final-cta__row">
          <p className="final-cta__text">
            Whether you have food to share, people to feed or a route to ride, there is a place for you in the loop.
          </p>
          <div className="cluster final-cta__ctas">
            <MagneticButton>
              <Button variant="on-dark" size="lg" to={EXPLORE_FOOD_PATH} iconEnd={<ArrowRight />}>
                Browse food
              </Button>
            </MagneticButton>
            <Button variant="outline" size="lg" to={PATHS.register} iconEnd={<ArrowUpRight />}>
              Join FoodLoop
            </Button>
          </div>
        </Reveal>
      </div>
    </section>
  )
}
