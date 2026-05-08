#!/usr/bin/env bash
set -euo pipefail

BASE_URL="${1:-http://localhost:8080}"
CURL_TIMEOUT_SECONDS="${CURL_TIMEOUT_SECONDS:-10}"

echo "Running smoke checks against: ${BASE_URL}"

check_endpoint() {
  local path="$1"
  shift
  local accepted_statuses=("$@")
  local body_file
  body_file="$(mktemp)"

  local status_code
  status_code="$(curl --silent --show-error --max-time "${CURL_TIMEOUT_SECONDS}" -o "${body_file}" -w "%{http_code}" "${BASE_URL}${path}" || true)"

  local matched="false"
  for expected in "${accepted_statuses[@]}"; do
    if [[ "${status_code}" == "${expected}" ]]; then
      matched="true"
      break
    fi
  done

  if [[ "${matched}" != "true" ]]; then
    echo "[FAIL] ${path} returned ${status_code}, expected one of: ${accepted_statuses[*]}"
    cat "${body_file}"
    rm -f "${body_file}"
    exit 1
  fi

  if [[ "${path}" == "/health" || "${path}" == "/health/live" || "${path}" == "/health/ready" ]]; then
    local compact
    compact="$(tr -d '[:space:]' < "${body_file}")"
    if ! grep -q '"status":"healthy"' <<< "${compact}"; then
      echo "[FAIL] ${path} did not report healthy status"
      cat "${body_file}"
      rm -f "${body_file}"
      exit 1
    fi
  fi

  echo "[PASS] ${path} -> ${status_code}"
  rm -f "${body_file}"
}

check_endpoint "/health/live" 200
check_endpoint "/health/ready" 200
check_endpoint "/health/detailed" 200
check_endpoint "/" 200 302

echo "Smoke checks passed."
