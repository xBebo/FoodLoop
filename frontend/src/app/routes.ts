// Information architecture (R2 public site + R3–R5 workspace). `section` targets an id on the Home page (see SectionLink).

export const PATHS = {
  home: '/',
  login: '/login',
  register: '/register',
  styleSystem: '/dev/style-system',
  marketplace: '/marketplace',
  donations: '/donations',
  newDonation: '/donations/new',
  donation: '/donations/:id',
  editDonation: '/donations/:id/edit',
  organization: '/organization',
  claims: '/claims',
  claim: '/claims/:id',
  courierTasks: '/courier/tasks',
  courierTask: '/courier/tasks/:id',
  verifyHandover: '/courier/tasks/:id/verify',
  handoverCodes: '/handover/codes',
  handoverCode: '/handover/codes/:id',
  admin: '/admin',
  adminOrganizations: '/admin/organizations',
  adminPending: '/admin/organizations/pending',
  adminCourier: '/admin/courier',
  adminAudit: '/admin/audit',
} as const

export const donationPath = (id: string) => `/donations/${id}`
export const editDonationPath = (id: string) => `/donations/${id}/edit`
export const claimPath = (id: string) => `/claims/${id}`
export const courierTaskPath = (id: string) => `/courier/tasks/${id}`
export const verifyHandoverPath = (id: string) => `/courier/tasks/${id}/verify`
export const handoverCodePath = (id: string) => `/handover/codes/${id}`

export const EXPLORE_FOOD_PATH = PATHS.marketplace

export type WorkspaceRole = 'donor' | 'beneficiary' | 'courier' | 'admin'

/**
 * Signed-in product navigation per role. The role comes from the real session (GET /api/auth/session);
 * `home` is where login lands. Marketplace is Beneficiary-only, matching the API.
 */
export const WORKSPACE_ROLES: { id: WorkspaceRole; label: string; home: string; nav: NavItem[] }[] = [
  {
    id: 'donor',
    label: 'Donor',
    home: PATHS.donations,
    nav: [
      { label: 'My donations', to: PATHS.donations },
      { label: 'Handover codes', to: PATHS.handoverCodes },
      { label: 'My organization', to: PATHS.organization },
    ],
  },
  {
    id: 'beneficiary',
    label: 'Beneficiary',
    home: PATHS.marketplace,
    nav: [
      { label: 'Marketplace', to: PATHS.marketplace },
      { label: 'My claims', to: PATHS.claims },
      { label: 'Handover codes', to: PATHS.handoverCodes },
    ],
  },
  { id: 'courier', label: 'Courier', home: PATHS.courierTasks, nav: [{ label: 'My tasks', to: PATHS.courierTasks }] },
  {
    id: 'admin',
    label: 'Admin',
    home: PATHS.admin,
    nav: [
      { label: 'Overview', to: PATHS.admin },
      { label: 'Organizations', to: PATHS.adminOrganizations },
      { label: 'Pending requests', to: PATHS.adminPending },
      { label: 'Assign courier', to: PATHS.adminCourier },
      { label: 'Audit log', to: PATHS.adminAudit },
    ],
  },
]

export const SECTIONS = {
  howItWorks: 'how-it-works',
  roles: 'roles',
  platform: 'platform',
} as const

export type NavItem = { label: string; to: string; section?: string }
export type NavGroup = { title: string; items: NavItem[] }

export const PRIMARY_NAV: NavItem[] = [
  { label: 'Home', to: PATHS.home },
  { label: 'How it works', to: PATHS.home, section: SECTIONS.howItWorks },
  { label: 'Login', to: PATHS.login },
  { label: 'Register', to: PATHS.register },
]

export const FOOTER_NAV: NavGroup[] = [
  {
    title: 'Platform',
    items: [
      { label: 'How it works', to: PATHS.home, section: SECTIONS.howItWorks },
      { label: 'Roles', to: PATHS.home, section: SECTIONS.roles },
      { label: 'Product', to: PATHS.home, section: SECTIONS.platform },
    ],
  },
  {
    title: 'Account',
    items: [
      { label: 'Log in', to: PATHS.login },
      { label: 'Join FoodLoop', to: PATHS.register },
    ],
  },
]

export const LEGAL_NAV: NavItem[] = [
  { label: 'Privacy', to: '/privacy' },
  { label: 'Terms', to: '/terms' },
  { label: 'Accessibility', to: '/accessibility' },
]

export const PAGE_TITLES: Record<string, string> = {
  [PATHS.home]: 'FoodLoop — Good food. Greater impact.',
  [PATHS.login]: 'Log in — FoodLoop',
  [PATHS.register]: 'Join FoodLoop',
  [PATHS.styleSystem]: 'Style system (dev) — FoodLoop',
}

/** Routes rendered without the site footer (full-height split layouts). */
export const BARE_PATHS: string[] = [PATHS.login, PATHS.register]
