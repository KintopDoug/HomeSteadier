import { createFileRoute, redirect } from "@tanstack/react-router";
import { session } from "../stores/SessionStore";
import { Login } from "../pages/Login";

export const Route = createFileRoute("/")({
  beforeLoad: () => {
    if (session.isAuthenticated) {
      throw redirect({ to: "/home" });
    }
  },
  component: Login,
});
