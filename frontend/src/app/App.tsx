import { MotionConfig } from 'framer-motion'
import { createBrowserRouter, RouterProvider } from 'react-router-dom'
import { PageShell } from '../components/layout/PageShell'
import { SessionProvider } from '../lib/session/SessionProvider'
import { Home } from '../pages/home/Home'
import { NotFound } from '../pages/NotFound'
import { RedirectIfAuthenticated, RequireAuthenticated, RequireRole } from './guards'
import { PATHS } from './routes'

// Home ships in the entry chunk (it is the landing page). Everything else is a route-level chunk,
// so Home/Auth never download workspace code. The router resolves a lazy route before switching
// pages, so navigation keeps the current page on screen instead of flashing a fallback.
const fallback = <div className="section" aria-busy="true" />

const router = createBrowserRouter([
  {
    element: <PageShell />,
    // Shown only while a lazy route loads on a cold start.
    hydrateFallbackElement: fallback,
    children: [
      { path: PATHS.home, element: <Home /> },
      {
        element: <RedirectIfAuthenticated />,
        children: [
          { path: PATHS.login, lazy: async () => ({ Component: (await import('../pages/auth/Login')).Login }) },
          { path: PATHS.register, lazy: async () => ({ Component: (await import('../pages/auth/Register')).Register }) },
        ],
      },
      // Dev-only reference page: intentionally absent from every navigation.
      {
        path: PATHS.styleSystem,
        lazy: async () => ({ Component: (await import('../pages/dev/StylePlayground')).StylePlayground }),
      },
      { path: '*', element: <NotFound /> },
    ],
  },
  {
    // Signed-in product shell. Guards shape the UX from the real session; the API still authorizes every request.
    element: <RequireAuthenticated />,
    children: [
      {
        lazy: async () => ({ Component: (await import('../components/workspace/WorkspaceShell')).WorkspaceShell }),
        hydrateFallbackElement: fallback,
        children: [
          {
            element: <RequireRole roles={['beneficiary']} />,
            children: [
              {
                path: PATHS.marketplace,
                handle: { title: 'Marketplace — FoodLoop' },
                lazy: async () => ({ Component: (await import('../pages/marketplace/Marketplace')).Marketplace }),
              },
              {
                path: PATHS.claims,
                handle: { title: 'My claims — FoodLoop' },
                lazy: async () => ({ Component: (await import('../pages/claims/MyClaims')).MyClaims }),
              },
              {
                path: PATHS.claim,
                handle: { title: 'Claim details — FoodLoop' },
                lazy: async () => ({ Component: (await import('../pages/claims/ClaimDetails')).ClaimDetails }),
              },
            ],
          },
          {
            element: <RequireRole roles={['donor']} />,
            children: [
              {
                path: PATHS.donations,
                handle: { title: 'My donations — FoodLoop' },
                lazy: async () => ({ Component: (await import('../pages/donations/MyDonations')).MyDonations }),
              },
              {
                path: PATHS.newDonation,
                handle: { title: 'Create donation — FoodLoop' },
                lazy: async () => ({ Component: (await import('../pages/donations/CreateDonation')).CreateDonation }),
              },
              {
                path: PATHS.editDonation,
                handle: { title: 'Edit donation — FoodLoop' },
                lazy: async () => ({ Component: (await import('../pages/donations/EditDonation')).EditDonation }),
              },
            ],
          },
          {
            element: <RequireRole roles={['donor', 'beneficiary']} />,
            children: [
              {
                path: PATHS.donation,
                handle: { title: 'Donation details — FoodLoop' },
                lazy: async () => ({ Component: (await import('../pages/donations/DonationDetails')).DonationDetails }),
              },
              {
                path: PATHS.organization,
                handle: { title: 'My organization — FoodLoop' },
                lazy: async () => ({ Component: (await import('../pages/organization/MyOrganization')).MyOrganization }),
              },
              {
                path: PATHS.handoverCodes,
                handle: { title: 'Handover codes — FoodLoop' },
                lazy: async () => ({ Component: (await import('../pages/handover/HandoverCodes')).HandoverCodes }),
              },
              {
                path: PATHS.handoverCode,
                handle: { title: 'Handover code — FoodLoop' },
                lazy: async () => ({ Component: (await import('../pages/handover/HandoverPass')).HandoverCodeGone }),
              },
            ],
          },
          {
            element: <RequireRole roles={['courier']} />,
            children: [
              {
                path: PATHS.courierTasks,
                handle: { title: 'My tasks — FoodLoop' },
                lazy: async () => ({ Component: (await import('../pages/courier/MyTasks')).MyTasks }),
              },
              {
                path: PATHS.courierTask,
                handle: { title: 'Task details — FoodLoop' },
                lazy: async () => ({ Component: (await import('../pages/courier/TaskDetails')).TaskDetails }),
              },
              {
                path: PATHS.verifyHandover,
                handle: { title: 'Verify handover — FoodLoop' },
                lazy: async () => ({ Component: (await import('../pages/courier/VerifyHandover')).VerifyHandover }),
              },
            ],
          },
          {
            element: <RequireRole roles={['admin']} />,
            children: [
              {
                path: PATHS.admin,
                handle: { title: 'Operations overview — FoodLoop' },
                lazy: async () => ({ Component: (await import('../pages/admin/AdminDashboard')).AdminDashboard }),
              },
              {
                path: PATHS.adminOrganizations,
                handle: { title: 'Manage organizations — FoodLoop' },
                lazy: async () => ({ Component: (await import('../pages/admin/ManageOrganizations')).ManageOrganizations }),
              },
              {
                path: PATHS.adminPending,
                handle: { title: 'Pending requests — FoodLoop' },
                lazy: async () => ({ Component: (await import('../pages/admin/PendingRequests')).PendingRequests }),
              },
              {
                path: PATHS.adminCourier,
                handle: { title: 'Assign courier — FoodLoop' },
                lazy: async () => ({ Component: (await import('../pages/admin/AssignCourier')).AssignCourier }),
              },
              {
                path: PATHS.adminAudit,
                handle: { title: 'Audit log — FoodLoop' },
                lazy: async () => ({ Component: (await import('../pages/admin/AuditLog')).AuditLog }),
              },
            ],
          },
        ],
      },
    ],
  },
])

// reducedMotion="user": Framer skips transform/layout animation when the OS asks for reduced motion.
export default function App() {
  return (
    <MotionConfig reducedMotion="user">
      <SessionProvider>
        <RouterProvider router={router} />
      </SessionProvider>
    </MotionConfig>
  )
}
