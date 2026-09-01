import { makeAutoObservable, runInAction } from "mobx";
import { FarmApi } from "../api/FarmApi";
import { getApiErrorMessage } from "../api/apiErrors";
import { geocodeAddress } from "../services/geocoding";
import { session } from "../stores/SessionStore";

export interface CreateFarmFormValues {
  name: string;
  addressLine: string;
  city: string;
  state: string;
  postalCode: string;
  country: string;
}

// Nominatim's public instance is rate-limited to ~1 req/sec, so geocoding waits this long after
// the last address-field edit before firing, instead of geocoding on every keystroke/blur.
const GEOCODE_DEBOUNCE_MS = 600;

export class CreateFarmViewModel {
  name = "";
  addressLine = "";
  city = "";
  state = "";
  postalCode = "";
  country = "";
  errorMessage: string | null = null;

  isGeocoding = false;
  geocodeError: string | null = null;
  resolvedLatitude: number | null = null;
  resolvedLongitude: number | null = null;
  resolvedDisplayName: string | null = null;

  private geocodeDebounceHandle: ReturnType<typeof setTimeout> | null = null;
  // Guards against an earlier, slower geocode request overwriting the result of a later one.
  private geocodeRequestId = 0;

  constructor() {
    makeAutoObservable(this, {}, { autoBind: true });
  }

  get values(): CreateFarmFormValues {
    return {
      name: this.name,
      addressLine: this.addressLine,
      city: this.city,
      state: this.state,
      postalCode: this.postalCode,
      country: this.country,
    };
  }

  private get addressQuery(): string {
    return [this.addressLine, this.city, this.state, this.postalCode, this.country]
      .filter((part) => part.trim().length > 0)
      .join(", ");
  }

  private clearResolvedLocation() {
    this.resolvedLatitude = null;
    this.resolvedLongitude = null;
    this.resolvedDisplayName = null;
    this.geocodeError = null;
  }

  setName(value: string) {
    this.name = value;
  }

  setAddressLine(value: string) {
    this.addressLine = value;
    this.onAddressFieldChanged();
  }

  setCity(value: string) {
    this.city = value;
    this.onAddressFieldChanged();
  }

  setState(value: string) {
    this.state = value;
    this.onAddressFieldChanged();
  }

  setPostalCode(value: string) {
    this.postalCode = value;
    this.onAddressFieldChanged();
  }

  setCountry(value: string) {
    this.country = value;
    this.onAddressFieldChanged();
  }

  setErrorMessage(message: string | null) {
    this.errorMessage = message;
  }

  private onAddressFieldChanged() {
    this.clearResolvedLocation();

    if (this.geocodeDebounceHandle) {
      clearTimeout(this.geocodeDebounceHandle);
    }

    const query = this.addressQuery;
    if (!query) {
      this.isGeocoding = false;
      return;
    }

    this.isGeocoding = true;
    this.geocodeDebounceHandle = setTimeout(() => {
      void this.runGeocode(query);
    }, GEOCODE_DEBOUNCE_MS);
  }

  private async runGeocode(query: string) {
    const requestId = ++this.geocodeRequestId;

    try {
      const result = await geocodeAddress(query);
      if (requestId !== this.geocodeRequestId) {
        return; // A newer edit superseded this request.
      }

      runInAction(() => {
        if (!result) {
          this.geocodeError = "Couldn't find that address. Try adding more detail (city, state, postal code).";
          return;
        }

        this.resolvedLatitude = result.latitude;
        this.resolvedLongitude = result.longitude;
        this.resolvedDisplayName = result.displayName;
      });
    } catch {
      if (requestId === this.geocodeRequestId) {
        runInAction(() => {
          this.geocodeError = "Unable to look up that address right now. Please try again.";
        });
      }
    } finally {
      if (requestId === this.geocodeRequestId) {
        runInAction(() => {
          this.isGeocoding = false;
        });
      }
    }
  }

  async submit(values: CreateFarmFormValues) {
    this.setErrorMessage(null);

    if (this.resolvedLatitude === null || this.resolvedLongitude === null) {
      this.setErrorMessage("Please enter an address that resolves to a location before creating the farm.");
      return;
    }

    try {
      const farm = await FarmApi.createAsync({
        name: values.name,
        addressLine: values.addressLine || undefined,
        city: values.city || undefined,
        state: values.state || undefined,
        postalCode: values.postalCode || undefined,
        country: values.country || undefined,
        latitude: this.resolvedLatitude,
        longitude: this.resolvedLongitude,
      });
      session.setFarms([...session.farms, farm]);
      session.setActiveFarm(farm);
    } catch (error) {
      this.setErrorMessage(getApiErrorMessage(error, "Unable to create farm. Please try again."));
    }
  }
}
