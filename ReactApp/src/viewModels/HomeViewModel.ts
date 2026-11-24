import { makeAutoObservable, runInAction } from "mobx";
import type { FarmResponse } from "../models/response/FarmResponse";
import { getApiErrorMessage } from "../api/apiErrors";
import { session } from "../stores/SessionStore";
import { loadFarms } from "../stores/loadFarms";

export class HomeViewModel {
  isLoading = true;
  errorMessage: string | null = null;

  constructor() {
    makeAutoObservable(this, {}, { autoBind: true });
  }

  get farms(): FarmResponse[] {
    return session.farms;
  }

  get needsFarmCreation() {
    return !this.isLoading && this.farms.length === 0;
  }

  get needsFarmSelection() {
    return !this.isLoading && this.farms.length > 1 && !session.activeFarm;
  }

  async initialize() {
    this.isLoading = true;
    this.errorMessage = null;

    try {
      await loadFarms();
    } catch (error) {
      runInAction(() => {
        this.errorMessage = getApiErrorMessage(error, "Unable to load your farms. Please try again.");
      });
    } finally {
      runInAction(() => {
        this.isLoading = false;
      });
    }
  }

  selectFarm(farm: FarmResponse) {
    session.setActiveFarm(farm);
  }
}
