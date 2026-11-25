import { makeAutoObservable, runInAction } from "mobx";
import type { FarmMemberResponse } from "../models/response/FarmMemberResponse";
import type { FarmRoleTypeResponse } from "../models/response/FarmRoleTypeResponse";
import { FarmApi } from "../api/FarmApi";
import { FarmRoleTypesApi } from "../api/FarmRoleTypesApi";
import { getApiErrorMessage } from "../api/apiErrors";
import { loadFarms } from "../services/loadFarms";
import { session } from "../stores/SessionStore";

export class FarmMembersViewModel {
  members: FarmMemberResponse[] = [];
  roleOptions: FarmRoleTypeResponse[] = [];
  isLoading = true;
  busyUserId: string | number | null = null;
  memberPendingRemoval: FarmMemberResponse | null = null;
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
    try {
      const [members, roleOptions] = await Promise.all([
        FarmApi.getMembersAsync(farmId),
        FarmRoleTypesApi.getAllAsync(),
      ]);
      runInAction(() => {
        this.members = members;
        this.roleOptions = roleOptions;
      });
    } catch (error) {
      this.setErrorMessage(getApiErrorMessage(error, "Unable to load farm members. Please try again."));
    } finally {
      runInAction(() => {
        this.isLoading = false;
      });
    }
  }

  setErrorMessage(message: string | null) {
    this.errorMessage = message;
  }

  requestRemoval(member: FarmMemberResponse | null) {
    this.memberPendingRemoval = member;
  }

  async changeRole(member: FarmMemberResponse, farmRoleTypeId: string) {
    const farmId = session.activeFarm?.id;
    if (farmId === undefined || member.userId === undefined) {
      return;
    }

    this.setErrorMessage(null);
    this.busyUserId = member.userId;
    try {
      await FarmApi.updateMemberRoleAsync(farmId, member.userId, { farmRoleTypeId });
      const role = this.roleOptions.find((r) => String(r.id) === farmRoleTypeId);
      runInAction(() => {
        member.farmRoleTypeId = farmRoleTypeId;
        member.roleName = role?.name ?? member.roleName;
      });
      // Demoting yourself changes what you're allowed to see on this very page.
      if (member.userId === session.user?.id) {
        await this.refreshOwnRole();
      }
    } catch (error) {
      this.setErrorMessage(getApiErrorMessage(error, "Unable to update the role. Please try again."));
    } finally {
      runInAction(() => {
        this.busyUserId = null;
      });
    }
  }

  async confirmRemoval() {
    const member = this.memberPendingRemoval;
    const farmId = session.activeFarm?.id;
    if (!member || farmId === undefined || member.userId === undefined) {
      return;
    }

    this.setErrorMessage(null);
    this.memberPendingRemoval = null;
    this.busyUserId = member.userId;
    try {
      await FarmApi.removeMemberAsync(farmId, member.userId);
      runInAction(() => {
        this.members = this.members.filter((m) => m.userId !== member.userId);
      });
    } catch (error) {
      this.setErrorMessage(getApiErrorMessage(error, "Unable to remove the member. Please try again."));
    } finally {
      runInAction(() => {
        this.busyUserId = null;
      });
    }
  }

  private async refreshOwnRole() {
    try {
      await loadFarms();
    } catch {
      // Non-fatal: the session's role is refreshed on the next load.
    }
  }
}
