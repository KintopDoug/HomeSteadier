import { createFileRoute } from '@tanstack/react-router'
import { Show, SignInButton, SignUpButton } from '@clerk/react'

const Home = () => (
  <div className="home-page">
    <Show when="signed-out">
      <h1>Welcome to HomeSteadier</h1>
      <p>Sign in to start managing your homestead.</p>
      <div className="home-page-actions">
        <SignInButton mode="modal" />
        <SignUpButton mode="modal" />
      </div>
    </Show>
    <Show when="signed-in">
      <h1>Your Homestead</h1>
      <p>You're signed in. Homestead dashboard coming soon.</p>
    </Show>
  </div>
)

export const Route = createFileRoute('/')({
  component: Home,
})
