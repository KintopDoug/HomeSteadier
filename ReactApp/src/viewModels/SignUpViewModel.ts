import { makeAutoObservable } from "mobx";

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

  handleSubmit(e: React.FormEvent<HTMLFormElement>) {
    // Not wired to the API yet — the create-user endpoint doesn't exist yet.
    e.preventDefault();
  }
}
