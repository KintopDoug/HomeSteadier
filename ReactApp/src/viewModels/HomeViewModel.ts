import { makeAutoObservable, runInAction } from "mobx";
import type { FarmResponse } from "../models/response/FarmResponse";
import { FarmApi } from "../api/FarmApi";
import { getApiErrorMessage } from "../api/apiErrors";
import { session } from "../stores/SessionStore";

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
      const farms = await FarmApi.getAllAsync();
      session.setFarms(farms);

      if (farms.length === 1) {
        session.setActiveFarm(farms[0]);
      } else if (farms.length > 1) {
        const rememberedId = session.getRememberedFarmId();
        const rememberedFarm = farms.find((farm) => String(farm.id) === rememberedId);
        if (rememberedFarm) {
          session.setActiveFarm(rememberedFarm);
        }
      }
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
