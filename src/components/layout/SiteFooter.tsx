import { FOOTER_NAV, LEGAL_NAV } from '../../app/routes'
import { AmbientOrb, BotanicalBranch, BotanicalDecoration } from '../brand/Botanical'
import { FoodLoopWordmark } from '../brand/FoodLoopMark'
import { NavItemLink } from './SectionLink'
import './layout.css'

export function SiteFooter() {
  return (
    <footer className="site-footer on-dark grain">
      <BotanicalDecoration>
        <AmbientOrb tone="forest" className="site-footer__orb" />
        <BotanicalBranch className="site-footer__branch" draw={false} />
      </BotanicalDecoration>

      <div className="container site-footer__inner">
        <div className="site-footer__top">
          <div className="site-footer__statement">
            <FoodLoopWordmark />
            <p className="display site-footer__headline">
              Good food should <em>travel</em>, not&nbsp;spoil.
            </p>
            <p className="site-footer__mission">
              FoodLoop connects donors, community organisations and couriers so surplus reaches people within hours —
              not landfill.
            </p>
          </div>

          <nav className="site-footer__nav" aria-label="Footer">
            {FOOTER_NAV.map((group) => (
              <div key={group.title} className="site-footer__group">
                <h2 className="t-label site-footer__heading">{group.title}</h2>
                <ul role="list">
                  {group.items.map((item) => (
                    <li key={item.label}>
                      <NavItemLink item={item} className="site-footer__link">
                        {item.label}
                      </NavItemLink>
                    </li>
                  ))}
                </ul>
              </div>
            ))}
          </nav>
        </div>

        <div className="site-footer__bottom">
          <p>© {new Date().getFullYear()} FoodLoop. Surplus, rerouted with care.</p>
          <ul role="list" className="site-footer__legal" aria-label="Legal">
            {LEGAL_NAV.map((item) => (
              <li key={item.label}>
                <NavItemLink item={item} className="site-footer__link">
                  {item.label}
                </NavItemLink>
              </li>
            ))}
          </ul>
        </div>
      </div>
    </footer>
  )
}
