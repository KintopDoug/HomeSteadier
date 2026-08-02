import { isAxiosError } from "axios";

/**
 * Hand-written, like httpClient.ts — 'packages gen' only owns the *Api.tsx files here.
 *
 * Turns an axios failure into a message for the user. Rate-limited responses need calling out
 * explicitly: the API returns 429 with an empty body (see AuthRateLimiting on the server), so
 * without this the generic fallback would report throttling as "invalid credentials".
 */
export const getApiErrorMessage = (error: unknown, fallback: string): string => {
  if (!isAxiosError<{ message?: string }>(error)) {
    return fallback;
  }

  if (error.response?.status === 429) {
    const retryAfter = Number(error.response.headers["retry-after"]);
    return Number.isFinite(retryAfter) && retryAfter > 0
      ? `Too many attempts. Please try again in ${retryAfter} seconds.`
      : "Too many attempts. Please try again shortly.";
  }

  return error.response?.data?.message ?? fallback;
};
