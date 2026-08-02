import axios from "axios";
import { session } from "../stores/SessionStore";

export const httpClient = axios.create({
  // VITE_API_URL is injected by Aspire; the fallback covers running `npm run dev` on its
  // own. It points at the API's HTTPS endpoint (see Homesteadier.API launchSettings.json)
  // because the HTTP one immediately redirects to it, and a redirected preflight fails CORS.
  baseURL: import.meta.env.VITE_API_URL ?? "https://localhost:7131",
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
