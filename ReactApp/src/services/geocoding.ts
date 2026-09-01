export interface GeocodeResult {
  latitude: number;
  longitude: number;
  displayName: string;
}

const NOMINATIM_SEARCH_URL = "https://nominatim.openstreetmap.org/search";

/**
 * Nominatim's usage policy (https://operations.osmfoundation.org/policies/nominatim/) asks for
 * an identifying User-Agent or Referer. Browsers block scripts from setting User-Agent, but
 * they always send Referer, which satisfies the policy for a browser-based caller like this one.
 * The public instance is rate-limited to ~1 req/sec, so callers must debounce (see
 * CreateFarmViewModel's address-change handling) rather than calling this on every keystroke.
 */
export async function geocodeAddress(query: string): Promise<GeocodeResult | null> {
  const url = `${NOMINATIM_SEARCH_URL}?format=json&limit=1&q=${encodeURIComponent(query)}`;
  const response = await fetch(url, {
    headers: { Accept: "application/json" },
  });

  if (!response.ok) {
    throw new Error(`Geocoding request failed with status ${response.status}`);
  }

  const results = (await response.json()) as Array<{ lat: string; lon: string; display_name: string }>;
  const [first] = results;
  if (!first) {
    return null;
  }

  return {
    latitude: Number(first.lat),
    longitude: Number(first.lon),
    displayName: first.display_name,
  };
}
