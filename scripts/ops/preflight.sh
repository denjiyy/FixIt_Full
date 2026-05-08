#!/usr/bin/env bash
set -euo pipefail

ENV_FILE="${1:-${FIXIT_ENV_FILE:-.env.production}}"

if [[ ! -f "${ENV_FILE}" ]]; then
  echo "[ERROR] Environment file not found: ${ENV_FILE}"
  exit 1
fi

errors=0
warnings=0

pass() {
  echo "[PASS] $1"
}

warn() {
  warnings=$((warnings + 1))
  echo "[WARN] $1"
}

fail() {
  errors=$((errors + 1))
  echo "[ERROR] $1"
}

trim() {
  local value="$1"
  value="${value#"${value%%[![:space:]]*}"}"
  value="${value%"${value##*[![:space:]]}"}"
  printf "%s" "${value}"
}

get_var() {
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

is_placeholder() {
  local value
  value="$(printf "%s" "$1" | tr '[:upper:]' '[:lower:]')"
  if [[ -z "${value}" ]]; then
    return 0
  fi

  local markers=(
    '${'
    'your_'
    'your-'
    '<'
    '>'
    'change-me'
    'set-in-secret-manager'
    'placeholder'
    'example'
    'replace-me'
  )

  local marker
  for marker in "${markers[@]}"; do
    if [[ "${value}" == *"${marker}"* ]]; then
      return 0
    fi
  done

  return 1
}

is_truthy() {
  local value
  value="$(printf "%s" "$1" | tr '[:upper:]' '[:lower:]')"
  [[ "${value}" == "true" || "${value}" == "1" || "${value}" == "yes" || "${value}" == "on" ]]
}

require_var() {
  local key="$1"
  local description="$2"
  local value
  value="$(get_var "${key}" || true)"

  if [[ -z "${value}" ]]; then
    fail "${key} is missing (${description})"
    return 1
  fi

  pass "${key} is set"
  return 0
}

echo "Running production preflight checks using ${ENV_FILE}"

require_var "ASPNETCORE_ENVIRONMENT" "must be Production for release"
require_var "MONGODB_CONNECTION_STRING" "MongoDB connection"
require_var "MONGODB_DATABASE_NAME" "MongoDB database name"
require_var "GOOGLE_CLIENT_ID" "OAuth login"
require_var "GOOGLE_CLIENT_SECRET" "OAuth login"
require_var "JWT_SECRET_KEY" "JWT signing secret"
require_var "ALLOWED_HOSTS" "host filtering"
require_var "CORS_ALLOWED_ORIGINS" "browser origin allowlist"
require_var "EMAIL_PROVIDER" "email transport"
require_var "EMAIL_FROM_ADDRESS" "notification sender"
require_var "DATA_PROTECTION_KEY_RING_PATH" "persistent key ring path"
require_var "APP_BASE_URL" "public app URL"
require_var "SECURITY_HTTPS_PORT" "expected HTTPS destination port"

aspn_env="$(get_var "ASPNETCORE_ENVIRONMENT" || true)"
if [[ "${aspn_env}" != "Production" ]]; then
  fail "ASPNETCORE_ENVIRONMENT must be Production (current: '${aspn_env:-unset}')"
fi

jwt_secret="$(get_var "JWT_SECRET_KEY" || true)"
if [[ -n "${jwt_secret}" ]]; then
  if (( ${#jwt_secret} < 32 )); then
    fail "JWT_SECRET_KEY must be at least 32 characters"
  fi
  if is_placeholder "${jwt_secret}"; then
    fail "JWT_SECRET_KEY looks like a placeholder"
  fi
fi

google_client_id="$(get_var "GOOGLE_CLIENT_ID" || true)"
google_client_secret="$(get_var "GOOGLE_CLIENT_SECRET" || true)"
if [[ -n "${google_client_id}" ]] && is_placeholder "${google_client_id}"; then
  fail "GOOGLE_CLIENT_ID looks like a placeholder"
fi
if [[ -n "${google_client_secret}" ]] && is_placeholder "${google_client_secret}"; then
  fail "GOOGLE_CLIENT_SECRET looks like a placeholder"
fi

mongo_conn="$(get_var "MONGODB_CONNECTION_STRING" || true)"
if [[ -n "${mongo_conn}" ]]; then
  if is_placeholder "${mongo_conn}"; then
    fail "MONGODB_CONNECTION_STRING looks like a placeholder"
  fi
  if [[ "${mongo_conn}" == *"localhost"* || "${mongo_conn}" == *"127.0.0.1"* ]]; then
    fail "MONGODB_CONNECTION_STRING points to localhost; production should use a remote/managed endpoint"
  fi
fi

allowed_hosts="$(get_var "ALLOWED_HOSTS" || true)"
if [[ -n "${allowed_hosts}" ]]; then
  if [[ "${allowed_hosts}" == *"*"* ]]; then
    fail "ALLOWED_HOSTS must not contain '*' in production"
  fi
  if [[ "$(printf "%s" "${allowed_hosts}" | tr '[:upper:]' '[:lower:]')" == *"localhost"* || "${allowed_hosts}" == *"127.0.0.1"* ]]; then
    fail "ALLOWED_HOSTS must not include localhost/127.0.0.1 in production"
  fi
fi

cors_origins="$(get_var "CORS_ALLOWED_ORIGINS" || true)"
if [[ -n "${cors_origins}" ]]; then
  IFS=',;' read -r -a origin_list <<< "${cors_origins}"
  for origin in "${origin_list[@]}"; do
    origin="$(trim "${origin}")"
    [[ -z "${origin}" ]] && continue
    if [[ "${origin}" != https://* ]]; then
      fail "CORS origin must use https in production: ${origin}"
    fi
    if [[ "$(printf "%s" "${origin}" | tr '[:upper:]' '[:lower:]')" == *"localhost"* || "${origin}" == *"127.0.0.1"* ]]; then
      fail "CORS origin must not include localhost/127.0.0.1 in production: ${origin}"
    fi
  done
fi

app_base_url="$(get_var "APP_BASE_URL" || true)"
if [[ -n "${app_base_url}" ]]; then
  if [[ "${app_base_url}" != https://* ]]; then
    fail "APP_BASE_URL must use https in production"
  fi
  if [[ "$(printf "%s" "${app_base_url}" | tr '[:upper:]' '[:lower:]')" == *"localhost"* || "${app_base_url}" == *"127.0.0.1"* ]]; then
    fail "APP_BASE_URL must not use localhost/127.0.0.1 in production"
  fi
fi

https_port="$(get_var "SECURITY_HTTPS_PORT" || true)"
if [[ -n "${https_port}" && ! "${https_port}" =~ ^[0-9]+$ ]]; then
  fail "SECURITY_HTTPS_PORT must be numeric"
elif [[ -n "${https_port}" && ( "${https_port}" -lt 1 || "${https_port}" -gt 65535 ) ]]; then
  fail "SECURITY_HTTPS_PORT must be within 1-65535"
fi

reset_on_startup="$(get_var "DATABASE_RESET_ON_STARTUP" || true)"
if is_truthy "${reset_on_startup:-false}"; then
  fail "DATABASE_RESET_ON_STARTUP must be false in production"
fi

admin_seed="$(get_var "DATABASE_ENABLE_DEVELOPMENT_ADMIN_SEED" || true)"
if is_truthy "${admin_seed:-false}"; then
  fail "DATABASE_ENABLE_DEVELOPMENT_ADMIN_SEED must be false in production"
fi

email_provider="$(get_var "EMAIL_PROVIDER" || true)"
if [[ "${email_provider}" == "Smtp" ]]; then
  for smtp_key in SMTP_HOST SMTP_PORT SMTP_USERNAME SMTP_PASSWORD; do
    if [[ -z "$(get_var "${smtp_key}" || true)" ]]; then
      fail "${smtp_key} is required when EMAIL_PROVIDER=Smtp"
    fi
  done
fi

trusted_proxy_ips="$(get_var "SECURITY_TRUSTED_PROXY_IPS" || true)"
if [[ -z "${trusted_proxy_ips}" ]]; then
  warn "SECURITY_TRUSTED_PROXY_IPS is empty; set trusted proxy IPs when running behind a reverse proxy"
fi

dp_cert_path="$(get_var "DATA_PROTECTION_CERTIFICATE_PATH" || true)"
dp_cert_password="$(get_var "DATA_PROTECTION_CERTIFICATE_PASSWORD" || true)"
if [[ -n "${dp_cert_path}" ]]; then
  if [[ ! -f "${dp_cert_path}" ]]; then
    fail "DATA_PROTECTION_CERTIFICATE_PATH points to a missing file: ${dp_cert_path}"
  fi
  if [[ -z "${dp_cert_password}" ]]; then
    warn "DATA_PROTECTION_CERTIFICATE_PASSWORD is empty; ensure certificate can be loaded without a password"
  fi
else
  warn "DATA_PROTECTION_CERTIFICATE_PATH is not set; key ring will not use an explicit at-rest encryptor"
fi

openai_api_key="$(get_var "OPENAI_API_KEY" || true)"
if [[ -n "${openai_api_key}" ]] && is_placeholder "${openai_api_key}"; then
  fail "OPENAI_API_KEY looks like a placeholder"
fi

if (( errors > 0 )); then
  echo "Preflight failed with ${errors} error(s) and ${warnings} warning(s)."
  exit 1
fi

echo "Preflight passed with ${warnings} warning(s)."
