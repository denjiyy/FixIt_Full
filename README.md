# FixIt — Civic Engagement Platform

FixIt is a full-stack civic engagement platform that lets citizens report local infrastructure issues and safety hazards, track resolution progress, and earn reputation through community participation. It ships as both a web application and a native mobile app for iOS and Android, served by a single REST API.

![.NET](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet)
![ASP.NET Core](https://img.shields.io/badge/ASP.NET%20Core-9.0-black?logo=dotnet)
![MAUI](https://img.shields.io/badge/.NET%20MAUI-iOS%20%7C%20Android-512BD4?logo=dotnet)
![MongoDB](https://img.shields.io/badge/MongoDB-Atlas-47A248?logo=mongodb)
![Railway](https://img.shields.io/badge/Deployed%20on-Railway-0B0D0E?logo=railway)

Live at **[fixit-production-202d.up.railway.app](https://fixit-production-202d.up.railway.app)**.

---

## Table of Contents

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
- [Gamification System](#gamification-system)
- [AI Features](#ai-features)
- [Security](#security)
- [Testing](#testing)
- [Deployment](#deployment)
- [Contributing](#contributing)
- [License](#license)

---

## Overview

FixIt connects citizens with local government by giving them a straightforward way to report infrastructure problems — potholes, broken streetlights, graffiti, flooding — and track them through to resolution. The platform pairs traditional issue tracking with AI-assisted analysis, a gamification layer that rewards civic participation, and real-time safety alerts for the community.

Web and mobile share the same REST API, the same domain models, and the same business-logic services. The two clients differ only in the presentation layer.

---

## Features

### Core

| Feature | Description |
|---|---|
| **Issue reporting** | Photos, geocoded location, descriptions, optional anonymity |
| **Safety hazards** | Hazard reporting with severity levels and community confirmation |
| **Voting** | Upvote / downvote to surface the most urgent community concerns |
| **Comments** | Threaded discussions on issues with media attachments |
| **Official responses** | Government entities can post status updates and resolution timelines |
| **Map picker** | Pin location on a Leaflet map; reverse-geocoded to street address |
| **Heatmaps** | Geospatial visualisation of issue clusters and hotspots |

### Advanced

| Feature | Description |
|---|---|
| **Gamification** | Reputation points, achievements, four-tier trust levels, leaderboards |
| **AI analysis** | Automatic categorisation, duplicate detection, priority suggestion via OpenAI |
| **Health reports** | City-level metrics: resolution rates, response times, hazard density |
| **Multi-language** | Localised UI in English (`en`) and Bulgarian (`bg`) |
| **OAuth / JWT** | Google sign-in for the web; JWT bearer tokens for the mobile app |
| **Rate limiting** | Per-endpoint limits with sliding-window enforcement |
| **Audit logging** | Comprehensive admin-action audit trail |

### Mobile (FixIt.Mobile)

| Feature | Description |
|---|---|
| **Native iOS & Android** | One .NET MAUI codebase for both platforms |
| **Map-based location picker** | Tap a Leaflet map to drop a pin; reverse geocoded to address + city |
| **Auto-geolocation** | Asks for location permission on first Report-Issue visit, centres the map there |
| **Multi-photo capture** | Take photos with the camera, pick from gallery, remove, up to 5 per report; photos are optional |
| **Adaptive theming** | Light / dark / system theme preference with adaptive iOS status bar |
| **JWT auth** | Email + password login with refresh tokens; tokens stored in platform SecureStorage |
| **Offline-aware** | API calls fail gracefully with localised error states |

---

## Tech Stack

### Web Backend

| Component | Technology |
|---|---|
| Framework | ASP.NET Core 9.0 |
| Language | C# 13 |
| Database | MongoDB Atlas |
| Identity | ASP.NET Core Identity + AspNetCore.Identity.Mongo 9.0.0 |
| Authentication | Cookie session (web) + JWT Bearer (mobile/API) |
| OAuth | Google |
| AI | OpenAI API (`gpt-4o-mini`) |
| Email | SMTP (SendGrid / Gmail) |
| Driver | MongoDB.Driver 2.30.0 |

### Web Frontend

| Component | Technology |
|---|---|
| UI | Razor Pages + MVC |
| Maps | Leaflet.js + OpenStreetMap |
| Styling | Custom CSS with design tokens + Bootstrap utilities |
| JavaScript | Vanilla ES6+ modules |
| Charts | Custom SVG/CSS charts |

### Mobile

| Component | Technology |
|---|---|
| Framework | .NET MAUI 9.0 |
| Platforms | iOS 15+, Android 5+ |
| Architecture | MVVM with CommunityToolkit.Mvvm |
| UI Toolkit | CommunityToolkit.Maui |
| HTTP | `Microsoft.Extensions.Http` with `DelegatingHandler` for auth + refresh |
| Storage | `SecureStorage` (JWT + refresh tokens) |
| Camera / gallery | `MediaPicker.Default` |
| Geolocation | `Geolocation.Default` + `Permissions` |
| Maps | Leaflet inside `WebView`, with a `fixit://` URL bridge for taps |

### DevOps

| Component | Technology |
|---|---|
| Hosting | Railway |
| Containerisation | Docker (multi-stage) |
| API Docs | Swagger / OpenAPI |
| Compression | Brotli + Gzip |
| Caching | MemoryCache + OutputCache |

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
              │ HTTP / Cookie session             │ HTTP / JWT
┌─────────────▼──────────────────────────────────▼──────────────┐
│                      Presentation Layer                        │
│  Razor Pages  |  REST API Controllers  |  Admin Area          │
└──────────────────────────────┬────────────────────────────────┘
                                │
┌──────────────────────────────▼────────────────────────────────┐
│                       Service Layer                            │
│  Issues | Safety | AI | Gamification | Auth | Analytics |     │
│  Media | Geocoding | Reputation                               │
└──────────────────────────────┬────────────────────────────────┘
                                │
┌──────────────────────────────▼────────────────────────────────┐
│                        Data Layer                              │
│  Repository<T>  |  MongoDbContext  |  MigrationRunner         │
└──────────────────────────────┬────────────────────────────────┘
                                │
┌──────────────────────────────▼────────────────────────────────┐
│                      MongoDB Atlas                              │
│  Issues | Users | Hazards | Comments | Votes | Media | ...    │
└──────────────────────────────────────────────────────────────┘
```

---

## Project Structure

```
FixIt/
├── FixIt/                          # Web application + REST API
│   ├── Areas/                      # Admin & Identity areas
│   ├── Controllers/                # REST API controllers
│   │   ├── AnalysisController.cs       # AI analysis
│   │   ├── AuditLogsController.cs
│   │   ├── AuthController.cs           # /api/auth/* (JWT login, refresh)
│   │   ├── GeocodingController.cs      # /api/geocoding/reverse
│   │   ├── HealthController.cs         # Liveness / readiness
│   │   ├── IssuesController.cs         # CRUD + /api/issues/{id}/media
│   │   ├── MediaController.cs          # Media stream / download
│   │   ├── SafetyController.cs
│   │   ├── SuggestionsController.cs
│   │   ├── TagsController.cs
│   │   └── UsersController.cs
│   ├── Extensions/                 # AuthExtensions, IdentityExtensions, ...
│   ├── Pages/                      # Razor pages (Web UI)
│   ├── Middleware/                 # Custom middleware
│   ├── Resources/                  # Localisation files (en, bg)
│   ├── wwwroot/                    # Static assets (CSS, JS, lib, uploads)
│   └── Program.cs                  # Entry point & DI composition
│
├── FixIt.Mobile/                   # .NET MAUI mobile app (iOS & Android)
│   ├── Views/                      # XAML pages
│   ├── ViewModels/                 # MVVM view models
│   ├── Services/
│   │   ├── ApiService.cs               # HTTP client wrapper for the REST API
│   │   ├── AuthService.cs              # JWT login / refresh / SecureStorage
│   │   ├── MapHtmlBuilder.cs           # Leaflet HTML for issue + picker maps
│   │   ├── ThemePreferenceService.cs   # Light / dark / system theming
│   │   └── Contracts/
│   ├── Models/                     # Mobile-side DTOs (PhotoAttachment, ...)
│   ├── Controls/                   # Reusable XAML controls
│   ├── Platforms/
│   │   ├── iOS/
│   │   │   ├── Info.plist              # Camera, photo, location strings
│   │   │   ├── Entitlements.plist      # keychain-access-groups (device builds)
│   │   │   └── StatusBar.iOS.cs        # Adaptive status-bar style
│   │   └── Android/
│   │       └── AndroidManifest.xml     # Camera + fine/coarse location permissions
│   ├── Resources/
│   │   ├── Images/                     # SVG sources → MAUI-generated PNGs
│   │   └── Strings/AppStrings.[bg.]resx
│   ├── AppShell.xaml               # Bottom tab bar with elevated centre FAB
│   └── MauiProgram.cs              # DI, HttpClient, toolkit setup
│
├── FixIt.Mobile.Tests/             # xUnit tests for the mobile project
├── FixIt.Models/                   # Domain models & enums (shared)
├── FixIt.Services/                 # Business logic (shared)
├── FixIt.Data/                     # Repositories, migrations, MongoDB context
├── FixIt.ViewModels/               # DTOs, responses, MapperExtensions
├── FixIt.Tests/                    # Backend unit & integration tests
├── Dockerfile                      # Multi-stage Docker build
└── FixIt.sln
```

---

## Getting Started

### Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [MongoDB](https://www.mongodb.com/try/download/community) (v6.0+) — local or MongoDB Atlas
- [Git](https://git-scm.com/)
- macOS + Xcode (for iOS mobile development) — optional
- Android SDK (for Android mobile development) — optional

### Web App

```bash
git clone https://github.com/denjiyy/FixIt.git
cd FixIt

# Configure environment
cp .env.example .env
# Edit .env with your MongoDB URI, JWT secret, OAuth, etc. (see Configuration)

# Restore & run
dotnet restore
dotnet run --project FixIt
```

Once running:

- Web:     http://localhost:5092
- API:     http://localhost:5092/api
- Swagger: http://localhost:5092/swagger

### Development Admin Account

Add to `appsettings.Development.json`:

```json
{
  "Database": {
    "EnableDevelopmentAdminSeed": true,
    "DevelopmentAdmin": {
      "Email": "admin@fixit.local",
      "UserName": "admin",
      "DisplayName": "Admin User",
      "Password": "YourSecurePassword123!"
    }
  }
}
```

The admin account is created on startup with the `Admin` role when the seed flag is enabled.

---

## Mobile App

`FixIt.Mobile` is a .NET MAUI app targeting iOS and Android. It talks to the same Railway-hosted REST API as the web app using JWT Bearer tokens.

### Report-Issue flow

1. The user opens **Report Issue** from the elevated centre FAB.
2. The app requests location permission. If granted it centres the Leaflet map on the device's coordinates, otherwise it falls back to Sofia and asks the user to tap the map.
3. Each tap on the map drops the pin, updates Lat/Lon, and reverse-geocodes to fill the address and city automatically.
4. The native camera launches once per session so the user can photograph the issue. Cancelling returns to the form without losing context.
5. The photo tile lets the user **Take Photo**, **Pick from Gallery**, **Remove**, or skip photos entirely — photos are optional, capped at five.
6. Submitting first POSTs `/api/issues` (JSON) and then uploads each photo to `/api/issues/{id}/media` (multipart). Partial-upload failures surface as a warning; the issue itself is created regardless.

### Running on iOS Simulator

```bash
dotnet build FixIt.Mobile/FixIt.Mobile.csproj -f net9.0-ios -c Debug
```

Or launch the iOS simulator target from Visual Studio for Mac / Rider / VS Code's MAUI integration. Xcode must be installed; a free Apple ID is sufficient for simulator builds.

The simulator skips the `Entitlements.plist` keychain-access-group (it would otherwise require a provisioning profile). On device, the entitlement is applied automatically and SecureStorage uses the bundle keychain group.

### Running on Android

```bash
dotnet build FixIt.Mobile/FixIt.Mobile.csproj -f net9.0-android -c Debug
```

Requires the Android SDK to be installed and `AndroidSdkDirectory` set (Visual Studio / Rider handle this automatically).

### Authentication

The mobile app authenticates against `/api/auth/login` and stores the access and refresh tokens in platform `SecureStorage`. An `AuthHeaderHandler` attaches the access token as a Bearer header on every API call, transparently refreshes it on a 401, and signs the user out if the refresh fails. Login fails cleanly with a localised error if token persistence ever fails — the user is never left in a phantom logged-in state.

---

## Configuration

### Environment Variables

| Variable | Description | Example |
|---|---|---|
| `MONGODB_URI` | MongoDB Atlas connection string | `mongodb+srv://user:pass@cluster.mongodb.net/` |
| `MONGODB_DATABASE` | Database name | `fixit` |
| `JWT_SECRET_KEY` | JWT signing key (min 32 chars) | `your-64-char-random-secret` |
| `GOOGLE_CLIENT_ID` | Google OAuth client ID | `123456.apps.googleusercontent.com` |
| `GOOGLE_CLIENT_SECRET` | Google OAuth secret | `GOCSPX-…` |
| `OPENAI_API_KEY` | OpenAI API key (optional — disables AI if unset) | `<openai-api-key>` |
| `AllowedHosts` | Accepted hostnames | `*` |
| `SMTP_HOST` | SMTP server | `smtp.gmail.com` |
| `SMTP_PORT` | SMTP port | `587` |
| `SMTP_USERNAME` | SMTP username | `noreply@fixit.app` |
| `SMTP_PASSWORD` | SMTP password | `…` |
| `DATA_PROTECTION_KEY_RING_PATH` | Key storage path | `/app/data-protection-keys` |

### appsettings.json (excerpt)

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
    "RateLimiting": {
      "Enabled": true,
      "RequestsPerMinute": 60
    }
  }
}
```

---

## API Reference

### Authentication

| Method | Endpoint | Description | Auth |
|---|---|---|---|
| `POST` | `/api/auth/login` | Email + password login (JWT) | No |
| `POST` | `/api/auth/register` | New user registration | No |
| `POST` | `/api/auth/refresh` | Refresh access token | No |
| `POST` | `/api/auth/logout` | Sign out | Yes |
| `GET` | `/api/auth/user` | Current user info | Yes |

### Issues

| Method | Endpoint | Description | Auth |
|---|---|---|---|
| `POST` | `/api/issues` | Create issue (JSON: title, description, lat, lon, cityId, address) | Yes |
| `POST` | `/api/issues/{id}/media` | Upload a media file (`multipart/form-data`, field `file`) | Yes (reporter or admin) |
| `GET` | `/api/issues/{id}` | Get issue details | No |
| `GET` | `/api/issues/city/{cityId}` | Issues by city | No |
| `POST` | `/api/issues/city/{cityId}/search` | Search issues | No |
| `GET` | `/api/issues/my-issues` | Current user's issues | Yes |
| `PUT` | `/api/issues/{id}/status` | Update status | Mod / Admin |
| `PUT` | `/api/issues/{id}/priority` | Update priority | Mod / Admin |
| `POST` | `/api/issues/{id}/vote` | Vote up / down | Yes |
| `DELETE` | `/api/issues/{id}/vote` | Remove vote | Yes |
| `DELETE` | `/api/issues/{id}` | Soft-delete | Owner / Admin |
| `POST` | `/api/issues/{id}/comments` | Add comment | Yes |
| `GET` | `/api/issues/{id}/comments` | Get comments | No |
| `POST` | `/api/issues/{id}/comments/{commentId}/like` | Like comment | Yes |
| `POST` | `/api/issues/{id}/comments/{commentId}/dislike` | Dislike comment | Yes |

`IssueDetailResponse` and `IssueSummaryResponse` both expose `latitude` and `longitude` derived from the entity's GeoJSON point, so map renderers don't need to look up the location separately.

### Safety

| Method | Endpoint | Description | Auth |
|---|---|---|---|
| `GET` | `/api/safety/nearby-hazards` | Nearby hazards | No |
| `GET` | `/api/safety/critical-hazards` | Critical hazards | No |
| `POST` | `/api/safety/report` | Report a hazard | Yes |
| `POST` | `/api/safety/{id}/confirm` | Confirm a hazard | Yes |
| `POST` | `/api/safety/{id}/resolve` | Resolve a hazard | Admin |

### Geocoding

| Method | Endpoint | Description | Auth |
|---|---|---|---|
| `GET` | `/api/geocoding/reverse?latitude={lat}&longitude={lng}` | Address + city for a coordinate; cached server-side | No |

### AI

| Method | Endpoint | Description | Auth |
|---|---|---|---|
| `POST` | `/api/analysis/analyze/{issueId}` | Analyse an issue | No |
| `POST` | `/api/analysis/issue-draft-suggestions` | Title + description → category/priority/department draft | Yes |
| `GET` | `/api/issues/{id}/analysis` | Poll latest analysis (204 while pending) | No |
| `POST` | `/api/suggestions/pending` | Pending moderation suggestions | Admin |

### Media

| Method | Endpoint | Description | Auth |
|---|---|---|---|
| `GET` | `/api/media/{id}` | Stream a media file (image / video) | No |

Full interactive docs available at `/swagger` on any running instance.

---

## Database Schema

### Collections

| Collection | Description |
|---|---|
| `users` | `ApplicationUser` — email, roles, reputation, anonymous-reporting flag |
| `issues` | Issue reports with GeoJSON location + optional address |
| `hazards` | Safety hazards with severity and confirmation count |
| `comments` | Threaded comments on issues |
| `votes` | Issue votes (unique compound index on `IssueId + UserId`) |
| `media` | Uploaded media metadata (storage path / Cloudinary URL) |
| `media_references` | Backlinks from media → issue or comment |
| `tags` | Issue tags with usage counts |
| `cities` | City configurations with coordinates |
| `user_reputations` | Per-user reputation profiles |
| `reputation_transactions` | Full points history |
| `leaderboards` | Weekly / monthly leaderboard snapshots |
| `issue_analyses` | AI analysis results |
| `audit_logs` | Admin-action audit trail |

### Key Relationships

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

---

## Gamification System

### Trust Levels

| Level | Name | Points | Permissions |
|---|---|---|---|
| 0 | New | 0–10 | Basic reporting |
| 1 | Active | 11–50 | Increased vote weight |
| 2 | Trusted | 51–150 | Priority vote weight, profile badge |
| 3 | Leader | 150+ | Maximum vote weight, leaderboard featured |

### Reputation Points

| Action | Points |
|---|---|
| Report a new issue | +5 |
| Issue confirmed by community | +3 |
| Post a comment | +2 |
| Receive an upvote | +1 |
| Issue marked resolved | +15 |
| Confirm a safety hazard | +5 |

### Achievements

| Achievement | Requirement | Points |
|---|---|---|
| FirstReporter | Report first issue | 10 |
| HelpfulCommenter | Post 10 comments | 15 |
| CommunityHelper | Receive 50 upvotes | 25 |
| IssueSolver | 5 issues resolved | 30 |
| CivicContributor | Reach Trust Level 2 | 20 |
| CommunityChampion | Reach Trust Level 3 | 50 |
| CivicLeader | Top of weekly leaderboard | 40 |
| AccurateReporter | 90 %+ issues confirmed | 35 |
| VerifiedCitizen | Verify email + phone | 15 |

---

## AI Features

When OpenAI is enabled, the AI system runs automatically on new issue reports via a background queue:

- **Categorisation** — Infrastructure, Public Safety, Environmental Health, Parks, Transportation, Utilities, Sanitation, Public Health, Other
- **Priority suggestion** — Low / Medium / High / Critical
- **Duplicate detection** — compares against existing open issues nearby
- **Keyword extraction** — improves searchability
- **Tag suggestions** — drawn from the live tag database

Additional AI capabilities:

| Feature | Description |
|---|---|
| `SuggestIssueDraft` | Category, priority, and department from title + description |
| `SummarizeIssueThread` | Summary of the issue and all comments (streaming) |
| `GenerateHazardInsight` | Pattern analysis across hazard clusters |
| `TranslateIssueFilter` | Natural-language query → structured filter |

Deterministic fallbacks are used when the OpenAI API is unavailable or disabled.

---

## Security

### Authentication flows

- **Web** — Cookie-based session with anti-forgery tokens
- **Mobile / API** — JWT Bearer (30-minute access token + 7-day refresh) with transparent refresh in the mobile client
- **OAuth** — Google via standard OAuth 2.0 callback

### Token persistence (mobile)

The mobile `AuthService` writes both tokens to platform `SecureStorage` on login. If either write fails, the login is rolled back and the user sees a clear `Login_Error_Persistence` message — there is no "phantom logged-in" state. On iOS device builds, the bundle's `Entitlements.plist` grants the keychain-access-group needed for SecureStorage to succeed.

### Rate limiting

| Endpoint group | Limit |
|---|---|
| Auth endpoints | 5 req / 15 min |
| General API | 60 req / min |
| Reporting & analytics | 30 req / min |
| File uploads | 10 req / min |

### Security headers

All responses include `X-Frame-Options`, `X-Content-Type-Options`, `X-XSS-Protection`, `Referrer-Policy`, and a configurable `Content-Security-Policy`.

### Audit logging

Every admin action is logged with event type, resource, actor, IP address, user agent, and before / after values.

---

## Testing

```bash
# Backend (unit + integration)
dotnet test FixIt.Tests/FixIt.Tests.csproj

# Mobile (view models, services, converters)
dotnet test FixIt.Mobile.Tests/FixIt.Mobile.Tests.csproj

# Everything in the solution
dotnet test
```

### Test layout

```
FixIt.Tests/                    # Backend
├── Services/                   # IssueService, CivicAiService, JwtTokenService, ...
├── Controllers/                # Controller tests
├── Data/                       # Repository tests
├── Middleware/                 # Middleware tests
└── Security/                   # Security tests

FixIt.Mobile.Tests/             # Mobile (plain net9.0 — no MAUI framework)
├── ViewModels/                 # Report, Issues, Home, Login view models
├── Services/                   # Localization, converters
└── TestSupport/                # MAUI type stubs so the VMs compile under net9.0
```

The backend tests use Moq + xUnit and run against in-memory mocks of `IRepository<T>`. A running MongoDB instance is **not** required for the test suite — only the development app needs one.

---

## Deployment

### Docker (recommended for self-hosting)

```bash
# Development
docker compose up -d

# Production
cp .env.production.example .env.production
docker compose -f docker-compose.yml -f docker-compose.prod.yml \
  --env-file .env.production up -d
```

### Railway (current production)

The live deployment runs on Railway with MongoDB Atlas.

Required Railway environment variables:

```
MONGODB_URI            = mongodb+srv://...
MONGODB_DATABASE       = fixit
JWT_SECRET_KEY         = <64-char random secret>
GOOGLE_CLIENT_ID       = ...
GOOGLE_CLIENT_SECRET   = ...
OPENAI_API_KEY         = <openai-api-key>        # optional
AllowedHosts           = *
ASPNETCORE_ENVIRONMENT = Production
```

### Production checklist

- [ ] `ASPNETCORE_ENVIRONMENT=Production`
- [ ] `JWT_SECRET_KEY` is at least 64 random characters
- [ ] MongoDB Atlas IP Access List includes `0.0.0.0/0` or Railway egress IPs
- [ ] Google OAuth credentials set
- [ ] `AllowedHosts=*` (or your specific domain)
- [ ] SMTP configured for transactional email
- [ ] Rate limiting enabled
- [ ] Audit logging active

### Operational scripts

```bash
# Preflight validation
scripts/ops/preflight.sh .env.production

# Full release gate (build, test, smoke, load, backup)
scripts/ops/release-gate.sh .env.production

# Smoke test a running instance
scripts/ops/smoke.sh https://fixit-production-202d.up.railway.app

# Database backup
FIXIT_ENV_FILE=.env.production scripts/ops/mongo-backup.sh ./backups
```

### Further docs

- [`DOCKER.md`](./DOCKER.md) — container setup and troubleshooting
- [`SECRETS.md`](./SECRETS.md) — secrets management across environments
- [`PRODUCTION_READINESS.md`](./PRODUCTION_READINESS.md) — implementation status
- [`BETA_RELEASE_CHECKLIST.md`](./BETA_RELEASE_CHECKLIST.md) — go / no-go checklist
- [`.github/CI-CD.md`](./.github/CI-CD.md) — GitHub Actions workflows
- [`MIGRATIONS.md`](./FixIt.Data/Infrastructure/Migrations/MIGRATIONS.md) — database migration guide

---

## Contributing

1. Fork the repository.
2. Create a feature branch: `git checkout -b feature/my-feature`.
3. Run the test suite (`dotnet test`) before pushing.
4. Commit: `git commit -m "Add my feature"`.
5. Push: `git push origin feature/my-feature`.
6. Open a Pull Request.

See [`CONTRIBUTING.md`](./CONTRIBUTING.md) for coding standards, testing requirements, and the PR process. Database schema changes require a migration — see the migrations guide linked above.

---

## License

MIT — see [`LICENSE`](./LICENSE) for details.
