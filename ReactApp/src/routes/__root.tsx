import { createRootRoute, Outlet } from "@tanstack/react-router";
import { isAxiosError } from "axios";
import { Layout } from "../components/Layout/Layout";
import { AuthApi } from "../api/AuthApi";
import { session } from "../stores/SessionStore";

// Cached rather than re-run on every navigation — beforeLoad on the root route fires on
// every navigation (not just the first), but the session only ever needs restoring once
// per page load.
let sessionRestorePromise: Promise<void> | null = null;

// Restores a session from the httpOnly refresh cookie before any route renders. Awaited
// from beforeLoad (rather than a mount effect) specifically so route guards (see
// _authenticated.tsx, index.tsx, login.tsx) see the real auth state on the very first
// navigation instead of racing the refresh call and treating a not-yet-restored session as
// "logged out". A 401 just means there's no valid refresh cookie (e.g. first visit, or it
// expired) — that's the expected "not signed in" case, not a failure.
function restoreSession(): Promise<void> {
  if (!sessionRestorePromise) {
    sessionRestorePromise = (async () => {
      try {
        const response = await AuthApi.refreshAsync();
        session.signIn(response);
      } catch (error) {
        if (!isAxiosError(error) || error.response?.status !== 401) {
          console.error("Failed to restore session", error);
        }
      } finally {
        session.setIsInitializing(false);
      }
    })();
  }

  return sessionRestorePromise;
}

export const Route = createRootRoute({
  beforeLoad: () => restoreSession(),
  component: () => (
    <Layout>
      <Outlet />
    </Layout>
  ),
});
