import { makeAutoObservable } from "mobx";
import type { LoginRequest } from "../models/request/LoginRequest";
import { AuthApi } from "../api/AuthApi";
import { getApiErrorMessage } from "../api/apiErrors";
import { session } from "../stores/SessionStore";
import { router } from "../router";

export class LoginViewModel {
  email = "";
  password = "";
  errorMessage: string | null = null;

  constructor() {
    makeAutoObservable(this, {}, { autoBind: true });
  }

  initialize() {
    // Reserved for future async setup (e.g. prefetching data).
  }

  get values(): LoginRequest {
    return {
      email: this.email,
      password: this.password,
    };
  }

  setEmail(value: string) {
    this.email = value;
  }

  setPassword(value: string) {
    this.password = value;
  }

  setErrorMessage(message: string | null) {
    this.errorMessage = message;
  }

  async submit(values: LoginRequest) {
    this.setErrorMessage(null);

    try {
      const response = await AuthApi.loginAsync(values);
      session.signIn(response);
      await router.navigate({ to: "/home" });
    } catch (error) {
      this.setErrorMessage(getApiErrorMessage(error, "Sign in failed. Please try again."));
    }
  }
}
