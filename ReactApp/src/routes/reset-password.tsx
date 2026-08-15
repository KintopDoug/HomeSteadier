import { createFileRoute } from "@tanstack/react-router";
import { z } from "zod";
import { ResetPassword } from "../pages/ResetPassword";

/**
 * `.catch("")` rather than letting the parse throw: a missing or garbled ?token should render the
 * page's own "this link isn't valid" state, not a search-param error boundary.
 */
const resetPasswordSearchSchema = z.object({
  token: z.string().catch(""),
});

export const Route = createFileRoute("/reset-password")({
  // Explicit function form rather than handing the schema straight to the router, so this doesn't
  // depend on the zod adapter resolving the same way across versions.
  validateSearch: (search: Record<string, unknown>) => resetPasswordSearchSchema.parse(search),
  // Deliberately no already-authenticated guard, unlike /forgot-password: someone with a live
  // session in another tab still has to be able to use a link from their email.
  component: ResetPassword,
});
