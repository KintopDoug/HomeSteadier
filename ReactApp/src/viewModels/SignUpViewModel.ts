import { makeAutoObservable } from "mobx";
import { isAxiosError } from "axios";
import type { RegisterRequest } from "../models/request/RegisterRequest";
import { AuthApi } from "../api/AuthApi";

export class SignUpViewModel {
  email = "";
  password = "";
  firstName = "";
  lastName = "";
  errorMessage: string | null = null;
  isRegistered = false;

  constructor() {
    makeAutoObservable(this, {}, { autoBind: true });
  }

  initialize() {
    // Reserved for future async setup (e.g. prefetching data).
  }

  get values(): RegisterRequest {
    return {
      email: this.email,
      password: this.password,
      firstName: this.firstName,
      lastName: this.lastName,
    };
  }

  setEmail(value: string) {
    this.email = value;
  }

  setPassword(value: string) {
    this.password = value;
  }

  setFirstName(value: string) {
    this.firstName = value;
  }

  setLastName(value: string) {
    this.lastName = value;
  }

  setErrorMessage(message: string | null) {
    this.errorMessage = message;
  }

  setIsRegistered(value: boolean) {
    this.isRegistered = value;
  }

  async submit(values: RegisterRequest) {
    this.setErrorMessage(null);

    try {
      await AuthApi.Register(values);
      this.setIsRegistered(true);
    } catch (error) {
      this.setErrorMessage(isAxiosError<{ message?: string }>(error)
        ? (error.response?.data.message ?? "Registration failed. Please try again.")
        : "Registration failed. Please try again.");
    }
  }
}
