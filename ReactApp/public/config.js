// Runtime application configuration, loaded before the app bundle (see index.html).
//
// In a deployed container the entrypoint overwrites this file with the real API URL from the
// VITE_API_URL environment variable (see ReactApp/Dockerfile) — this is how the SPA learns
// the API's address, which isn't known when the static bundle is built. Locally it stays
// empty and the app falls back to import.meta.env.VITE_API_URL / the dev default.
window.__APP_CONFIG__ = { apiUrl: "" };
