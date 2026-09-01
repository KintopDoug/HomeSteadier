import { makeAutoObservable, runInAction } from "mobx";
import type { FarmInvitationDetailsResponse } from "../models/response/FarmInvitationDetailsResponse";
import { FarmInvitationsApi } from "../api/FarmInvitationsApi";
import { getApiErrorMessage } from "../api/apiErrors";

const InvalidInvitationMessage =
  "This invitation is invalid or has expired. Please ask the farm admin to send a new one.";

export class AcceptInvitationViewModel {
  private readonly token: string;

  details: FarmInvitationDetailsResponse | null = null;
  isLoading = true;
  isAccepting = false;
  isAccepted = false;
  errorMessage: string | null = null;

  constructor(token: string) {
    this.token = token;
    makeAutoObservable(this, {}, { autoBind: true });
  }

  async initialize() {
    this.isLoading = true;
    this.errorMessage = null;

    try {
      const details = await FarmInvitationsApi.getByTokenAsync(this.token);
      runInAction(() => {
        this.details = details;
      });
    } catch (error) {
      runInAction(() => {
        this.errorMessage = getApiErrorMessage(error, InvalidInvitationMessage);
      });
    } finally {
      runInAction(() => {
        this.isLoading = false;
      });
    }
  }

  async accept() {
    this.isAccepting = true;
    this.errorMessage = null;

    try {
      const details = await FarmInvitationsApi.acceptAsync(this.token);
      runInAction(() => {
        this.details = details;
        // The page's confirmation state is gated on this flag, set only once the backend has
        // actually finished adding the membership — not optimistically on click.
        this.isAccepted = true;
      });
    } catch (error) {
      runInAction(() => {
        this.errorMessage = getApiErrorMessage(error, InvalidInvitationMessage);
      });
    } finally {
      runInAction(() => {
        this.isAccepting = false;
      });
    }
  }
}
