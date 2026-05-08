#!/usr/bin/env bash
set -euo pipefail

BASE_URL="${1:-http://localhost:8080}"
DURATION_SECONDS="${DURATION_SECONDS:-60}"
CONCURRENCY="${CONCURRENCY:-20}"
REQUEST_TIMEOUT_SECONDS="${REQUEST_TIMEOUT_SECONDS:-8}"
MIN_SUCCESS_RATE_PERCENT="${MIN_SUCCESS_RATE_PERCENT:-99}"
MAX_P95_SECONDS="${MAX_P95_SECONDS:-1.50}"
REQUEST_HOST_HEADER="${REQUEST_HOST_HEADER:-}"
CURL_RESOLVE_ENTRY="${CURL_RESOLVE_ENTRY:-}"

TARGET_PATHS=(
  "/health/live"
  "/health/ready"
  "/health/detailed"
)

tmp_dir="$(mktemp -d)"
results_file="${tmp_dir}/results.log"
times_file="${tmp_dir}/times.log"
trap 'rm -rf "${tmp_dir}"' EXIT

echo "Running light load test against: ${BASE_URL}"
echo "Duration: ${DURATION_SECONDS}s | Concurrency: ${CONCURRENCY}"

end_ts=$((SECONDS + DURATION_SECONDS))

run_worker() {
  while (( SECONDS < end_ts )); do
    local path_index=$((RANDOM % ${#TARGET_PATHS[@]}))
    local path="${TARGET_PATHS[$path_index]}"
    local curl_args=(
      --silent
      --show-error
      --max-time "${REQUEST_TIMEOUT_SECONDS}"
      -o /dev/null
      -w "%{http_code} %{time_total}\n"
    )
    if [[ -n "${REQUEST_HOST_HEADER}" ]]; then
      curl_args+=(-H "Host: ${REQUEST_HOST_HEADER}")
    fi
    if [[ -n "${CURL_RESOLVE_ENTRY}" ]]; then
      curl_args+=(--resolve "${CURL_RESOLVE_ENTRY}")
    fi

    curl "${curl_args[@]}" "${BASE_URL}${path}" \
      >> "${results_file}" 2>/dev/null || echo "000 ${REQUEST_TIMEOUT_SECONDS}" >> "${results_file}"
  done
}

for _ in $(seq 1 "${CONCURRENCY}"); do
  run_worker &
done
wait

total_requests="$(wc -l < "${results_file}" | tr -d ' ')"
if [[ "${total_requests}" -eq 0 ]]; then
  echo "[FAIL] No requests were executed."
  exit 1
fi

success_requests="$(awk '$1 >= 200 && $1 < 400 { c++ } END { print c + 0 }' "${results_file}")"
errors_5xx="$(awk '$1 >= 500 && $1 < 600 { c++ } END { print c + 0 }' "${results_file}")"
timeouts="$(awk '$1 == 000 { c++ } END { print c + 0 }' "${results_file}")"

success_rate="$(awk -v s="${success_requests}" -v t="${total_requests}" 'BEGIN { printf "%.2f", (s / t) * 100 }')"

awk '{ print $2 }' "${results_file}" | sort -n > "${times_file}"
p95_index="$(awk -v t="${total_requests}" 'BEGIN { idx = int(t * 0.95); if (idx < 1) idx = 1; print idx }')"
p95_seconds="$(awk -v idx="${p95_index}" 'NR == idx { print $1; exit }' "${times_file}")"

echo "Total requests: ${total_requests}"
echo "Success requests: ${success_requests}"
echo "5xx responses: ${errors_5xx}"
echo "Timeouts/transport errors: ${timeouts}"
echo "Success rate: ${success_rate}%"
echo "P95 latency: ${p95_seconds}s"

pass_success_rate="$(awk -v rate="${success_rate}" -v min="${MIN_SUCCESS_RATE_PERCENT}" 'BEGIN { print (rate >= min) ? "1" : "0" }')"
pass_p95="$(awk -v p95="${p95_seconds}" -v max="${MAX_P95_SECONDS}" 'BEGIN { print (p95 <= max) ? "1" : "0" }')"

if [[ "${pass_success_rate}" != "1" || "${pass_p95}" != "1" || "${errors_5xx}" -gt 0 ]]; then
  echo "[FAIL] Load gate failed."
  echo "Expected success rate >= ${MIN_SUCCESS_RATE_PERCENT}% and p95 <= ${MAX_P95_SECONDS}s with zero 5xx."
  exit 1
fi

echo "Load gate passed."
