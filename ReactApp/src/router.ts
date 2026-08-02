import { createRouter } from "@tanstack/react-router";
import { routeTree } from "./routeTree.gen";

// Exported separately from main.tsx so view models can call router.navigate(...)
// imperatively after an async action (e.g. sign-in) without needing the useNavigate()
// hook, keeping navigation alongside the rest of that action's logic.
export const router = createRouter({ routeTree });

declare module "@tanstack/react-router" {
  interface Register {
    router: typeof router;
  }
}
