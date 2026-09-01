import { createFileRoute } from "@tanstack/react-router";
import { InviteMember } from "../pages/InviteMember";

export const Route = createFileRoute("/_authenticated/invite-member")({
  component: InviteMember,
});
