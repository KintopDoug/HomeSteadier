import axios from "axios";

export const httpClient = axios.create({
  // VITE_API_URL is injected by Aspire; the fallback covers running `npm run dev` on its
  // own. It points at the API's HTTPS endpoint (see Homesteadier.API launchSettings.json)
  // because the HTTP one immediately redirects to it, and a redirected preflight fails CORS.
  baseURL: import.meta.env.VITE_API_URL ?? "https://localhost:7131",
  withCredentials: true,
});
