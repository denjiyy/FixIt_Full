#!/usr/bin/env bash
set -euo pipefail

BASE_URL="${1:-http://localhost:8080}"
CURL_TIMEOUT_SECONDS="${CURL_TIMEOUT_SECONDS:-10}"
REQUEST_HOST_HEADER="${REQUEST_HOST_HEADER:-}"
CURL_RESOLVE_ENTRY="${CURL_RESOLVE_ENTRY:-}"

echo "Running smoke checks against: ${BASE_URL}"

check_endpoint() {
  local path="$1"
  shift
  local accepted_statuses=("$@")
  local body_file
  body_file="$(mktemp)"

  local status_code
  local curl_args=(
    --silent
    --show-error
    --max-time "${CURL_TIMEOUT_SECONDS}"
    -o "${body_file}"
    -w "%{http_code}"
  )
  if [[ -n "${REQUEST_HOST_HEADER}" ]]; then
    curl_args+=(-H "Host: ${REQUEST_HOST_HEADER}")
  fi
  if [[ -n "${CURL_RESOLVE_ENTRY}" ]]; then
    curl_args+=(--resolve "${CURL_RESOLVE_ENTRY}")
  fi

  status_code="$(curl "${curl_args[@]}" "${BASE_URL}${path}" || true)"

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
