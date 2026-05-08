# FixIt Beta Release Go/No-Go Checklist

Audience: diploma beta launch (10-20 users)

Mark every item before release.

## 0) One-Command Full Gate

- [ ] Run full automated gate:
  `scripts/ops/release-gate.sh .env.production`
- [ ] If this command fails, mark NO-GO and fix the reported gate.

## 1) Build and Test Gates

- [ ] `dotnet build FixIt.sln -c Release` passes with 0 errors.
- [ ] `dotnet test FixIt.sln -c Release --no-build` passes.
- [ ] `dotnet list FixIt.sln package --vulnerable --include-transitive` reports no vulnerabilities.
- [ ] `docker compose --env-file .env.example config --quiet` passes.
- [ ] `docker compose -f docker-compose.yml -f docker-compose.prod.yml --env-file .env.production config --quiet` passes.

## 2) Secrets and Configuration Gates

- [ ] `scripts/ops/preflight.sh .env.production` passes.
- [ ] `.env.production` is created from `.env.production.example`.
- [ ] `ASPNETCORE_ENVIRONMENT=Production`
- [ ] `JWT_SECRET_KEY` is 32+ characters and random.
- [ ] `GOOGLE_CLIENT_ID` and `GOOGLE_CLIENT_SECRET` are production values.
- [ ] `MONGODB_CONNECTION_STRING` points to production database.
- [ ] `CORS_ALLOWED_ORIGINS` only contains production domains.
- [ ] `ALLOWED_HOSTS` only contains production hostnames.
- [ ] `DATA_PROTECTION_KEY_RING_PATH` points to persistent storage.
- [ ] `SECURITY_HTTPS_PORT` is set correctly (usually `443`).

## 3) Runtime Smoke Gates

- [ ] Start production stack:
  `docker compose -f docker-compose.yml -f docker-compose.prod.yml --env-file .env.production up -d`
- [ ] Run smoke test:
  `scripts/ops/smoke.sh http://localhost:8080` (or your ingress URL)
- [ ] Verify app logs have no unhandled exceptions:
  `docker compose -f docker-compose.yml -f docker-compose.prod.yml --env-file .env.production logs --tail=200 fixit-app`

## 4) Light Load Gate (10-20 Users)

- [ ] Run light load:
  `CONCURRENCY=20 DURATION_SECONDS=90 scripts/ops/load-lite.sh http://localhost:8080`
- [ ] Pass criteria:
  - success rate >= 99%
  - p95 latency <= 1.50s
  - 0 responses with status 5xx

## 5) Backup and Restore Gates

- [ ] Backup succeeds:
  `FIXIT_ENV_FILE=.env.production scripts/ops/mongo-backup.sh ./backups`
- [ ] Restore test succeeds on non-production environment:
  `FIXIT_ENV_FILE=.env.production scripts/ops/mongo-restore.sh ./backups/<file>.archive.gz --drop`
- [ ] Backup file is stored outside the app host (cloud storage or off-host copy).

## 6) Rollback Gate

- [ ] Previous known-good image tag is recorded.
- [ ] Rollback command is prepared and tested in staging:
  `docker compose -f docker-compose.yml -f docker-compose.prod.yml --env-file .env.production up -d --force-recreate`
- [ ] Team knows the rollback trigger: any sustained 5xx spike or failed readiness.

## 7) Final Go/No-Go

- [ ] All items above are complete.
- [ ] If any gate fails, mark NO-GO and fix before release.

Release decision:
- GO [ ]
- NO-GO [ ]
