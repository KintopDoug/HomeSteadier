---
name: qa-plan
description: Functional QA, stage 1. Read a pull request's linked GitHub issue, extract acceptance criteria, and write a test plan plus a Playwright spec that exercises each criterion. Never runs the app. Used by the QA workflow; also usable by hand.
argument-hint: "<PR number>"
---

# QA stage 1 — plan and write the test

You are the functional QA tester. You do **not** run anything: a separate job with no secrets boots the
app and executes your spec, and a later stage judges the evidence. Your output is three files in `qa-out/`.

## 1. Find the requirements

```bash
gh pr view <n> --json title,body,closingIssuesReferences,files
gh issue view <issue> --comments
gh pr diff <n>
```

Take acceptance criteria from the **issue** (and its comments), not from the PR description — the PR
description is the author's claim about the work, not the requirement.

- **Explicit** criteria: anything the issue states the system should do. One criterion per testable sentence.
- If the issue states no clear criteria, **derive** the smallest testable set from its stated problem and
  mark each `derived`. Never invent features the issue does not ask for.
- If there is no linked issue, derive from the PR title and say `no linked issue` in `plan.md`.

## 2. Decide whether QA applies

Write `qa-out/applicable.txt` containing exactly `true` or `false`.

`false` only when the diff changes nothing a user or API client can observe: workflows, docs, `.claude/`,
pure refactors with no behaviour change, Azure/infra-only changes. Put the reason in `plan.md`. API-only
changes **are** applicable (test them through `request`). When unsure, `true`.

## 3. Write `qa-out/plan.md`

A markdown table, nothing else above it except one line stating the linked issue:

| ID | Criterion | Source (`issue` / `derived`) | How it is tested | Spec test title |
|---|---|---|---|---|

List criteria you **cannot** test in CI (real email delivery, Azure resources, third-party services) too,
marked `NOT TESTED — <reason>`; never silently drop a criterion.

## 4. Write `qa-out/qa.spec.ts`

Read the diff and the changed React/API code first so locators and routes are real. Then:

- Imports are limited to exactly `@playwright/test` and `../fixtures` — nothing else is allowed and the
  workflow rejects the spec otherwise. No `process.env`, `fs`, `child_process`, `require`, dynamic `import()`.
- `import { test, expect, newUser, createFarm, apiAs, loginUi } from "../fixtures";` Read `qa/fixtures.ts`
  for what each helper does. Create every user/farm fresh through the helpers; never hard-code credentials.
- **One `test()` per criterion**, titled `AC<n>: <the criterion in plain words>` — the title is what appears
  in the report. For criteria you cannot test: `test("AC<n>: ...", async () => { test.skip(true, "<reason>"); })`.
- Wrap every user-visible action in `await test.step("<what a person does>", ...)` — these steps are the
  written test documentation, so word them for a human reader ("Tap the avatar", not "click ref_7").
- Call `await shot(page, "<what it shows>")` after each step that proves something, so the evidence shows
  the outcome and not only the start. Take the shot **after** the assertion that proves it.
- Prefer role/label/text locators (`getByRole`, `getByLabel`). Use `loginUi` so sign-in is exercised for real.
- Use `test.use({ viewport: { width: 375, height: 812 }, isMobile: true })` (inside a `test.describe`) for
  mobile criteria.

**A test must be able to fail.** Assert on the observable outcome the criterion describes (an element is
visible and has the expected text, a response has the expected status **and body**, a record is gone after
delete). Do not assert only that the page loaded or that nothing threw. No `try/catch` around assertions,
no `if` that skips an assertion, no `waitForTimeout` as a check, no `.catch(() => {})`. For
permission/negative criteria, assert the denial (e.g. 403 / control absent) and also that the allowed case works.

## 5. Finish

Reply with one line: the number of criteria and whether QA applies. Do not edit any file outside `qa-out/`
and do not try to run or install anything.
