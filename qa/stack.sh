#!/usr/bin/env bash
# Run the real HomeSteadier stack against a Postgres the caller already started, and poke it.
#
# One implementation shared by every place that needs a live API: qa.yml (QA run), claude-fix.yml (so the
# fixer can run `packages gen`), and the work-issue agent (so it can verify its changes against a database).
# Local dev is unchanged (Aspire starts Postgres + API + web); this mirrors it for CI, with the same image.
#
#   bash qa/stack.sh start-api          build + start the API in the background, wait until /health is OK
#   bash qa/stack.sh restart-api        stop, then rebuild and start (after you change API code)
#   bash qa/stack.sh stop-api
#   bash qa/stack.sh start-web          install + start the Vite dev server (used by QA)
#   bash qa/stack.sh signup             create a fresh user; prints JSON {email,password,token} and saves the token
#   bash qa/stack.sh call GET /api/Farm                      uses the saved token (the last signup)
#   bash qa/stack.sh call POST /api/Farm '{"name":"x","latitude":1,"longitude":2}'
#   bash qa/stack.sh call --token <jwt> GET /api/Farm        a different user (e.g. to test a 403)
#   bash qa/stack.sh cli database update | dotnet gen | packages gen    the codegen CLI, pointed at this database
#
# No command needs an environment-variable prefix, on purpose: the agent's tool allowlist matches commands that
# START with `bash qa/stack.sh`, and an `API_TOKEN=... bash ...` form would not match it.
#
# The API applies the DbUp migrations and seeds on startup, so a fresh database needs nothing else. The
# connection comes from appsettings.shared.json (localhost:5432, db homesteadier-db, user homesteadier_admin)
# plus POSTGRES_PASSWORD, which defaults to the throwaway value the CI service container uses.
#
# `call` only talks to a local API (it refuses any API_URL that is not http://localhost[:port] or
# http://127.0.0.1[:port]); the host is fixed and only the path is yours. That keeps the agent's allowlist narrow:
# it never needs raw curl. (The allowlist still permits other commands such as dotnet, so this is a guard rail
# against mistakes and sloppy prompts, not a sandbox.)
#
# Environment (all optional):
#   STACK_ROOT        directory containing Homesteadier.API and ReactApp   (default: .)
#   STACK_LOG_DIR     api.log / web.log / api.pid / token       (default: ${RUNNER_TEMP:-/tmp}/agent, the same
#                     place for every caller, so `restart-api` finds what `start-api` wrote)
#   STACK_NOTES_FILE  if set, failures are appended here too (QA reads it)
#   API_URL, WEB_URL  default http://localhost:5128 and http://localhost:5173
#   STACK_API_TRIES, STACK_WEB_TRIES   health-check attempts, 2s apart (defaults 90 and 60)
set -uo pipefail

API_URL="${API_URL:-http://localhost:5128}"
WEB_URL="${WEB_URL:-http://localhost:5173}"
ROOT="${STACK_ROOT:-.}"
LOG_DIR="${STACK_LOG_DIR:-${RUNNER_TEMP:-/tmp}/agent}"
NOTES="${STACK_NOTES_FILE:-}"
API_TRIES="${STACK_API_TRIES:-90}"
WEB_TRIES="${STACK_WEB_TRIES:-60}"

note() { if [ -n "$NOTES" ]; then tee -a "$NOTES"; else cat; fi; }

# Print a message plus the tail of a log, record it for QA, and fail.
fail() { # message, logfile
  { echo "$1"; [ -n "${2:-}" ] && [ -f "$2" ] && { echo "Last log lines:"; tail -40 "$2"; }; } | note
  exit 1
}

# Poll a URL until it answers. If a pid is given and that process has exited, stop waiting at once: a crashed
# API (bad password, failed migration) will never become healthy, and waiting out the timeout wastes minutes.
wait_for() { # url, attempts, [pid]
  local i
  for i in $(seq 1 "$2"); do
    curl -sf "$1" > /dev/null 2>&1 && return 0
    if [ -n "${3:-}" ] && ! kill -0 "$3" 2> /dev/null; then return 2; fi
    sleep 2
  done
  return 1
}

stop_api() {
  local pid_file="$LOG_DIR/api.pid"
  if [ -f "$pid_file" ]; then
    kill "$(cat "$pid_file")" 2> /dev/null || true
    rm -f "$pid_file"
  fi
  # `dotnet run` starts the app as a child process; make sure that child is gone too.
  # Match the built app (…/bin/Release/…/Homesteadier.API), not any command that merely mentions the project.
  command -v pkill > /dev/null 2>&1 && pkill -f "bin/Release/.*Homesteadier\.API" 2> /dev/null || true
  local i
  for i in 1 2 3 4 5 6 7 8 9 10; do
    curl -sf "$API_URL/health" > /dev/null 2>&1 || return 0
    sleep 1
  done
  return 0
}

