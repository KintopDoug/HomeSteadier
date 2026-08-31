import { createFileRoute } from "@tanstack/react-router";
import { z } from "zod";
import { AcceptInvitation } from "../pages/AcceptInvitation";

/**
 * `.catch("")` rather than letting the parse throw: a missing or garbled ?token should render the
 * page's own "this link isn't valid" state, not a search-param error boundary. Mirrors
 * reset-password.tsx.
 */
const acceptInvitationSearchSchema = z.object({
  token: z.string().catch(""),
});

export const Route = createFileRoute("/accept-invitation")({
  validateSearch: (search: Record<string, unknown>) => acceptInvitationSearchSchema.parse(search),
  // No login required to accept — token possession is the authorization, same as password reset.
  component: AcceptInvitation,
});
