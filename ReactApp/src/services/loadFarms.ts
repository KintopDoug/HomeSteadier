import { FarmApi } from "../api/FarmApi";
import { session } from "../stores/SessionStore";

/**
 * Fetches the signed-in user's farms into the session and picks the active farm: the only
 * farm, or the remembered one when there are several. Lives in services/, not SessionStore,
 * because the store must not import API clients (httpClient imports the store — import cycle).
 * Shared by the session restore in routes/__root.tsx and HomeViewModel so the active farm
 * (and its role, which gates the invite page and header) is correct on any page after a
 * hard refresh, not only Home.
 */
export async function loadFarms(): Promise<void> {
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
}
