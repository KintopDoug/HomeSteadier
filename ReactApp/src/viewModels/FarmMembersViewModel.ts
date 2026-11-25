import { makeAutoObservable, runInAction } from "mobx";
import type { FarmMemberResponse } from "../models/response/FarmMemberResponse";
import type { FarmRoleTypeResponse } from "../models/response/FarmRoleTypeResponse";
import { FarmApi } from "../api/FarmApi";
import { FarmRoleTypesApi } from "../api/FarmRoleTypesApi";
import { getApiErrorMessage } from "../api/apiErrors";
import { session } from "../stores/SessionStore";

export class FarmMembersViewModel {
  members: FarmMemberResponse[] = [];
  roles: FarmRoleTypeResponse[] = [];
  isLoading = true;
  busyUserId: string | number | null = null;
  errorMessage: string | null = null;

  constructor() {
    makeAutoObservable(this, {}, { autoBind: true });
  }

  async initialize() {
    const farmId = session.activeFarm?.id;
    if (farmId === undefined) {
      this.isLoading = false;
      return;
    }

    this.isLoading = true;
    this.errorMessage = null;
    try {
      const [members, roles] = await Promise.all([
        FarmApi.getMembersAsync(farmId),
        FarmRoleTypesApi.getAllAsync(),
      ]);
      runInAction(() => {
        this.members = members;
        this.roles = roles;
      });
    } catch (error) {
      this.setErrorMessage(getApiErrorMessage(error, "Unable to load farm members. Please try again."));
    } finally {
      runInAction(() => {
        this.isLoading = false;
      });
    }
  }

  isCurrentUser(member: FarmMemberResponse) {
    return session.user?.id !== undefined && String(session.user.id) === String(member.userId);
  }

  setErrorMessage(message: string | null) {
    this.errorMessage = message;
  }

  async changeRole(member: FarmMemberResponse, farmRoleTypeId: string) {
    const farmId = session.activeFarm?.id;
    if (farmId === undefined || member.userId === undefined) {
      return;
    }

    this.errorMessage = null;
    this.busyUserId = member.userId;
    try {
      await FarmApi.updateMemberRoleAsync(farmId, member.userId, { farmRoleTypeId: Number(farmRoleTypeId) });
      await this.initialize();
    } catch (error) {
      this.setErrorMessage(getApiErrorMessage(error, "Unable to update the member's role. Please try again."));
    } finally {
      runInAction(() => {
        this.busyUserId = null;
      });
    }
  }

  async remove(member: FarmMemberResponse) {
    const farmId = session.activeFarm?.id;
    if (farmId === undefined || member.userId === undefined) {
      return;
    }

    this.errorMessage = null;
    this.busyUserId = member.userId;
    try {
      await FarmApi.removeMemberAsync(farmId, member.userId);
      await this.initialize();
    } catch (error) {
      this.setErrorMessage(getApiErrorMessage(error, "Unable to remove the member. Please try again."));
    } finally {
      runInAction(() => {
        this.busyUserId = null;
      });
    }
  }
}
