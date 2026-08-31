import { makeAutoObservable, runInAction } from "mobx";
import type { RegisterRequest } from "../models/request/RegisterRequest";
import type { FarmInvitationDetailsResponse } from "../models/response/FarmInvitationDetailsResponse";
import { AuthApi } from "../api/AuthApi";
import { FarmInvitationsApi } from "../api/FarmInvitationsApi";
import { getApiErrorMessage } from "../api/apiErrors";
import { session } from "../stores/SessionStore";
import { router } from "../router";

export class SignUpViewModel {
  private readonly inviteToken?: string;

  email = "";
  password = "";
  firstName = "";
  lastName = "";
  errorMessage: string | null = null;

  invitation: FarmInvitationDetailsResponse | null = null;
  isLoadingInvitation = false;
  invitationError: string | null = null;

  constructor(inviteToken?: string) {
    this.inviteToken = inviteToken;
    makeAutoObservable(this, {}, { autoBind: true });
  }

  get isEmailLocked() {
    return !!this.inviteToken && !!this.invitation;
  }

  async initialize() {
    if (!this.inviteToken) {
      return;
    }

    this.isLoadingInvitation = true;

    try {
      const invitation = await FarmInvitationsApi.getByTokenAsync(this.inviteToken);
      runInAction(() => {
        this.invitation = invitation;
        this.email = invitation.email ?? "";
      });
    } catch (error) {
      runInAction(() => {
        this.invitationError = getApiErrorMessage(
          error,
          "This invitation is invalid or has expired. Please ask the farm admin to send a new one.",
        );
      });
    } finally {
      runInAction(() => {
        this.isLoadingInvitation = false;
      });
    }
  }

  get values(): RegisterRequest {
    return {
      email: this.email,
      password: this.password,
      firstName: this.firstName,
      lastName: this.lastName,
      inviteToken: this.inviteToken,
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

  async submit(values: RegisterRequest) {
    this.setErrorMessage(null);

    try {
      const response = await AuthApi.signUpAsync(values);
      session.signIn(response);
      await router.navigate({ to: "/home" });
    } catch (error) {
      this.setErrorMessage(getApiErrorMessage(error, "Registration failed. Please try again."));
    }
  }
}
