// Deterministic QA report: turns Playwright's results.json into markdown + a flat evidence/ folder.
// No LLM involved, so a "passed" here can only come from a test that actually ran and passed.
//
//   node render-report.mjs --results <results.json> --test-results <dir> --plan <plan.md>
//                          --out <dir> --base-url <url prefix evidence will be served from>
import fs from "node:fs";
import path from "node:path";

const args = Object.fromEntries(
  process.argv.slice(2).reduce((acc, a, i, all) => (a.startsWith("--") ? [...acc, [a.slice(2), all[i + 1]]] : acc), []),
);
const out = args.out ?? "published";
const baseUrl = (args["base-url"] ?? "").replace(/\/$/, "");
const MAX_FILE = 15 * 1024 * 1024; // per file
const MAX_TOTAL = 60 * 1024 * 1024; // per run
const ALLOWED = new Map([
  [".png", "image/png"],
  [".webm", "video/webm"],
]);

fs.mkdirSync(path.join(out, "evidence"), { recursive: true });
let total = 0;

function publishFile(srcPath, destName) {
  const ext = path.extname(srcPath).toLowerCase();
  if (!ALLOWED.has(ext)) return null;
  const resolved = resolveAttachment(srcPath);
  if (!resolved) return null;
  const size = fs.statSync(resolved).size;
  if (size > MAX_FILE || total + size > MAX_TOTAL) return null;
  total += size;
  const safe = destName.replace(/[^a-zA-Z0-9._-]+/g, "-");
  fs.copyFileSync(resolved, path.join(out, "evidence", safe));
  return { name: safe, url: `${baseUrl}/evidence/${safe}`, size };
}

// Attachment paths in results.json are absolute on the machine that ran the tests; re-root them
// under --test-results (the downloaded artifact) from the "test-results/" segment onward.
function resolveAttachment(p) {
  if (fs.existsSync(p) && args["test-results"] === undefined) return p;
  const rel = p.split(/test-results[\\/]/).pop();
  const candidate = path.join(args["test-results"] ?? ".", rel);
  const resolvedRoot = path.resolve(args["test-results"] ?? ".");
  const resolved = path.resolve(candidate);
  if (!resolved.startsWith(resolvedRoot)) return null; // no path traversal
  return fs.existsSync(resolved) ? resolved : null;
}

const results = JSON.parse(fs.readFileSync(args.results, "utf8"));
const specs = [];
(function walk(suite) {
  for (const s of suite.specs ?? []) specs.push(s);
  for (const child of suite.suites ?? []) walk(child);
})({ suites: results.suites });

specs.sort((a, b) => a.title.localeCompare(b.title, undefined, { numeric: true }));

const icon = { passed: "✅ PASS", failed: "❌ FAIL", timedOut: "❌ FAIL (timeout)", skipped: "⏭️ SKIPPED", interrupted: "⚠️ INTERRUPTED" };
const lines = [];
let passed = 0, failed = 0, skipped = 0;

function renderSteps(steps, depth = 0) {
  for (const step of steps ?? []) {
    if (step.category && step.category !== "test.step") continue; // hide fixture/expect noise
    lines.push(`${"  ".repeat(depth)}${step.error ? "❌" : "-"} ${depth ? "" : "- "}${step.title}${step.error ? ` — ${firstLine(step.error.message)}` : ""}`.replace(/^(\s*)- - /, "$1- "));
    renderSteps(step.steps, depth + 1);
  }
}
const firstLine = (s = "") => stripAnsi(s).split("\n").find((l) => l.trim()) ?? "";
const stripAnsi = (s) => s.replace(/\u001b\[[0-9;]*m/g, "");

specs.forEach((spec, i) => {
  const test = spec.tests?.[0];
  const result = test?.results?.at(-1);
  const status = result?.status ?? "skipped";
  if (status === "passed") passed++; else if (status === "skipped") skipped++; else failed++;

  lines.push(`### ${spec.title}`, "", `**Result:** ${icon[status] ?? status}${result ? ` · ${(result.duration / 1000).toFixed(1)}s` : ""}`, "");
  if (status === "skipped") {
    const reason = test?.annotations?.find((a) => a.type === "skip" || a.type === "fixme")?.description;
    lines.push(`Not tested${reason ? `: ${reason}` : ""}`, "");
    return;
  }
  lines.push("**Steps taken:**", "");
  renderSteps(result?.steps);
  lines.push("");
  if (result?.error) lines.push("**Failure:**", "", "```", stripAnsi(result.error.message).split("\n").slice(0, 12).join("\n"), "```", "");

  const shots = [], videos = [];
  for (const att of result?.attachments ?? []) {
    if (!att.path) continue;
    const isVideo = att.contentType === "video/webm";
    const file = publishFile(att.path, `t${i + 1}-${isVideo ? "video" : att.name}${path.extname(att.path)}`);
    if (!file) continue;
    (isVideo ? videos : shots).push({ att, file });
  }
  // The auto end-of-test screenshot is redundant when the spec attached numbered ones.
  const explicit = shots.filter((s) => /^\d{2}-/.test(s.att.name));
  for (const { att, file } of explicit.length ? explicit : shots) lines.push(`**${att.name.replace(/^\d+-/, "")}**`, "", `![${att.name}](${file.url})`, "");
  for (const { file } of videos) lines.push(`🎥 [Video recording](${file.url}) (${(file.size / 1024 / 1024).toFixed(1)} MB, .webm)`, "");
});

const plan = args.plan && fs.existsSync(args.plan) ? fs.readFileSync(args.plan, "utf8").trim() : "_(no plan file)_";
const summary = `**${passed} passed · ${failed} failed · ${skipped} not tested** (of ${specs.length} acceptance criteria)`;
fs.writeFileSync(
  path.join(out, "report.md"),
  [`### Test summary`, "", summary, "", `<details><summary>Acceptance criteria and test plan</summary>`, "", plan, "", `</details>`, "", `## Test steps and evidence`, "", ...lines].join("\n"),
);
fs.writeFileSync(path.join(out, "counts.json"), JSON.stringify({ passed, failed, skipped, total: specs.length }));
console.log(summary);
