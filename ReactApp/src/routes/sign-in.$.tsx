import { createFileRoute } from '@tanstack/react-router'
import { SignIn } from '@clerk/react'

const SignInPage = () => (
  <div className="auth-page">
    <SignIn routing="path" path="/sign-in" />
  </div>
)

export const Route = createFileRoute('/sign-in/$')({
  component: SignInPage,
})
