import { createFileRoute, redirect } from "@tanstack/react-router";
import { ForgotPassword } from "../pages/ForgotPassword";
import { session } from "../stores/SessionStore";

export const Route = createFileRoute("/forgot-password")({
  beforeLoad: () => {
    if (session.isAuthenticated) {
      throw redirect({ to: "/home" });
    }
  },
  component: ForgotPassword,
});
