import { makeAutoObservable } from "mobx";
import type { AuthResponse } from "../models/response/AuthResponse";
import type { UserResponse } from "../models/response/UserResponse";

/**
 * App-wide auth session. Unlike the per-page view models in src/viewModels, this is a
 * singleton — the access token and current user are shared by every feature.
 *
 * The access token is held in memory only (never localStorage/sessionStorage) so an XSS bug
 * can't read it back out. That's viable because the API returns the refresh token as an
 * httpOnly cookie, so a dropped session can be restored via AuthApi.refresh() instead of
 * being persisted client-side. Trade-off: a page reload currently signs the user out until
 * refresh-on-startup is wired up.
 *
 * Deliberately holds no API calls of its own — httpClient imports this to read the token, so
 * calling AuthApi from here would create an import cycle. Callers (routes/__root.tsx for the
 * initial restore, view models for everything else) orchestrate the actual AuthApi calls.
 */
class Session {
  accessToken: string | null = null;
  user: UserResponse | null = null;
  // True until the initial refresh-on-load attempt (routes/__root.tsx) settles. Lets any
  // route-guard UI added later tell "not signed in" apart from "haven't checked yet", so it
  // doesn't flash a logged-out state before the refresh cookie has had a chance to restore one.
  isInitializing = true;

  constructor() {
    makeAutoObservable(this, {}, { autoBind: true });
  }

  get isAuthenticated() {
    return this.accessToken !== null;
  }

  setIsInitializing(value: boolean) {
    this.isInitializing = value;
  }

  signIn(response: AuthResponse) {
    this.accessToken = response.token ?? null;
    this.user = response.user ?? null;
  }

  signOut() {
    this.accessToken = null;
    this.user = null;
  }
}

export const session = new Session();
