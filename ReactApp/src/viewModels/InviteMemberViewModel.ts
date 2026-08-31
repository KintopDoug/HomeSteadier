import { makeAutoObservable, runInAction } from "mobx";
import type { FarmRoleTypeResponse } from "../models/response/FarmRoleTypeResponse";
import { FarmInvitationsApi } from "../api/FarmInvitationsApi";
import { FarmRoleTypesApi } from "../api/FarmRoleTypesApi";
import { getApiErrorMessage } from "../api/apiErrors";
import { session } from "../stores/SessionStore";

export interface InviteMemberFormValues {
  email: string;
  farmRoleTypeId: string;
}

export class InviteMemberViewModel {
  email = "";
  farmRoleTypeId = "";
  roleOptions: FarmRoleTypeResponse[] = [];
  isLoadingRoles = true;
  errorMessage: string | null = null;
  successMessage: string | null = null;

  constructor() {
    makeAutoObservable(this, {}, { autoBind: true });
  }

  get values(): InviteMemberFormValues {
    return {
      email: this.email,
      farmRoleTypeId: this.farmRoleTypeId,
    };
  }

  async initialize() {
    this.isLoadingRoles = true;

    try {
      const roleOptions = await FarmRoleTypesApi.getAllAsync();
      runInAction(() => {
        this.roleOptions = roleOptions;
      });
    } catch (error) {
      runInAction(() => {
        this.errorMessage = getApiErrorMessage(error, "Unable to load farm roles. Please try again.");
      });
    } finally {
      runInAction(() => {
        this.isLoadingRoles = false;
      });
    }
  }

  setEmail(value: string) {
    this.email = value;
  }

  setFarmRoleTypeId(value: string) {
    this.farmRoleTypeId = value;
  }

  setErrorMessage(message: string | null) {
    this.errorMessage = message;
  }

  async submit(values: InviteMemberFormValues) {
    this.setErrorMessage(null);
    this.successMessage = null;

    const farmId = session.activeFarm?.id;
    if (farmId === undefined) {
      this.setErrorMessage("No active farm selected.");
      return;
    }

    try {
      await FarmInvitationsApi.createAsync({
        farmId,
        email: values.email,
        farmRoleTypeId: values.farmRoleTypeId,
      });
      runInAction(() => {
        this.successMessage = `Invitation sent to ${values.email}.`;
        this.email = "";
        this.farmRoleTypeId = "";
      });
    } catch (error) {
      this.setErrorMessage(getApiErrorMessage(error, "Unable to send invitation. Please try again."));
    }
  }
}
