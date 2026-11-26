import { makeAutoObservable, runInAction } from "mobx";
import type { FarmMemberResponse } from "../models/response/FarmMemberResponse";
import type { FarmRoleTypeResponse } from "../models/response/FarmRoleTypeResponse";
import { FarmApi } from "../api/FarmApi";
import { FarmRoleTypesApi } from "../api/FarmRoleTypesApi";
import { getApiErrorMessage } from "../api/apiErrors";
import { session } from "../stores/SessionStore";

export class FarmMembersViewModel {
  members: FarmMemberResponse[] = [];
  roleOptions: FarmRoleTypeResponse[] = [];
  isLoading = true;
  errorMessage: string | null = null;

  constructor() {
    makeAutoObservable(this, {}, { autoBind: true });
  }

  async initialize() {
    this.isLoading = true;
    this.errorMessage = null;
    try {
      const farmId = session.activeFarm?.id;
      if (farmId === undefined) {
        return;
      }
      const [members, roleOptions] = await Promise.all([
        FarmApi.getMembersAsync(Number(farmId)),
        FarmRoleTypesApi.getAllAsync(),
      ]);
      runInAction(() => {
        this.members = members;
        this.roleOptions = roleOptions;
      });
    } catch (error) {
      runInAction(() => {
        this.errorMessage = getApiErrorMessage(error, "Unable to load farm members. Please try again.");
      });
    } finally {
      runInAction(() => {
        this.isLoading = false;
      });
    }
  }

  async changeRole(member: FarmMemberResponse, farmRoleTypeId: number) {
    const farmId = session.activeFarm?.id;
    if (farmId === undefined || member.userId === undefined || farmRoleTypeId === Number(member.farmRoleTypeId)) {
      return;
    }
    this.errorMessage = null;
    try {
      await FarmApi.updateMemberRoleAsync(Number(farmId), Number(member.userId), { farmRoleTypeId });
      await this.initialize();
    } catch (error) {
      runInAction(() => {
        this.errorMessage = getApiErrorMessage(error, "Unable to change the role. Please try again.");
      });
    }
  }

  async remove(member: FarmMemberResponse) {
    const farmId = session.activeFarm?.id;
    if (farmId === undefined || member.userId === undefined) {
      return;
    }
    this.errorMessage = null;
    try {
      await FarmApi.removeMemberAsync(Number(farmId), Number(member.userId));
      await this.initialize();
    } catch (error) {
      runInAction(() => {
        this.errorMessage = getApiErrorMessage(error, "Unable to remove the member. Please try again.");
      });
    }
  }
}
