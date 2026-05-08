#!/usr/bin/env bash
set -euo pipefail

OUTPUT_DIR="${1:-./backups}"
ENV_FILE="${FIXIT_ENV_FILE:-.env}"
MONGO_SERVICE="${MONGO_SERVICE_NAME:-mongodb}"
DB_NAME="${MONGODB_DATABASE_NAME:-}"
MONGO_USER="${MONGODB_ROOT_USERNAME:-}"
MONGO_PASSWORD="${MONGODB_ROOT_PASSWORD:-}"

trim() {
  local value="$1"
  value="${value#"${value%%[![:space:]]*}"}"
  value="${value%"${value##*[![:space:]]}"}"
  printf "%s" "${value}"
}

get_env_file_var() {
  local key="$1"
  if [[ ! -f "${ENV_FILE}" ]]; then
    return 1
  fi

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

if [[ -z "${DB_NAME}" ]]; then
  DB_NAME="$(get_env_file_var "MONGODB_DATABASE_NAME" || true)"
fi
if [[ -z "${MONGO_USER}" ]]; then
  MONGO_USER="$(get_env_file_var "MONGODB_ROOT_USERNAME" || true)"
fi
if [[ -z "${MONGO_PASSWORD}" ]]; then
  MONGO_PASSWORD="$(get_env_file_var "MONGODB_ROOT_PASSWORD" || true)"
fi

DB_NAME="${DB_NAME:-fixit}"
MONGO_USER="${MONGO_USER:-root}"

if [[ -z "${MONGO_PASSWORD}" ]]; then
  echo "MONGODB_ROOT_PASSWORD is required (env var or ${ENV_FILE})."
  exit 1
fi

mkdir -p "${OUTPUT_DIR}"
timestamp="$(date -u +%Y%m%dT%H%M%SZ)"
backup_file="${OUTPUT_DIR}/fixit-${DB_NAME}-${timestamp}.archive.gz"

echo "Creating MongoDB backup: ${backup_file}"
docker compose --env-file "${ENV_FILE}" exec -T "${MONGO_SERVICE}" mongodump \
  --username "${MONGO_USER}" \
  --password "${MONGO_PASSWORD}" \
  --authenticationDatabase admin \
  --nsInclude "${DB_NAME}.*" \
  --archive \
  --gzip > "${backup_file}"

echo "Backup complete."
