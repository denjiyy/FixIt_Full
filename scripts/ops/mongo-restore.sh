#!/usr/bin/env bash
set -euo pipefail

if [[ $# -lt 1 ]]; then
  echo "Usage: $0 <backup-file> [--drop]"
  exit 1
fi

BACKUP_FILE="$1"
DROP_FLAG="${2:-}"
DB_NAME="${MONGODB_DATABASE_NAME:-fixit}"
MONGO_USER="${MONGODB_ROOT_USERNAME:-root}"
MONGO_PASSWORD="${MONGODB_ROOT_PASSWORD:-}"

if [[ ! -f "${BACKUP_FILE}" ]]; then
  echo "Backup file not found: ${BACKUP_FILE}"
  exit 1
fi

if [[ -z "${MONGO_PASSWORD}" ]]; then
  echo "MONGODB_ROOT_PASSWORD is required."
  exit 1
fi

restore_cmd=(
  docker compose exec -T mongodb
  mongorestore
  --username "${MONGO_USER}"
  --password "${MONGO_PASSWORD}"
  --authenticationDatabase admin
  --db "${DB_NAME}"
  --archive
  --gzip
)

if [[ "${DROP_FLAG}" == "--drop" ]]; then
  restore_cmd+=(--drop)
fi

echo "Restoring MongoDB backup from: ${BACKUP_FILE}"
"${restore_cmd[@]}" < "${BACKUP_FILE}"
echo "Restore complete."
