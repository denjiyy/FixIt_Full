# FixIt — Civic Engagement Platform

A full-stack civic engagement platform where citizens report local infrastructure issues and safety hazards, track resolution progress, and earn reputation through community participation. Web + native mobile (iOS/Android), one REST API.

![.NET](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet)
![ASP.NET Core](https://img.shields.io/badge/ASP.NET%20Core-9.0-black?logo=dotnet)
![MAUI](https://img.shields.io/badge/.NET%20MAUI-iOS%20%7C%20Android-512BD4?logo=dotnet)
![MongoDB](https://img.shields.io/badge/MongoDB-Atlas-47A248?logo=mongodb)
![Railway](https://img.shields.io/badge/Deployed%20on-Railway-0B0D0E?logo=railway)
![Tests](https://img.shields.io/badge/tests-255%20passing-success)
![License](https://img.shields.io/badge/license-MIT-blue)

Live at **[fixitfull-production.up.railway.app](https://fixitfull-production.up.railway.app)**.

---

## Table of Contents

- [Project Status](#project-status)
- [Overview](#overview)
- [Features](#features)
- [Tech Stack](#tech-stack)
- [Architecture](#architecture)
- [Project Structure](#project-structure)
- [Getting Started](#getting-started)
- [Mobile App](#mobile-app)
- [Configuration](#configuration)
- [API Reference](#api-reference)
- [Database Schema](#database-schema)
- [Gamification](#gamification)
- [AI Features](#ai-features)
- [Security](#security)
- [Testing](#testing)
- [Deployment & Ops](#deployment--ops)
- [Contributing](#contributing)
- [License](#license)

---

## Project Status

Backend is **production-deployed on Railway** against MongoDB Atlas. Mobile (iOS/Android) builds and runs against the same API. The recent improvement arc (commit log: `7002e31..cffd48a`):

- **Admin bootstrap CLI** — `dotnet run -- --bootstrap-admin --email <e> --password <p>` provisions the first admin without manual DB edits.
- **Role consistency invariant** — `UserRoleSync.SetUserRoleAsync` is the single chokepoint for role changes, keeping `ApplicationUser.Role` and Identity role claims in sync. Startup audit logs any pre-existing drift.
- **API auth scheme** — `[ApiAuthorize]` pins `/api/*` endpoints to JWT Bearer. Mobile clients get clean 401/403, not the cookie scheme's 302-to-login.
- **Migration concurrency lock** — rolling deploys no longer race the same migration into a partial state.
- **Cron-based scheduling** — leaderboard regeneration uses `Cronos` expressions; the previous `Timer + FromDays(30)` had ~5-day annual drift.
- **CI as a real gate** — Codecov status checks block coverage regressions; vulnerable-package check on every PR.
- **Integration tests** — `WebApplicationFactory<Program>` + Mongo `Testcontainers` for full HTTP-roundtrip auth coverage.
- **255 tests passing** (245 unit, 5 integration, 5 service-extraction).

Known deferred items (Phase 4 candidates): Mongo multi-document transactions for the reputation pipeline, CSP nonces to remove `'unsafe-inline'`, and the rest of `IssueService` decomposition (vote methods → `VoteService`).

---

## Overview

FixIt connects citizens with local government by giving them a straightforward way to report infrastructure problems — potholes, broken streetlights, graffiti, flooding — and track them through to resolution. The platform pairs traditional issue tracking with AI-assisted analysis, a gamification layer that rewards civic participation, and real-time safety alerts.

Web and mobile share the same REST API, the same domain models, and the same business-logic services. The two clients differ only in the presentation layer.

---

## Features

### Core

| Feature | Description |
|---|---|
| **Issue reporting** | Photos, geocoded location, descriptions, optional anonymity |
| **Safety hazards** | Live geolocated hazard map — tap to report, severity levels, community confirmation, and AI trend insights |
| **Voting** | Up/downvote to surface the most urgent concerns |
| **Comments** | Threaded discussion with media attachments |
| **Official responses** | Government entities post status updates |
| **Map picker** | Leaflet map with reverse-geocoded street address |
| **Heatmaps** | Geospatial visualisation of issue clusters |

### Advanced

| Feature | Description |
|---|---|
| **Gamification** | Reputation points, achievements, four-tier trust levels, leaderboards |
| **AI analysis** | Categorisation, duplicate detection, priority suggestion via OpenAI |
| **Health reports** | City-level resolution rates, response times, hazard density |
| **Multi-language** | en, bg |
| **OAuth / JWT** | Google sign-in (web); JWT Bearer (mobile/API) |
| **Rate limiting** | Per-endpoint sliding-window enforcement |
| **Audit logging** | Comprehensive admin-action trail |

### Mobile

| Feature | Description |
|---|---|
| **Native iOS & Android** | Single .NET MAUI codebase |
| **Map-based picker** | Tap a Leaflet map to drop a pin; reverse geocoded to address + city |
| **Auto-geolocation** | Permission requested on first Report Issue visit |
| **Multi-photo capture** | Camera + gallery, up to 5 per report; photos are optional |
| **Adaptive theming** | Light / dark / system, adaptive iOS status bar |
| **JWT auth** | Refresh tokens in platform `SecureStorage`; transparent retry on 401 |
| **Offline-aware** | API failures surface localised error states |

---

## Tech Stack

### Backend

| Component | Technology |
|---|---|
| Framework | ASP.NET Core 9.0 |
| Language | C# 13 |
| Database | MongoDB Atlas (driver 2.30.0) |
| Identity | ASP.NET Core Identity + `AspNetCore.Identity.Mongo` 9.0.0 |
| Auth | Cookie session (web) + JWT Bearer (mobile/API) |
| OAuth | Google |
| AI | OpenAI API (`gpt-4o-mini`) |
| Email | SMTP (SendGrid / Gmail) |
| Scheduling | `Cronos` for cron-expression scheduling |

### Web Frontend

| Component | Technology |
|---|---|
| UI | Razor Pages + MVC |
| Maps | Leaflet.js + OpenStreetMap |
| Styling | Custom design tokens + Bootstrap utilities |
| JavaScript | Vanilla ES6+ modules |

### Mobile

| Component | Technology |
|---|---|
| Framework | .NET MAUI 9.0 |
| Platforms | iOS 15+, Android 5+ |
| Architecture | MVVM with `CommunityToolkit.Mvvm` |
| HTTP | `Microsoft.Extensions.Http` + custom `DelegatingHandler` for auth/refresh |
| Storage | `SecureStorage` for JWT + refresh tokens |
| Maps | Leaflet in a `WebView` with a `fixit://` URL bridge |

### DevOps & Quality

| Component | Technology |
|---|---|
| Hosting | Railway |
| Containerisation | Docker (multi-stage) |
| API Docs | Swagger / OpenAPI |
| Compression | Brotli + Gzip |
| Caching | MemoryCache + OutputCache + ETag middleware |
| CI | GitHub Actions (build/test/coverage, security scan, Docker push) |
| Coverage | Codecov with status-check gates (`codecov.yml`) |
| Integration tests | `Testcontainers.MongoDb` + `WebApplicationFactory<Program>` |

---

## Architecture

```
┌──────────────────────────────────────────────────────────────┐
│                          Clients                              │
│  ┌─────────────────────┐         ┌──────────────────────────┐ │
│  │  Web Browser         │         │  FixIt.Mobile (MAUI)     │ │
│  │  Razor Pages / MVC   │         │  iOS  |  Android         │ │
│  └──────────┬──────────┘         └────────────┬─────────────┘ │
└─────────────┼──────────────────────────────────┼──────────────┘
              │ HTTP / Cookie session             │ HTTP / JWT Bearer
┌─────────────▼──────────────────────────────────▼──────────────┐
│                      Presentation Layer                        │
│  Razor Pages  |  REST API ([ApiAuthorize])  |  Admin Area     │
└──────────────────────────────┬────────────────────────────────┘
                                │
┌──────────────────────────────▼────────────────────────────────┐
│                       Service Layer                            │
│  Issues | Comments | Safety | AI | Gamification | Auth |       │
│  Analytics | Media | Geocoding | Reputation                   │
└──────────────────────────────┬────────────────────────────────┘
                                │
┌──────────────────────────────▼────────────────────────────────┐
│                        Data Layer                              │
│  Repository<T> | MongoDbContext | MigrationRunner (locked)    │
└──────────────────────────────┬────────────────────────────────┘
                                │
┌──────────────────────────────▼────────────────────────────────┐
│                      MongoDB Atlas                              │
│  Issues | Users | Hazards | Comments | Votes | Media | ...    │
└──────────────────────────────────────────────────────────────┘
```

### Key architectural decisions

These are the load-bearing patterns worth knowing before contributing:

- **Two-scheme auth, path-routed.** Cookie auth backs Razor Pages; JWT Bearer backs `/api/*`. The `[ApiAuthorize]` attribute (`FixIt/Extensions/ApiAuthorizeAttribute.cs`) pins API endpoints to `JwtBearerDefaults.AuthenticationScheme` so unauthenticated `/api/*` requests get `401` (not a cookie-scheme `302` redirect). Use `[ApiAuthorize]` on every API controller endpoint that requires auth.
- **Single source of truth for roles.** `ApplicationUser.Role` is a denormalized convenience field; Identity's role claims are authoritative. All role mutations go through `UserRoleSync.SetUserRoleAsync` (`FixIt/Extensions/UserRoleSync.cs`) which updates both atomically. A startup audit (`UserRoleSync.AuditRoleDriftAsync`) warns on any mismatches in existing data.
- **Migration safety.** `MigrationRunner` acquires an advisory lock in `_migration_lock` via atomic `findAndModify` before running. 10-minute stale-cutoff TTL reclaims locks from crashed pods; 5-minute acquire timeout fails loudly rather than silently skipping migrations.
- **Cron, not Timer.** `LeaderboardRegenerationService` uses `Cronos` expressions, not `TimeSpan.FromDays(30)`. Each cadence re-anchors per iteration — no month-length drift.
- **Production seed gate.** `seedDemoData = !isProduction` in `Program.cs`. Cities + tags + indexes seed everywhere (reference data); sample issues do not seed in production.
- **N+1 prevention.** Author hydration in `CommentService.GetCommentsForIssueAsync` batches via a single `In(ObjectId[])` query, never `await FindByIdAsync` in a loop.

---

## Project Structure

```
FixIt/
├── FixIt/                          # Web application + REST API
│   ├── Areas/                      # Admin & Identity areas
│   ├── Controllers/                # REST API controllers (use [ApiAuthorize])
│   ├── Extensions/
│   │   ├── ApiAuthorizeAttribute.cs    # JWT-scheme [Authorize] variant
│   │   ├── AdminBootstrapExtensions.cs # --bootstrap-admin CLI + EnsureAdminAsync
│   │   ├── UserRoleSync.cs             # Role sync + drift audit
│   │   ├── AuthExtensions.cs           # Cookie + Bearer auth registration
│   │   └── IdentityExtensions.cs       # Policies (AdminOnly, AdminArea)
│   ├── Pages/                      # Razor pages (web UI)
│   ├── Middleware/                 # GlobalExceptionHandlingMiddleware
│   ├── Resources/                  # Localisation (en, bg)
│   ├── wwwroot/                    # Static assets
│   └── Program.cs                  # Entry point, CLI short-circuit, DI, pipeline
│
├── FixIt.Mobile/                   # .NET MAUI app (iOS & Android)
│   ├── Views/                      # XAML pages
│   ├── ViewModels/                 # MVVM
│   ├── Services/
│   │   ├── ApiService.cs               # REST client (bounded caches)
│   │   ├── AuthService.cs              # JWT login / refresh / SecureStorage
│   │   ├── AuthHeaderHandler.cs        # DelegatingHandler — retry-on-401 + refresh
│   │   └── BoundedCache.cs             # Capacity-capped FIFO-evict cache
│   ├── Platforms/                  # iOS & Android specifics
│   └── MauiProgram.cs              # DI + HttpClient + toolkit setup
│
├── FixIt.Mobile.Tests/             # xUnit (plain net9.0 — no MAUI framework)
├── FixIt.Models/                   # Domain models, enums (shared)
├── FixIt.Services/                 # Business logic (shared)
│   ├── IssueService.cs                 # Issue CRUD + votes
│   ├── CommentService.cs               # Extracted in Phase 3
│   ├── Gamification/
│   ├── Background/
│   │   ├── LeaderboardRegenerationService.cs  # Cronos-driven
│   │   └── IssueAnalysisQueue.cs
│   └── Audit/MongoDbAuditService.cs
├── FixIt.Data/                     # Repository<T>, migrations, MongoDB context
│   └── Infrastructure/
│       └── Migrations/
│           ├── MigrationRunner.cs       # Advisory-lock concurrency control
│           └── MigrationLock.cs
├── FixIt.ViewModels/               # DTOs, responses, MapperExtensions
├── FixIt.Tests/                    # Backend unit + integration tests
│   └── Integration/                # WebApplicationFactory + Testcontainers
├── scripts/ops/                    # Operational scripts
│   ├── prod-clean.js                   # mongosh: dry-run-by-default cleanup
│   ├── preflight.sh                    # Pre-deploy env validation
│   ├── smoke.sh                        # Health probe a running instance
│   ├── release-gate.sh                 # Build + test + smoke + load + backup
│   ├── mongo-backup.sh
│   └── mongo-restore.sh
├── Directory.Build.props           # Backend-wide build settings (warnings-as-errors)
├── global.json                     # Pinned .NET SDK 9.0.304
├── codecov.yml                     # Coverage status-check policy
├── Dockerfile                      # Multi-stage build
└── FixIt.sln
```

---

## Getting Started

### Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) (the repo pins `9.0.304` via `global.json`; later patches roll forward)
- [MongoDB](https://www.mongodb.com/try/download/community) (v6.0+) — local or MongoDB Atlas
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) — required to run integration tests locally
- Optional: macOS + Xcode (iOS dev), Android SDK (Android dev)

### Web app — first run

```bash
git clone https://github.com/denjiyy/FixIt.git
cd FixIt

cp .env.example .env
# Edit .env with your MongoDB URI, JWT secret, OAuth, etc.

dotnet restore
dotnet run --project FixIt
```

Once running:

| Surface | URL |
|---|---|
| Web | http://localhost:5092 |
| API | http://localhost:5092/api |
| Swagger | http://localhost:5092/swagger |
| Admin login | http://localhost:5092/admin/login |

### Provisioning the first admin

The admin surface (`/admin/*`) is role-gated; you need at least one user with the `Admin` role to access it. There are two ways:

**Recommended — CLI subcommand** (works in any environment):

```bash
dotnet run --project FixIt -- \
  --bootstrap-admin \
  --email you@example.com \
  --password 'YourStrongPassword1'
```

Optional flags: `--username`, `--display-name`, `--force` (overwrite existing user with that email). Exit codes: `0` success, `1` bad args, `2` user already exists (use `--force`), `3` Identity failure. Works on Railway via `railway run`.

**Alternative — dev auto-seed** (development only):

```sh
# Via user-secrets so the password stays out of git
dotnet user-secrets set "Database:DevelopmentAdmin:Password" "YourDevPassword1" --project FixIt
dotnet user-secrets set "Database:EnableDevelopmentAdminSeed" "true" --project FixIt
```

Then start the app with an empty DB (or set `Database:ResetOnStartup=true`). The seeder routes through the same `EnsureAdminAsync` helper as the CLI.

**Signing in as an admin:**

The admin panel has its own login page at **`/Admin/Login`** — it is intentionally separate from the public `/Identity/Account/Login` and is not linked from the main navigation. There is also a discreet **"Staff sign-in"** link in the site footer (shown when signed out).

- Sign in with your **email as the username** (the bootstrap sets `UserName = email` unless you pass `--username`) plus the password you provisioned.
- Admins land on `/admin/dashboard`; moderators land on `/admin/issues`.
- Regular accounts created through the public Register page get the `User` role and are rejected by the admin login with "You don't have permission to access the admin panel" — registering normally never grants admin. Provision via the CLI/seeder above.

### Production: starting from an empty database

A production deploy on a fresh MongoDB will:

1. Acquire the migration lock and run pending migrations.
2. Seed reference data only (cities, tags, indexes) — `seedDemoData=false` is forced when `ASPNETCORE_ENVIRONMENT=Production`.
3. Log a `CRITICAL` warning if no admin exists, telling you to run the bootstrap CLI.

Run the bootstrap CLI once after the deploy is up.

---

## Mobile App

`FixIt.Mobile` is a .NET MAUI app targeting iOS and Android, talking to the same Railway-hosted REST API via JWT Bearer.

### Report-issue flow

1. User opens **Report Issue** from the elevated centre FAB.
2. App requests location permission. If granted it centres the Leaflet map on the device's coordinates; otherwise falls back to a default city and asks the user to tap the map.
3. Each tap drops the pin, updates lat/lon, and reverse-geocodes to fill the address/city.
4. Native camera launches once per session for issue photos. Cancelling returns to the form without losing context.
5. Take Photo / Pick from Gallery / Remove / skip — up to 5, photos optional.
6. Submit POSTs `/api/issues` (JSON) then uploads each photo to `/api/issues/{id}/media` (multipart). Partial-upload failures surface as a warning; the issue itself is created regardless.

### Running locally

```bash
# iOS simulator
dotnet build FixIt.Mobile/FixIt.Mobile.csproj -f net9.0-ios -c Debug

# Android
dotnet build FixIt.Mobile/FixIt.Mobile.csproj -f net9.0-android -c Debug
```

Or launch the target from Visual Studio / Rider / VS Code's MAUI integration.

The iOS simulator skips the `Entitlements.plist` keychain-access-group (it would otherwise require a provisioning profile). On device builds the entitlement is applied and `SecureStorage` uses the bundle keychain group.

### Authentication

The mobile app authenticates against `/api/auth/login`, persists access + refresh tokens to platform `SecureStorage`, and an `AuthHeaderHandler` attaches the access token to every request, transparently refreshes on 401, and signs the user out if refresh fails. Login fails cleanly with a localised error if token persistence ever fails — no phantom logged-in state.

---

## Configuration

### Environment variables

| Variable | Description | Example |
|---|---|---|
| `MONGODB_URI` | Atlas connection string | `mongodb+srv://user:pass@cluster.mongodb.net/` |
| `MONGODB_DATABASE` | Database name | `fixit` |
| `JWT_SECRET_KEY` | JWT signing key (min 32 chars; ≥64 recommended) | `…` |
| `GOOGLE_CLIENT_ID` | Google OAuth client ID | `…apps.googleusercontent.com` |
| `GOOGLE_CLIENT_SECRET` | Google OAuth secret | `GOCSPX-…` |
| `OPENAI_API_KEY` | OpenAI API key (optional — AI disabled if unset) | `<openai-api-key>` |
| `AllowedHosts` | Accepted hostnames | `*` |
| `SMTP_HOST` / `SMTP_PORT` / `SMTP_USERNAME` / `SMTP_PASSWORD` | Outbound email | |
| `DATA_PROTECTION_KEY_RING_PATH` | DataProtection key storage path | `/app/data-protection-keys` |

See [`SECRETS.md`](./SECRETS.md) for the full environment and secrets reference.

### appsettings.json excerpt

```json
{
  "MongoDB": {
    "ConnectionString": "mongodb://localhost:27017",
    "DatabaseName": "fixit"
  },
  "Jwt": {
    "Issuer": "FixIt",
    "Audience": "FixItClients",
    "AccessTokenExpirationMinutes": 30,
    "RefreshTokenExpirationDays": 7
  },
  "Media": {
    "StoragePath": "wwwroot/uploads",
    "MaxFileSizeBytes": 5242880,
    "MaxVideoFileSizeBytes": 104857600,
    "AllowedExtensions": [".jpg", ".jpeg", ".png", ".mp4", ".webm"]
  },
  "OpenAI": {
    "Model": "gpt-4o-mini",
    "Enabled": true,
    "TimeoutSeconds": 30
  },
  "Security": {
    "RateLimiting": { "Enabled": true, "RequestsPerMinute": 60 }
  }
}
```

---

## API Reference

Full interactive docs at `/swagger` on any running instance. The summary tables below cover the surface that matters; the swagger UI is authoritative.

<details>
<summary><b>Authentication</b> — JWT login, refresh, OAuth callback</summary>

| Method | Endpoint | Description | Auth |
|---|---|---|---|
| `POST` | `/api/auth/login` | Email + password login (returns JWT pair) | No |
| `POST` | `/api/auth/register` | New-user registration | No |
| `POST` | `/api/auth/refresh` | Rotate access token using a refresh token | No |
| `POST` | `/api/auth/logout` | Invalidate refresh token version | Yes |
| `GET` | `/api/auth/user` | Current user info | Yes |
| `POST` | `/api/auth/login/{provider}` | OAuth challenge (Google) | No |
| `GET` | `/api/auth/signin-callback` | OAuth callback (web redirect or mobile JSON) | No |
| `POST` | `/api/auth/link-provider/{provider}` | Link OAuth provider to current account | Yes |
| `POST` | `/api/auth/unlink-provider/{provider}` | Unlink provider | Yes |

</details>

<details>
<summary><b>Issues</b> — CRUD, search, voting, comments</summary>

| Method | Endpoint | Description | Auth |
|---|---|---|---|
| `POST` | `/api/issues` | Create issue | Yes |
| `POST` | `/api/issues/{id}/media` | Upload media (multipart) | Yes |
| `GET` | `/api/issues/{id}` | Get issue details | No |
| `GET` | `/api/issues/city/{cityId}` | Issues by city | No |
| `POST` | `/api/issues/city/{cityId}/search` | Search issues | No |
| `GET` | `/api/issues/my-issues` | Current user's issues | Yes |
| `PUT` | `/api/issues/{id}/status` | Update status | Mod/Admin |
| `PUT` | `/api/issues/{id}/priority` | Update priority | Mod/Admin |
| `POST` / `DELETE` | `/api/issues/{id}/vote` | Vote / un-vote | Yes |
| `DELETE` | `/api/issues/{id}` | Soft-delete | Owner/Admin |
| `POST` / `GET` | `/api/issues/{id}/comments` | Add / list comments | Yes / No |
| `POST` | `/api/issues/{id}/comments/{commentId}/like` | Like a comment | Yes |
| `POST` | `/api/issues/{id}/comments/{commentId}/dislike` | Dislike a comment | Yes |

`IssueDetailResponse` and `IssueSummaryResponse` both expose `latitude`/`longitude` derived from the entity's GeoJSON point, so map renderers don't need a separate lookup.

</details>

<details>
<summary><b>Safety, Geocoding, AI, Admin</b></summary>

**Safety**

| Method | Endpoint | Description | Auth |
|---|---|---|---|
| `GET` | `/api/safety/nearby-hazards` | Nearby hazards | No |
| `GET` | `/api/safety/critical-hazards` | Critical hazards | No |
| `POST` | `/api/safety/report` | Report a hazard | Yes |
| `POST` | `/api/safety/{id}/confirm` | Confirm a hazard | Yes |
| `POST` | `/api/safety/{id}/resolve` | Resolve a hazard | Admin |

**Geocoding**

| Method | Endpoint | Description | Auth |
|---|---|---|---|
| `GET` | `/api/geocoding/reverse?latitude={lat}&longitude={lng}` | Address + city for a coordinate (cached) | No |

**AI**

| Method | Endpoint | Description | Auth |
|---|---|---|---|
| `POST` | `/api/analysis/analyze/{issueId}` | Analyse an issue | No |
| `POST` | `/api/analysis/issue-draft-suggestions` | Title + description → category/priority/department | Yes |
| `GET` | `/api/issues/{id}/analysis` | Poll latest analysis (204 while pending) | No |

**Admin** (all `[ApiAuthorize(PolicyNames.AdminOnly)]` or `AdminArea`)

| Method | Endpoint | Description |
|---|---|---|
| `GET` | `/api/admin/audit-logs` | Audit-log query with filters |
| `GET` | `/api/admin/audit-logs/stats` | Aggregated stats |
| `GET` | `/api/admin/audit-logs/export/csv` | CSV download |
| `GET` | `/api/suggestions/pending` | Pending moderation suggestions |

</details>

---

## Database Schema

Collection names are defined in `FixIt.Data/Infrastructure/MongoCollectionNames.cs`. The legacy snake_case names (`media_references`, `content_reports`, `official_responses`, `moderation_actions`) are auto-renamed on startup via `MongoCollectionNamingMigration`.

| Collection | Description |
|---|---|
| `AspNetUsers` | Identity users — email, normalized email/username, role enum, reputation snapshot |
| `issues` | Issue reports with GeoJSON `Location` point + optional address |
| `hazards` | Safety hazards with severity + confirmation count |
| `comments` | Comments on issues (soft-delete via `IsDeleted`) |
| `votes` | Issue votes (unique compound index on `IssueId + UserId`) |
| `viewEvents` | View tracking for unique-view counting |
| `media` | Uploaded media metadata + Cloudinary URL |
| `mediaReferences` | Backlinks from media → issue or comment |
| `tags` | Tag taxonomy with usage counts |
| `cities` | Reference data (Bulgarian municipalities) |
| `neighborhoods` | Reference data |
| `userReputations` | Per-user reputation profiles |
| `reputationTransactions` | Full points history |
| `leaderboards` | Weekly / monthly / all-time leaderboard snapshots |
| `issueAnalyses` | AI analysis results |
| `audit-logs` | Admin-action audit trail (TTL: 3 years) |
| `adminSuggestions` | AI-suggested moderation actions |
| `contentReports` | User-reported content |
| `officialResponses` | Government-entity responses |
| `moderationActions` | Moderation event log |
| `translations` / `supportedLanguages` | i18n |
| `_migrations` | Migration history (don't touch) |
| `_migration_lock` | Migration concurrency lock (don't touch) |

<details>
<summary>Key relationships</summary>

```
ApplicationUser (1) ─→ (M) Issue
ApplicationUser (1) ─→ (1) UserReputation
Issue           (1) ─→ (M) Comment
Issue           (1) ─→ (M) Vote
Issue           (1) ─→ (1) IssueAnalysis
Issue           (1) ─→ (M) MediaReference ─→ (1) Media
City            (1) ─→ (M) Issue
City            (1) ─→ (M) Hazard
```

</details>

---

## Gamification

Four trust levels gate vote weight and visibility. Reputation points are awarded for civic participation; the `ReputationService` persists transactions and recomputes the user's snapshot.

<details>
<summary>Trust levels, points table, achievements</summary>

| Level | Name | Points | Permissions |
|---|---|---|---|
| 0 | New | 0–10 | Basic reporting |
| 1 | Active | 11–50 | Increased vote weight |
| 2 | Trusted | 51–150 | Priority vote weight, profile badge |
| 3 | Leader | 150+ | Maximum vote weight, leaderboard featured |

| Action | Points |
|---|---|
| Report a new issue | +5 |
| Issue confirmed by community | +3 |
| Post a comment | +2 |
| Receive an upvote | +1 |
| Issue marked resolved | +15 |
| Confirm a safety hazard | +5 |

| Achievement | Requirement | Points |
|---|---|---|
| FirstReporter | Report first issue | 10 |
| HelpfulCommenter | Post 10 comments | 15 |
| CommunityHelper | Receive 50 upvotes | 25 |
| IssueSolver | 5 issues resolved | 30 |
| CivicContributor | Reach Trust Level 2 | 20 |
| CommunityChampion | Reach Trust Level 3 | 50 |
| CivicLeader | Top of weekly leaderboard | 40 |
| AccurateReporter | 90%+ issues confirmed | 35 |
| VerifiedCitizen | Verify email + phone | 15 |

</details>

---

## AI Features

When OpenAI is enabled (`OPENAI_API_KEY` set, `OpenAI:Enabled=true`), new issue reports are analysed asynchronously via `IssueAnalysisQueue`:

- **Categorisation** — Infrastructure, Public Safety, Environmental Health, Parks, Transportation, Utilities, Sanitation, Public Health, Other
- **Priority** — Low / Medium / High / Critical
- **Duplicate detection** — compared against open issues nearby
- **Keyword extraction** + **tag suggestions** drawn from the live tag database

Additional capabilities exposed via `ICivicAiService`: `SuggestIssueDraft` (title/description → suggested category, priority, department), `SummarizeIssueThread` (streaming summary), `GenerateHazardInsight` (pattern analysis across hazard clusters), and `TranslateIssueFilter` (natural language → structured filter).

Deterministic fallbacks engage if the API is unavailable or disabled, so the rest of the app keeps working.

---

## Security

### Authentication flows

- **Web** — cookie session with anti-forgery tokens.
- **Mobile / API** — JWT Bearer (30-minute access token, 7-day refresh) with transparent refresh in the mobile client. **API endpoints use `[ApiAuthorize]`** which pins the auth scheme to Bearer so mobile clients get proper 401/403, not a cookie 302.
- **OAuth** — Google via standard OAuth 2.0 callback.

### Token persistence (mobile)

The mobile `AuthService` writes both tokens to platform `SecureStorage` on login. If either write fails, the login is rolled back and the user sees a localised `Login_Error_Persistence` message — there is no phantom logged-in state.

### Refresh-token rotation

Each refresh issues a new pair and increments `ApplicationUser.RefreshTokenVersion`. Refresh tokens carry the version they were issued under; mismatches are treated as replay/stale and rejected. This forecloses the "stolen refresh token can be re-used" class of attack.

### Role consistency

The codebase carries two role representations: `ApplicationUser.Role` (enum on the user document) and Identity's role-claim store. `UserRoleSync.SetUserRoleAsync` is the only correct way to mutate roles — it updates both atomically. The startup `AuditRoleDriftAsync` audit logs warnings for any users where the two disagree (stale data from earlier bugs).

### Rate limiting

| Endpoint group | Limit |
|---|---|
| Auth endpoints | 5 req / 15 min |
| General API | 60 req / min |
| Reporting & analytics | 30 req / min |
| File uploads | 10 req / min |

### Security headers

Responses include `X-Frame-Options`, `X-Content-Type-Options`, `X-XSS-Protection`, `Referrer-Policy`, `Permissions-Policy`, and a configurable `Content-Security-Policy`. `Permissions-Policy` disables camera/microphone/payment and allows `geolocation=(self)` so the hazard map can centre on the visitor (note: browsers only expose geolocation on a secure origin — HTTPS or `localhost`). The CSP currently retains `'unsafe-inline'` for `script-src` because of a `<script type="application/json">` config block in `Safety/HazardMode.cshtml`; tightening via per-request nonces is a tracked follow-up.

### Audit logging

`IAuditService` (Mongo-backed via `MongoDbAuditService`) records every admin action with event type, resource, actor, IP, user agent, and before/after values. The `audit-logs` collection has a 3-year TTL index. Audit failures log via `ILogger` (not stdout) so a broken pipeline surfaces in observability.

---

## Testing

```bash
# Backend — unit tests (245)
dotnet test FixIt.Tests/FixIt.Tests.csproj --filter "FullyQualifiedName!~Integration"

# Backend — integration tests (require Docker)
dotnet test FixIt.Tests/FixIt.Tests.csproj --filter "FullyQualifiedName~Integration"

# Mobile (view models, services, converters)
dotnet test FixIt.Mobile.Tests/FixIt.Mobile.Tests.csproj
```

### Integration tests

Backend integration tests in `FixIt.Tests/Integration/` use `WebApplicationFactory<Program>` against a real MongoDB started via `Testcontainers.MongoDb`. They require a running Docker daemon; when Docker isn't available they **skip cleanly** (via `Xunit.SkippableFact`) rather than failing — so local dev without Docker doesn't break `dotnet test`. CI on `ubuntu-latest` always has Docker.

Currently covered:

- Admin bootstrap → `/api/auth/login` → JWT carries the `Admin` role claim.
- Regular-user JWT does not carry the `Admin` role claim.
- `[ApiAuthorize]` HTTP roundtrip: admin → 200, regular user → 403, no auth → 401.

### Coverage gates

`codecov.yml` defines the project-level (no decrease, 1% threshold) and patch-level (70% target, 5% threshold) status checks. A PR that drops coverage or adds untested code fails the Codecov check and blocks merge.

### Test layout

```
FixIt.Tests/                    # Backend
├── Integration/                # WebApplicationFactory + Testcontainers
├── Services/                   # IssueService, CommentService, JwtTokenService, ...
├── Controllers/                # Controller tests
├── Data/                       # Repository tests
├── Middleware/                 # Middleware tests
└── Security/                   # Authorize-attribute reflection tests

FixIt.Mobile.Tests/             # Mobile (plain net9.0 — no MAUI framework)
├── ViewModels/                 # Report, Issues, Home, Login view models
├── Services/                   # Localization, converters
└── TestSupport/                # MAUI type stubs so the VMs compile under net9.0
```

---

## Deployment & Ops

### Docker (self-host)

```bash
# Development
docker compose up -d

# Production
cp .env.production.example .env.production
docker compose -f docker-compose.yml -f docker-compose.prod.yml \
  --env-file .env.production up -d
```

### Railway (current production)

The live deployment runs on Railway against MongoDB Atlas. Required env vars are documented in [`SECRETS.md`](./SECRETS.md).

After a fresh-DB deploy, bootstrap the admin once:

```bash
railway run dotnet run --project FixIt -- --bootstrap-admin --email admin@you.com --password '...'
```

### Operational scripts

```bash
# Pre-deploy env validation
scripts/ops/preflight.sh .env.production

# Smoke test a running instance
scripts/ops/smoke.sh https://fixitfull-production.up.railway.app

# Full release gate (build + test + smoke + load + backup)
scripts/ops/release-gate.sh .env.production

# Database backup
FIXIT_ENV_FILE=.env.production scripts/ops/mongo-backup.sh ./backups

# Clear test data from a Mongo (dry-run by default; preserves cities/tags/migrations)
DRY_RUN=1 mongosh "$MONGODB_URI" --file scripts/ops/prod-clean.js
DRY_RUN=0 mongosh "$MONGODB_URI" --file scripts/ops/prod-clean.js
```

### Production checklist

- `ASPNETCORE_ENVIRONMENT=Production`
- `JWT_SECRET_KEY` ≥ 64 random characters
- `MONGODB_URI` and `MONGODB_DATABASE` set explicitly (don't rely on URI default DB)
- Google OAuth credentials set
- `DATA_PROTECTION_KEY_RING_PATH` configured (otherwise auth cookies invalidate on container restart)
- SMTP configured for transactional email
- After deploy: run `--bootstrap-admin` to provision the initial admin

### Further docs

| Doc | Purpose |
|---|---|
| [`DOCKER.md`](./DOCKER.md) | Container setup + troubleshooting |
| [`SECRETS.md`](./SECRETS.md) | Secrets management across environments |
| [`PRODUCTION_READINESS.md`](./PRODUCTION_READINESS.md) | Implementation status |
| [`BETA_RELEASE_CHECKLIST.md`](./BETA_RELEASE_CHECKLIST.md) | Go / no-go checklist |
| [`MONGODB_SSL_TROUBLESHOOTING.md`](./MONGODB_SSL_TROUBLESHOOTING.md) | TLS connection issues |
| [`.github/CI-CD.md`](./.github/CI-CD.md) | GitHub Actions workflows |
| [`FixIt.Data/Infrastructure/Migrations/MIGRATIONS.md`](./FixIt.Data/Infrastructure/Migrations/MIGRATIONS.md) | Database migration guide |

---

## Contributing

1. Fork the repository.
2. Create a feature branch: `git checkout -b feature/my-feature`.
3. Run `dotnet test` (unit + integration if Docker available) before pushing.
4. Open a Pull Request — Codecov status checks will gate coverage; the dotnet workflow will gate build + vulnerable-package checks.

See [`CONTRIBUTING.md`](./CONTRIBUTING.md) for coding standards. Database schema changes require a migration — see the migrations guide linked above. Role-related changes should go through `UserRoleSync.SetUserRoleAsync`; bypassing it reintroduces the enum/claim drift bug class.

---

## License

MIT — see [`LICENSE`](./LICENSE) for details.
