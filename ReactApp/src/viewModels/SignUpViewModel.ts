import { makeAutoObservable } from "mobx";
import type { RegisterRequest } from "../models/request/RegisterRequest";

export class SignUpViewModel {
  email = "";
  password = "";
  firstName = "";
  lastName = "";

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

  submit(values: RegisterRequest) {
    // Not wired to the API yet — the create-user endpoint isn't wired up yet.
    void values;
  }
}