start_api() {
  mkdir -p "$LOG_DIR"
  # Same settings qa.yml has always used. The key is a throwaway for an ephemeral stack, not a secret.
  export ASPNETCORE_ENVIRONMENT=Development
  export ASPNETCORE_URLS="$API_URL"
  export POSTGRES_PASSWORD="${POSTGRES_PASSWORD:-qa-ci-throwaway}"
  export JWT_SIGNING_KEY="${JWT_SIGNING_KEY:-qa-ci-throwaway-signing-key-0123456789abcdef}"
  # Plain-http localhost cannot carry a Secure/SameSite=None cookie in every browser build.
  export RefreshCookie__SameSite="${RefreshCookie__SameSite:-Lax}"
  export RefreshCookie__Secure="${RefreshCookie__Secure:-false}"

  stop_api
  dotnet build "$ROOT/Homesteadier.API" -c Release --nologo -v q || fail "API build failed."
  nohup dotnet run --no-build -c Release --no-launch-profile --project "$ROOT/Homesteadier.API" > "$LOG_DIR/api.log" 2>&1 &
  local pid=$!
  echo "$pid" > "$LOG_DIR/api.pid"
  wait_for "$API_URL/health" "$API_TRIES" "$pid"
  case $? in
    0) ;;
    2) fail "API process exited during startup (bad database password or failed migration?)." "$LOG_DIR/api.log" ;;
    *) fail "API never became healthy (migrations/startup)." "$LOG_DIR/api.log" ;;
  esac
  echo "API is up at $API_URL (log: $LOG_DIR/api.log)"
}

start_web() {
  mkdir -p "$LOG_DIR"
  export VITE_API_URL="$API_URL"
  npm --prefix "$ROOT/ReactApp" install --no-audit --no-fund || fail "npm install failed."
  nohup npm --prefix "$ROOT/ReactApp" run dev -- --port 5173 --strictPort > "$LOG_DIR/web.log" 2>&1 &
  local pid=$!
  wait_for "$WEB_URL/" "$WEB_TRIES" "$pid" || fail "Web never came up." "$LOG_DIR/web.log"
  echo "Web is up at $WEB_URL"
}

# Extract a string field from flat JSON without needing jq.
json_field() { sed -n "s/.*\"$1\":\"\([^\"]*\)\".*/\1/p" | head -1; }

signup() {
  local id email password out status body
  id="$(date +%s)$RANDOM"
  email="agent-$id@example.com"
  password="Agent!${id}Aa1"
  out=$(curl -sS -w '\n%{http_code}' -X POST "$API_URL/api/Auth/SignUp" -H 'Content-Type: application/json' \
    -d "{\"email\":\"$email\",\"password\":\"$password\",\"firstName\":\"Agent\",\"lastName\":\"Test\"}") \
    || fail "SignUp request failed (is the API running? try: bash qa/stack.sh start-api)."
  status=$(printf '%s' "$out" | tail -1)
  body=$(printf '%s' "$out" | sed '$d')
  [ "$status" = "200" ] || [ "$status" = "201" ] || fail "SignUp returned HTTP $status: $body"
  local token
  token="$(printf '%s' "$body" | json_field token)"
  mkdir -p "$LOG_DIR"
  ( umask 077; printf '%s' "$token" > "$LOG_DIR/token" )
  printf '{"email":"%s","password":"%s","token":"%s"}\n' "$email" "$password" "$token"
}

call() { # [--token JWT] METHOD /path [json-body]
  local token="${API_TOKEN:-}"
  if [ "${1:-}" = "--token" ]; then token="${2:-}"; shift 2; fi
  [ -n "$token" ] || { [ -f "$LOG_DIR/token" ] && token="$(cat "$LOG_DIR/token")"; }
  local method="${1:-}" path="${2:-}" data="${3:-}" tmp code
  [[ "$API_URL" =~ ^http://(localhost|127\.0\.0\.1)(:[0-9]+)?$ ]] \
    || { echo "call only talks to a local API; refusing API_URL=$API_URL" >&2; exit 2; }
  case "$method" in GET | POST | PUT | PATCH | DELETE) ;; *) echo "usage: call [--token JWT] GET|POST|PUT|PATCH|DELETE /path [json]" >&2; exit 2 ;; esac
  case "$path" in /*) ;; *) echo "path must start with /" >&2; exit 2 ;; esac
  tmp="$(mktemp)"
  local args=(-sS -o "$tmp" -w '%{http_code}' -X "$method")
  [ -n "$token" ] && args+=(-H "Authorization: Bearer $token")
  [ -n "$data" ] && args+=(-H 'Content-Type: application/json' -d "$data")
  code=$(curl "${args[@]}" "$API_URL$path") || { rm -f "$tmp"; echo "request failed (is the API running? try: bash qa/stack.sh start-api)" >&2; exit 1; }
  echo "HTTP $code"
  cat "$tmp"; echo
  rm -f "$tmp"
}

# The codegen CLI against the same database, with the same default password as the API. Only the commands
# CLAUDE.md documents; the CLI also has an interactive REPL, which has no place in an unattended run.
cli() {
  case "${1:-} ${2:-}" in
    "database update" | "database gen" | "database seed" | "dotnet gen" | "packages gen") ;;
    *) echo "usage: cli database update|gen|seed  |  cli dotnet gen  |  cli packages gen" >&2; exit 2 ;;
  esac
  export POSTGRES_PASSWORD="${POSTGRES_PASSWORD:-qa-ci-throwaway}"
  dotnet run --project "$ROOT/HomeSteadier.CLI" -- "$@"
}

case "${1:-}" in
  start-api) start_api ;;
  restart-api) start_api ;; # start_api already stops a running instance first
  stop-api) stop_api; echo "API stopped" ;;
  start-web) start_web ;;
  signup) signup ;;
  call) shift; call "$@" ;;
  cli) shift; cli "$@" ;;
  *) sed -n '2,/^set -uo/p' "$0" | sed '$d' | sed 's/^# \{0,1\}//' >&2; exit 2 ;;
esac
