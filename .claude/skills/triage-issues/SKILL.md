---
name: triage-issues
description: Review all open GitHub issues, assign a severity/priority label to each, map dependencies between open issues, and produce a ranked work queue. Use when asked to triage, prioritize, or decide what to work on next.
---

# Triage open issues

Goal: turn the open issue list into a ranked, dependency-aware work queue and record the result on GitHub so it survives this session. **Do not write code or open PRs here** — that is `/work-issue`.

## 1. Gather

```bash
gh issue list --state open --limit 200 --json number,title,body,labels,assignees,createdAt,comments
gh pr list --state open --json number,title,headRefName,body   # work already in flight
```

For each issue read the body and comments (`gh issue view <n> --comments`). Where an issue names code, skim the relevant files so the severity call is grounded in the repo, not the title. Skip issues that already have an open PR linked (`Closes #n`) — report them as "in flight".

## 2. Assign priority

First ensure the supporting labels exist (`gh label create` if missing): `blocked`, `needs-human`, and `claude-ready` (the human-applied trigger that sends an issue to the GitHub-hosted agent — never apply it yourself).

Use exactly these priority labels (create any that are missing):

| Label | Meaning |
|---|---|
| `priority: P0` | Security hole, data loss/corruption, production down, deploy broken |
| `priority: P1` | Core flow broken (auth, signup, farms) for many users, no workaround |
| `priority: P2` | Real bug with a workaround, or a feature other work depends on |
| `priority: P3` | Polish, cleanup, nice-to-have, tech debt with no user impact |

Judge by **impact × likelihood**, then bump up one level if the issue blocks other open issues. Existing `bug` outranks `enhancement` at equal impact. This is a public repo deployed to Azure: anything touching auth, tokens, secrets, CORS or the DB is at least P2.

Also assign a size estimate in your report only (S/M/L) — do not label it.

## 3. Map dependencies

An issue B *depends on* A when B cannot be finished or verified until A lands (shared migration, shared model/endpoint, B edits code A is about to rewrite). Sources, in order of trust:

1. GitHub's native relationship, if present: `gh api repos/{owner}/{repo}/issues/<n>/dependencies/blocked_by`
2. "Blocked by #n" / "Depends on #n" text in the body or comments.
3. Your own reading of the code. Mark these as **inferred** and say why.

Record real dependencies on GitHub: use the native API (`gh api -X POST repos/{owner}/{repo}/issues/<n>/dependencies/blocked_by -f issue_id=<A's numeric id>`) when available, otherwise add one comment `Blocked by #A (reason)`. Add the `blocked` label to blocked issues. Never record an *inferred* dependency without saying it is inferred.

Check for cycles; if you find one, flag it for the user instead of picking a side.

## 4. Apply and report

- Apply the priority labels (replace any existing `priority:` label on that issue).
- Add `needs-human` to any issue whose fix involves: DB migrations, auth/token/secret handling, Azure infra or `.github/workflows`, or where the issue is ambiguous enough that you would be guessing at the requirement. Add a comment saying which.
- Print a table ordered by **ready first, then priority**: `# | title | priority | size | blocked by | needs-human | notes`.
- Finish with **"Next up"**: the single highest-priority issue that is unblocked and not `needs-human` — this is what `/work-issue` will pick.

Ask before changing anything beyond labels and the comments described above (never close, reassign, or edit issue bodies).
