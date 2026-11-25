import { createFileRoute } from "@tanstack/react-router";
import { FarmMembers } from "../pages/FarmMembers";

export const Route = createFileRoute("/_authenticated/farm-members")({
  component: FarmMembers,
});
