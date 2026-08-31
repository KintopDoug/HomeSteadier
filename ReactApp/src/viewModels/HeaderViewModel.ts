import { makeAutoObservable } from "mobx";
import { AuthApi } from "../api/AuthApi";
import { session } from "../stores/SessionStore";
import { router } from "../router";

export class HeaderViewModel {
  isLoggingOut = false;
  userMenuAnchor: HTMLElement | null = null;

  constructor() {
    makeAutoObservable(this, {}, { autoBind: true });
  }

  setIsLoggingOut(value: boolean) {
    this.isLoggingOut = value;
  }

  openUserMenu(anchor: HTMLElement) {
    this.userMenuAnchor = anchor;
  }

  closeUserMenu() {
    this.userMenuAnchor = null;
  }

  async logout() {
    this.setIsLoggingOut(true);

    try {
      await AuthApi.logoutAsync();
    } catch {
      // Handle logout error
    } finally {
      session.signOut();
      this.setIsLoggingOut(false);
      // Signing out only clears MobX state — it isn't a navigation, so the
      // _authenticated route guard never re-runs on its own. Without this, signing out
      // from a protected page (e.g. /home) would leave that page rendered.
      await router.navigate({ to: "/login" });
    }
  }
}
