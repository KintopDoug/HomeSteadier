import { createFileRoute } from '@tanstack/react-router'
import { SignUp } from '@clerk/react'

const SignUpPage = () => (
  <div className="auth-page">
    <SignUp routing="path" path="/sign-up" />
  </div>
)

export const Route = createFileRoute('/sign-up/$')({
  component: SignUpPage,
})
