import { defineConfig, devices } from "@playwright/test";

// Trusted harness config. The spec under tests/qa.spec.ts is written per-PR by the QA planner agent;
// everything that decides *what counts as evidence* (video, screenshots, reporters) lives here.
export default defineConfig({
  testDir: "./tests",
  outputDir: "./test-results",
  timeout: 60_000,
  expect: { timeout: 10_000 },
  workers: 1,
  retries: 0,
  reporter: [["list"], ["json", { outputFile: "results.json" }]],
  use: {
    ...devices["Desktop Chrome"],
    baseURL: process.env.QA_WEB_URL ?? "http://localhost:5173",
    // CI installs Playwright's own Chromium; locally set QA_CHROME_CHANNEL=chrome to use installed Chrome.
    channel: process.env.QA_CHROME_CHANNEL || undefined,
    // QA_VIDEO=off only for local smoke tests on a machine without Playwright's ffmpeg; CI always records.
    video: process.env.QA_VIDEO === "off" ? "off" : "on",
    screenshot: "on",
    trace: "retain-on-failure",
  },
});
