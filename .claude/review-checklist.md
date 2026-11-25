# Review checklist

Shared by the pre-PR reviewer (`.claude/agents/pre-pr-reviewer.md`) and the post-PR reviewer
(`.claude/skills/pr-review/SKILL.md`). Review the **diff against `origin/main`**, not the whole repo.

## Blocking (must be fixed before merge)

1. **Does it do what the issue asks?** Re-read the issue yourself; do not trust the PR description or
   commit message. Missing acceptance criteria, or behaviour beyond the issue, is a finding.
2. **Correctness bugs** in the changed lines: null/edge cases, wrong authorization (any endpoint that
   acts on a farm must check the caller's membership/role), off-by-one, unawaited async, broken
   routes, state not reset.
3. **Forbidden edits** (see CLAUDE.md): hand-edits to `HomesteadierDbContext.cs`,
   `HomeSteadier.Models/Database/*`, `ReactApp/src/models/**`, `ReactApp/src/api/*Api.tsx`, or
   `routeTree.gen.ts`; schema changes without a new numbered DbUp migration; edits to
   `.github/workflows/` or secret handling the issue did not ask for.
4. **Security and deploy risk**: secrets/tokens in code or logs, loosened CORS or auth, weakened
   password/token handling, migrations that are not idempotent or that lose data, anything that
   would break the Azure deploy on merge to `main`.
5. **Unverified claims**: the PR says something works or passes that the diff or CI does not support.
6. **No test for new or changed behaviour.** `Homesteadier.Tests` runs the real API against a real Postgres and CI
   runs it on every PR. New or changed API, auth, authorization or data behaviour needs a test there covering the
   success path and the denial path (401/403). A blocker for auth, authorization and data changes; a nit for other
   changes. A pure refactor, docs or config change needs none.

## Non-blocking (note briefly, do not pad)

- Repo conventions: React components are arrow functions in `observer(...)`; state in MobX view models;
  new repositories carry `[AutoRegister]`.
- Unrelated drive-by changes that should be a separate PR.
- Tests that cannot fail: asserting only that a call returned, a page loaded, or nothing threw; a test that passes
  with the change reverted. Tests must assert the status AND the outcome, and use their own unique data.

## Rules for findings

- Cite `file:line`. State the concrete failing scenario (input/state → wrong result), not a vague worry.
- Only report what you can point to in the diff. If unsure, say "unverified" and why.
- Severity: **blocker** or **nit**. No praise, no restating what the code does.
- It is fine, and common, to find nothing. Say so plainly.
