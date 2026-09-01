import { createFileRoute } from "@tanstack/react-router";
import { z } from "zod";
import { SignUp } from "../pages/signUp";

const registerSearchSchema = z.object({
  inviteToken: z.string().optional().catch(undefined),
});

export const Route = createFileRoute("/register")({
  validateSearch: (search: Record<string, unknown>) => registerSearchSchema.parse(search),
  component: SignUp,
});
