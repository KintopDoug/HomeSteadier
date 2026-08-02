import { createFileRoute, Outlet, redirect } from '@tanstack/react-router'
import { session } from '../stores/SessionStore'

// Pathless layout route (the "_" prefix excludes it from the URL) — guards every route
// nested under it (see _authenticated.home.tsx) rather than repeating the check per page.
export const Route = createFileRoute('/_authenticated')({
  beforeLoad: () => {
    if (!session.isAuthenticated) {
      throw redirect({ to: '/login' })
    }
  },
  component: () => <Outlet />,
})
