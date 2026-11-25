import { createFileRoute } from "@tanstack/react-router";
import { EditFarm } from "../pages/EditFarm";

export const Route = createFileRoute("/_authenticated/edit-farm")({
  component: EditFarm,
});
