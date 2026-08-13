import axios from "axios";
import { session } from "../stores/SessionStore";

declare global {
  interface Window {
    __APP_CONFIG__?: { apiUrl?: string };
  }
}

// API base URL, resolved in priority order:
//  1. window.__APP_CONFIG__.apiUrl — written into /config.js at container start from the
//     VITE_API_URL env var (see ReactApp/public/config.js + Dockerfile). This is how the
//     deployed SPA learns the API URL, which isn't known when the static bundle is built.
//  2. import.meta.env.VITE_API_URL — injected by Aspire when running `npm run dev` under AppHost.
//  3. The localhost HTTPS endpoint — for a bare `npm run dev` with no Aspire. HTTPS (not HTTP)
//     because the HTTP endpoint immediately redirects, and a redirected preflight fails CORS.
const runtimeApiUrl = window.__APP_CONFIG__?.apiUrl?.trim();

export const httpClient = axios.create({
  baseURL:
    (runtimeApiUrl ? runtimeApiUrl : undefined) ??
    import.meta.env.VITE_API_URL ??
    "https://localhost:7131",
  withCredentials: true,
});

// Attaches the in-memory access token (see SessionStore) to every outgoing request.
// Endpoints that don't require auth just ignore the header, so it's simplest to apply
// this globally rather than opting in per call.
httpClient.interceptors.request.use((config) => {
  if (session.accessToken) {
    config.headers.set("Authorization", `Bearer ${session.accessToken}`);
  }
  return config;
});
