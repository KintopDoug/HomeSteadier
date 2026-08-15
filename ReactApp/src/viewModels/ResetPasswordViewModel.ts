import { makeAutoObservable } from "mobx";
import type { ResetPasswordRequest } from "../models/request/ResetPasswordRequest";
import { AuthApi } from "../api/AuthApi";
import { getApiErrorMessage } from "../api/apiErrors";
import { session } from "../stores/SessionStore";
import { router } from "../router";

/**
 * The confirm field is client-only: it exists to drive the zod cross-field check and is stripped
 * before the request goes out. Hence the intersection rather than the bare generated model that
 * the other view models expose — Form requires `values` and the schema to describe the same shape.
 */
export type ResetPasswordFormValues = ResetPasswordRequest & { confirmPassword: string };

export class ResetPasswordViewModel {
  token: string;
  newPassword = "";
  confirmPassword = "";
  errorMessage: string | null = null;

  constructor(token: string) {
    this.token = token;
    makeAutoObservable(this, {}, { autoBind: true });
  }

  initialize() {
    // Reserved for future async setup (e.g. prefetching data).
  }

  get values(): ResetPasswordFormValues {
    return {
      token: this.token,
      newPassword: this.newPassword,
      confirmPassword: this.confirmPassword,
    };
  }

  setNewPassword(value: string) {
    this.newPassword = value;
  }

  setConfirmPassword(value: string) {
    this.confirmPassword = value;
  }

  setErrorMessage(message: string | null) {
    this.errorMessage = message;
  }

  async submit(values: ResetPasswordFormValues) {
    this.setErrorMessage(null);

    try {
      // confirmPassword is a client-side-only field; the API never sees it.
      const response = await AuthApi.resetPasswordAsync({
        token: values.token,
        newPassword: values.newPassword,
      });

      // The API signs the user in as part of the reset, so there's no trip back to /login.
      session.signIn(response);
      await router.navigate({ to: "/home" });
    } catch (error) {
      this.setErrorMessage(
        getApiErrorMessage(error, "Password reset failed. Please try again."),
      );
    }
  }
}
