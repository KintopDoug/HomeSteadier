import { createFileRoute, redirect } from '@tanstack/react-router'
import { Login } from '../pages/Login'
import { session } from '../stores/SessionStore'

export const Route = createFileRoute('/login')({
  beforeLoad: () => {
    if (session.isAuthenticated) {
      throw redirect({ to: '/home' })
    }
  },
  component: Login,
})
