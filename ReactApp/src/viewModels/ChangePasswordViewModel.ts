import { makeAutoObservable } from "mobx";
import type { ChangePasswordRequest } from "../models/request/ChangePasswordRequest";
import { AuthApi } from "../api/AuthApi";
import { getApiErrorMessage } from "../api/apiErrors";
import { session } from "../stores/SessionStore";

/** Confirm is client-only — see the note on ResetPasswordViewModel's equivalent type. */
export type ChangePasswordFormValues = ChangePasswordRequest & { confirmPassword: string };

export class ChangePasswordViewModel {
  currentPassword = "";
  newPassword = "";
  confirmPassword = "";
  errorMessage: string | null = null;
  successMessage: string | null = null;

  constructor() {
    makeAutoObservable(this, {}, { autoBind: true });
  }

  initialize() {
    // Reserved for future async setup (e.g. prefetching data).
  }

  get values(): ChangePasswordFormValues {
    return {
      currentPassword: this.currentPassword,
      newPassword: this.newPassword,
      confirmPassword: this.confirmPassword,
    };
  }

  setCurrentPassword(value: string) {
    this.currentPassword = value;
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

  setSuccessMessage(message: string | null) {
    this.successMessage = message;
  }

  clearFields() {
    this.currentPassword = "";
    this.newPassword = "";
    this.confirmPassword = "";
  }

  async submit(values: ChangePasswordFormValues) {
    this.setErrorMessage(null);
    this.setSuccessMessage(null);

    try {
      // confirmPassword is a client-side-only field; the API never sees it.
      const response = await AuthApi.changePasswordAsync({
        currentPassword: values.currentPassword,
        newPassword: values.newPassword,
      });

      // The change revokes every refresh token, including this tab's. The API hands back a fresh
      // session so the caller stays signed in; without adopting it here, this tab would silently
      // drop out as soon as its access token expired.
      session.signIn(response);

      this.clearFields();
      this.setSuccessMessage(
        "Your password has been changed. Any other devices you were signed in on have been signed out.",
      );
    } catch (error) {
      this.setErrorMessage(
        getApiErrorMessage(error, "Password change failed. Please try again."),
      );
    }
  }
}
