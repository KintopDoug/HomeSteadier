import axios from "axios";

export const httpClient = axios.create({
  baseURL: import.meta.env.VITE_API_URL ?? "http://localhost:5128",
  withCredentials: true,
});
