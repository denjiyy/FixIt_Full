#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "${ROOT_DIR}"

ENV_FILE="${1:-${FIXIT_ENV_FILE:-.env.production}}"
PROJECT_NAME="${FIXIT_RELEASE_PROJECT_NAME:-fixit-release-check}"
HOST_PORT="${FIXIT_RELEASE_HOST_PORT:-18080}"
BASE_URL="${FIXIT_RELEASE_BASE_URL:-http://localhost:${HOST_PORT}}"
CONCURRENCY="${CONCURRENCY:-20}"
DURATION_SECONDS="${DURATION_SECONDS:-90}"
KEEP_STACK_UP="${KEEP_STACK_UP:-false}"
RUN_BACKUP_GATE="${RUN_BACKUP_GATE:-true}"
BACKUP_OUTPUT_DIR="${BACKUP_OUTPUT_DIR:-./backups}"
REQUEST_HOST_HEADER="${FIXIT_RELEASE_REQUEST_HOST_HEADER:-}"
CURL_RESOLVE_ENTRY="${FIXIT_RELEASE_CURL_RESOLVE_ENTRY:-}"

if [[ ! -f "${ENV_FILE}" ]]; then
  echo "[ERROR] Environment file not found: ${ENV_FILE}"
  exit 1
fi

if ! command -v docker >/dev/null 2>&1; then
  echo "[ERROR] docker is required"
  exit 1
fi

if ! command -v dotnet >/dev/null 2>&1; then
  echo "[ERROR] dotnet is required"
  exit 1
fi

compose_cmd=(docker compose --project-name "${PROJECT_NAME}" --env-file "${ENV_FILE}" -f docker-compose.yml -f docker-compose.prod.yml)
export APP_HOST_PORT="${HOST_PORT}"

trim() {
  local value="$1"
  value="${value#"${value%%[![:space:]]*}"}"
  value="${value%"${value##*[![:space:]]}"}"
  printf "%s" "${value}"
}

get_env_file_var() {
  local key="$1"
  local line
  line="$(grep -E "^${key}=" "${ENV_FILE}" | tail -n1 || true)"
  if [[ -z "${line}" ]]; then
    return 1
  fi

  local value="${line#*=}"
  value="$(trim "${value%$'\r'}")"
  if [[ "${value}" =~ ^\".*\"$ ]]; then
    value="${value:1:${#value}-2}"
  elif [[ "${value}" =~ ^\'.*\'$ ]]; then
    value="${value:1:${#value}-2}"
  fi
  printf "%s" "${value}"
}

extract_host() {
  local url="$1"
  local without_scheme="${url#*://}"
  local host_with_port="${without_scheme%%/*}"
  printf "%s" "${host_with_port%%:*}"
}

is_localhost_host() {
  local host
  host="$(printf "%s" "$1" | tr '[:upper:]' '[:lower:]')"
  [[ "${host}" == "localhost" || "${host}" == "127.0.0.1" || "${host}" == "::1" ]]
}

base_url_host="$(extract_host "${BASE_URL}")"
app_base_url="$(get_env_file_var "APP_BASE_URL" || true)"
app_base_url_host=""
if [[ -n "${app_base_url}" ]]; then
  app_base_url_host="$(extract_host "${app_base_url}")"
fi

if [[ -z "${REQUEST_HOST_HEADER}" ]] \
  && is_localhost_host "${base_url_host}" \
  && [[ -n "${app_base_url_host}" ]] \
  && ! is_localhost_host "${app_base_url_host}"; then
  REQUEST_HOST_HEADER="${app_base_url_host}"
fi

cleanup() {
  if [[ "$(printf "%s" "${KEEP_STACK_UP}" | tr '[:upper:]' '[:lower:]')" != "true" ]]; then
    "${compose_cmd[@]}" down --remove-orphans >/dev/null 2>&1 || true
  fi
}
trap cleanup EXIT

echo "==> Step 1/8: Preflight"
scripts/ops/preflight.sh "${ENV_FILE}"

echo "==> Step 2/8: Build"
dotnet build FixIt.sln -c Release

echo "==> Step 3/8: Test"
dotnet test FixIt.sln -c Release --no-build

echo "==> Step 4/8: Vulnerability audit"
vuln_report="$(mktemp /tmp/fixit-vuln-report.XXXXXX.txt)"
dotnet list FixIt.sln package --vulnerable --include-transitive | tee "${vuln_report}"
if grep -qi "has the following vulnerable packages" "${vuln_report}"; then
  echo "[ERROR] Vulnerable NuGet packages detected."
  exit 1
fi
rm -f "${vuln_report}"

echo "==> Step 5/8: Compose validation"
"${compose_cmd[@]}" config --quiet

echo "==> Step 6/8: Start stack"
"${compose_cmd[@]}" up -d --build --remove-orphans

wait_for_health() {
  local service="$1"
  local timeout_seconds="$2"
  local start_ts
  start_ts="$(date +%s)"

  while true; do
    local container_id
    container_id="$("${compose_cmd[@]}" ps -q "${service}" | head -n1)"
    if [[ -n "${container_id}" ]]; then
      local health_state
      health_state="$(docker inspect --format '{{if .State.Health}}{{.State.Health.Status}}{{else}}{{.State.Status}}{{end}}' "${container_id}" 2>/dev/null || true)"
      if [[ "${health_state}" == "healthy" || "${health_state}" == "running" ]]; then
        echo "[PASS] ${service} is ${health_state}"
        return 0
      fi
      if [[ "${health_state}" == "unhealthy" || "${health_state}" == "exited" || "${health_state}" == "dead" ]]; then
        echo "[ERROR] ${service} is ${health_state}"
        "${compose_cmd[@]}" logs --tail=200 "${service}" || true
        return 1
      fi
    fi

    local now_ts
    now_ts="$(date +%s)"
    if (( now_ts - start_ts > timeout_seconds )); then
      echo "[ERROR] Timed out waiting for ${service} health."
      "${compose_cmd[@]}" ps || true
      "${compose_cmd[@]}" logs --tail=200 "${service}" || true
      return 1
    fi

    sleep 2
  done
}

wait_for_health "mongodb" 180
wait_for_health "fixit-app" 240

echo "==> Step 7/8: Smoke and load gates"
REQUEST_HOST_HEADER="${REQUEST_HOST_HEADER}" CURL_RESOLVE_ENTRY="${CURL_RESOLVE_ENTRY}" scripts/ops/smoke.sh "${BASE_URL}"
REQUEST_HOST_HEADER="${REQUEST_HOST_HEADER}" CURL_RESOLVE_ENTRY="${CURL_RESOLVE_ENTRY}" \
  CONCURRENCY="${CONCURRENCY}" DURATION_SECONDS="${DURATION_SECONDS}" scripts/ops/load-lite.sh "${BASE_URL}"

echo "==> Step 8/8: Backup gate"
if [[ "$(printf "%s" "${RUN_BACKUP_GATE}" | tr '[:upper:]' '[:lower:]')" == "true" ]]; then
  FIXIT_ENV_FILE="${ENV_FILE}" scripts/ops/mongo-backup.sh "${BACKUP_OUTPUT_DIR}"
else
  echo "[INFO] Backup gate skipped (RUN_BACKUP_GATE=${RUN_BACKUP_GATE})"
fi

echo "Release gate PASSED."
