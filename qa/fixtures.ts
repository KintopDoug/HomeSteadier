import { test as base, expect, type APIRequestContext, type Page } from "@playwright/test";

/**
 * Trusted helpers for the per-PR spec written by the QA planner. Specs may import ONLY from
 * "@playwright/test" and "../fixtures" (enforced in qa.yml before the spec runs).
 */

const API_URL = process.env.QA_API_URL ?? "http://localhost:5128";

export interface QaUser {
  email: string;
  password: string;
  firstName: string;
  lastName: string;
  token: string;
}

export interface QaFarm {
  id: number | string;
  name: string;
  roleName?: string;
}

const unique = () => `${Date.now().toString(36)}${Math.random().toString(36).slice(2, 7)}`;

/** Register a brand-new user through the real API. Always unique, so tests never collide. */
export async function newUser(request: APIRequestContext, label = "qa"): Promise<QaUser> {
  const id = unique();
  const user = {
    email: `${label}-${id}@example.com`,
    password: `Qa!${id}Aa1`,
    firstName: "Qa",
    lastName: label,
  };
  const response = await request.post(`${API_URL}/api/Auth/SignUp`, { data: user });
  expect(response.ok(), `SignUp failed: ${response.status()} ${await response.text()}`).toBeTruthy();
  const body = await response.json();
  return { ...user, token: body.token };
}

/** Create a farm owned (Admin) by `user` through the real API. */
export async function createFarm(
  request: APIRequestContext,
  user: QaUser,
  name = `Qa Farm ${unique()}`,
): Promise<QaFarm> {
  const response = await request.post(`${API_URL}/api/Farm`, {
    headers: { Authorization: `Bearer ${user.token}` },
    data: { name, latitude: 40.0, longitude: -75.0 },
  });
  expect(response.ok(), `Create farm failed: ${response.status()} ${await response.text()}`).toBeTruthy();
  return await response.json();
}

/** Authenticated API call as `user`, for asserting on backend state. */
export const apiAs = (request: APIRequestContext, user: QaUser) => ({
  get: (path: string) =>
    request.get(`${API_URL}${path}`, { headers: { Authorization: `Bearer ${user.token}` } }),
  send: (method: "POST" | "PUT" | "PATCH" | "DELETE", path: string, data?: unknown) =>
    request.fetch(`${API_URL}${path}`, {
      method,
      data,
      headers: { Authorization: `Bearer ${user.token}` },
    }),
});

/** Sign in through the real login form (this is itself exercised, not bypassed). */
export async function loginUi(page: Page, user: QaUser): Promise<void> {
  await page.goto("/login");
  await page.getByLabel(/^Email/).fill(user.email);
  await page.getByLabel(/^Password/).fill(user.password);
  await page.getByRole("button", { name: "Sign In" }).click();
  await expect(page).not.toHaveURL(/\/login/);
}

/**
 * A distinct client IP per test. The API's `auth` rate limit allows 10 requests/minute per client IP, so
 * a spec whose tests each sign up and sign in would otherwise share one bucket and hit 429. The API
 * honours a single X-Forwarded-For value (ForwardLimit=1), so giving each test its own address gives it
 * its own bucket. The limit itself is unchanged. Collisions within a minute are negligible (~16M addresses).
 */
const uniqueClientIp = () => {
  const n = () => Math.floor(Math.random() * 254) + 1;
  return `10.${n()}.${n()}.${n()}`;
};

export const test = base.extend<{
  /** Attach a numbered screenshot to the report: `await shot(page, "menu open")`. */
  shot: (page: Page, name: string) => Promise<void>;
}>({
  // extraHTTPHeaders is applied to the page's browser traffic AND to the `request` fixture, so both the
  // UI sign-in and API sign-ups of one test share that test's bucket and no other test's.
  extraHTTPHeaders: async ({}, use) => {
    await use({ "X-Forwarded-For": uniqueClientIp() });
  },
  shot: async ({}, use, testInfo) => {
    let n = 0;
    await use(async (page, name) => {
      n += 1;
      const label = `${String(n).padStart(2, "0")}-${name}`;
      // Written to disk (not attached as inline bytes) so it lands in the evidence artifact.
      const file = testInfo.outputPath(`${label.replace(/[^a-zA-Z0-9]+/g, "-")}.png`);
      // animations:"disabled" fast-forwards CSS transitions, so e.g. a fading-in MUI menu is captured fully shown.
      await page.screenshot({ path: file, animations: "disabled" });
      await testInfo.attach(label, { path: file, contentType: "image/png" });
    });
  },
});

export { expect };
