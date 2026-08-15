import { makeAutoObservable } from "mobx";
import type { ForgotPasswordRequest } from "../models/request/ForgotPasswordRequest";
import { AuthApi } from "../api/AuthApi";
import { getApiErrorMessage } from "../api/apiErrors";

export class ForgotPasswordViewModel {
  email = "";
  errorMessage: string | null = null;
  /**
   * Set once the request succeeds. The page swaps the form out for this message rather than
   * inviting a resubmit — each request supersedes the previous link, so sending twice and then
   * opening the older email would fail.
   */
  successMessage: string | null = null;

  constructor() {
    makeAutoObservable(this, {}, { autoBind: true });
  }

  initialize() {
    // Reserved for future async setup (e.g. prefetching data).
  }

  get values(): ForgotPasswordRequest {
    return {
      email: this.email,
    };
  }

  setEmail(value: string) {
    this.email = value;
  }

  setErrorMessage(message: string | null) {
    this.errorMessage = message;
  }

  setSuccessMessage(message: string | null) {
    this.successMessage = message;
  }

  async submit(values: ForgotPasswordRequest) {
    this.setErrorMessage(null);

    try {
      await AuthApi.forgotPasswordAsync(values);

      // Phrased so it reveals nothing: the API answers identically whether or not the address
      // has an account, and this message has to match that.
      this.setSuccessMessage(
        "If an account exists for that email, we've sent a password reset link. " +
          "It expires in 60 minutes, and only the most recent link will work.",
      );
    } catch (error) {
      this.setErrorMessage(
        getApiErrorMessage(error, "We couldn't send a reset link. Please try again."),
      );
    }
  }
}
