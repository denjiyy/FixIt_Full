#!/usr/bin/env bash
set -euo pipefail

OUTPUT_DIR="${1:-./backups}"
DB_NAME="${MONGODB_DATABASE_NAME:-fixit}"
MONGO_USER="${MONGODB_ROOT_USERNAME:-root}"
MONGO_PASSWORD="${MONGODB_ROOT_PASSWORD:-}"

if [[ -z "${MONGO_PASSWORD}" ]]; then
  echo "MONGODB_ROOT_PASSWORD is required."
  exit 1
fi

mkdir -p "${OUTPUT_DIR}"
timestamp="$(date -u +%Y%m%dT%H%M%SZ)"
backup_file="${OUTPUT_DIR}/fixit-${DB_NAME}-${timestamp}.archive.gz"

echo "Creating MongoDB backup: ${backup_file}"
docker compose exec -T mongodb mongodump \
  --username "${MONGO_USER}" \
  --password "${MONGO_PASSWORD}" \
  --authenticationDatabase admin \
  --db "${DB_NAME}" \
  --archive \
  --gzip > "${backup_file}"

echo "Backup complete."
