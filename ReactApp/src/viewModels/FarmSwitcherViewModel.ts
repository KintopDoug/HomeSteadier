import { makeAutoObservable } from "mobx";
import type { FarmResponse } from "../models/response/FarmResponse";
import { session } from "../stores/SessionStore";

export class FarmSwitcherViewModel {
  menuAnchor: HTMLElement | null = null;

  constructor() {
    makeAutoObservable(this, {}, { autoBind: true });
  }

  openMenu(anchor: HTMLElement) {
    this.menuAnchor = anchor;
  }

  closeMenu() {
    this.menuAnchor = null;
  }

  selectFarm(farm: FarmResponse) {
    session.setActiveFarm(farm);
    this.closeMenu();
  }
}
