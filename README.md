# FixIt — Civic Engagement Platform

FixIt is a full-stack civic engagement platform that lets citizens report local infrastructure issues and safety hazards, track resolution progress, and earn reputation through community participation. It ships as both a web application and a native mobile app for iOS and Android.

![.NET](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet)
![MongoDB](https://img.shields.io/badge/MongoDB-Atlas-47A248?logo=mongodb)
![ASP.NET Core](https://img.shields.io/badge/ASP.NET%20Core-9.0-black?logo=dotnet)
![MAUI](https://img.shields.io/badge/.NET%20MAUI-iOS%20%7C%20Android-512BD4?logo=dotnet)
![Railway](https://img.shields.io/badge/Deployed%20on-Railway-0B0D0E?logo=railway)

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

FixIt connects citizens with local government by giving them a straightforward way to report infrastructure problems — potholes, broken streetlights, graffiti, flooding — and track them through to resolution. The platform pairs traditional issue tracking with AI-powered analysis, a gamification layer that rewards civic participation, and real-time safety alerts for the community.

The platform is live at **[fixit-production-202d.up.railway.app](https://fixit-production-202d.up.railway.app)**.

---

## Features

### Core

| Feature | Description |
|---------|-------------|
| **Issue Reporting** | Report infrastructure problems with photos, location data, and descriptions |
| **Safety Hazards** | Real-time hazard reporting with severity levels and community confirmation |
| **Voting System** | Upvote/downvote issues to surface the most urgent community concerns |
| **Comments & Discussion** | Threaded discussions on issues with media attachments |
| **Official Responses** | Government entities can post status updates and resolution timelines |
| **Anonymous Reporting** | User-controlled privacy setting for anonymous submissions |

### Advanced

| Feature | Description |
|---------|-------------|
| **Gamification** | Reputation points, achievements, trust levels, and leaderboards |
| **AI Analysis** | Automatic categorisation, duplicate detection, and priority suggestions via OpenAI |
| **Heatmaps** | Geospatial visualisation of issue clusters and hotspots |
| **Health Reports** | City-level metrics including resolution rates and response times |
| **Multi-language** | Localised UI in English (en-US) and Bulgarian (bg-BG) |
| **OAuth** | Google, Microsoft, and Facebook login; JWT support for mobile clients |
| **Audit Logging** | Comprehensive security and compliance audit trail |
| **Rate Limiting** | DDoS protection with configurable limits per endpoint |

### Mobile (FixIt.Mobile)

| Feature | Description |
|---------|-------------|
| **Native iOS & Android** | Built with .NET MAUI, single codebase for both platforms |
| **Camera Integration** | Tap the centre dock button to photograph an issue and go straight to the report form |
| **Bottom Navigation** | Custom tab bar with elevated centre camera button; camera only shown when logged in |
| **JWT Auth** | Email/password login with tokens stored in platform SecureStorage |
| **Issue Feed** | Browse and filter community issues from the mobile app |
| **Offline-friendly** | API calls fail gracefully with user-visible error states |

---

## Tech Stack

### Web Backend

| Component | Technology |
|-----------|------------|
| Framework | ASP.NET Core 9.0 |
| Language | C# 13 |
| Database | MongoDB Atlas |
| Identity | ASP.NET Core Identity + AspNetCore.Identity.Mongo 9.0.0 |
| Authentication | Cookie-based + JWT Bearer |
| OAuth | Google, Microsoft, Facebook |
| AI | OpenAI API (gpt-4o-mini) |
| Email | SMTP (SendGrid / Gmail) |
| Driver | MongoDB.Driver 2.30.0 |

### Web Frontend

| Component | Technology |
|-----------|------------|
| UI | Razor Pages + MVC |
| Maps | Leaflet.js + OpenStreetMap |
| Styling | Custom CSS with design tokens + Bootstrap |
| JavaScript | Vanilla ES6+ modules |
| Charts | Custom SVG/CSS charts |

### Mobile

| Component | Technology |
|-----------|------------|
| Framework | .NET MAUI 9.0 |
| Platforms | iOS 14+, Android 10+ |
| Architecture | MVVM with CommunityToolkit.Mvvm |
| UI Toolkit | CommunityToolkit.Maui |
| HTTP | Microsoft.Extensions.Http |
| Storage | SecureStorage (JWT tokens) |
| Camera | MediaPicker.Default (built-in MAUI API) |

### DevOps

| Component | Technology |
|-----------|------------|
| Hosting | Railway |
| Containerisation | Docker (multi-stage) |
| Version Control | Git / GitHub |
| API Docs | Swagger / OpenAPI |
| Compression | Brotli + Gzip |
| Caching | MemoryCache + OutputCache |

---

## Architecture

FixIt follows a layered architecture with clear separation of concerns. The mobile app communicates with the same REST API used by the web frontend.

```
┌──────────────────────────────────────────────────────────────┐
│                      Clients                                  │
│   ┌─────────────────────┐     ┌─────────────────────────┐   │
│   │   Web Browser        │     │   FixIt.Mobile (MAUI)   │   │
│   │ (Razor Pages / MVC)  │     │   iOS  |  Android        │   │
│   └──────────┬──────────┘     └────────────┬────────────┘   │
└──────────────┼──────────────────────────────┼────────────────┘
               │  HTTP/Cookie                  │  HTTP/JWT
┌──────────────▼──────────────────────────────▼────────────────┐
│                  Presentation Layer                            │
│   Razor Pages  |  API Controllers  |  Admin Area              │
└─────────────────────────────┬────────────────────────────────┘
                               │
┌─────────────────────────────▼────────────────────────────────┐
│                  Services Layer                                │
│   Issues | Safety | AI | Gamification | Auth | Analytics      │
└─────────────────────────────┬────────────────────────────────┘
                               │
┌─────────────────────────────▼────────────────────────────────┐
│                  Data Layer                                    │
│   Repository<T>  |  MongoDbContext  |  MigrationRunner        │
└─────────────────────────────┬────────────────────────────────┘
                               │
┌─────────────────────────────▼────────────────────────────────┐
│                  MongoDB Atlas                                  │
│   Issues | Users | Hazards | Comments | Votes | Media | ...   │
└──────────────────────────────────────────────────────────────┘
```

---

## Project Structure

```
FixIt/
├── FixIt/                        # Web application
│   ├── Areas/                    # Admin & Identity areas
│   ├── Controllers/              # REST API controllers
│   ├── Pages/                    # Razor pages
│   ├── Middleware/               # Custom middleware
│   ├── Resources/                # Localisation files (en-US, bg-BG)
│   ├── wwwroot/                  # Static assets (CSS, JS, lib, uploads)
│   ├── Program.cs                # Application entry point & DI setup
│   └── appsettings.json
│
├── FixIt.Mobile/                 # .NET MAUI mobile app (iOS & Android)
│   ├── Views/                    # XAML pages
│   │   ├── HomePage.xaml
│   │   ├── IssuesPage.xaml
│   │   ├── ReportIssuePage.xaml  # Camera → report flow
│   │   └── LoginPage.xaml
│   ├── ViewModels/               # MVVM view models
│   │   ├── HomeViewModel.cs
│   │   ├── IssuesViewModel.cs
│   │   ├── ReportIssueViewModel.cs
│   │   └── LoginViewModel.cs
│   ├── Services/
│   │   ├── ApiService.cs         # HTTP client wrapper for Railway API
│   │   └── AuthService.cs        # JWT login, SecureStorage
│   ├── Models/
│   │   └── Issue.cs              # Mobile DTO
│   ├── Platforms/
│   │   └── iOS/
│   │       └── Info.plist        # Camera & photo permissions
│   ├── AppShell.xaml             # Shell with custom bottom tab bar
│   └── MauiProgram.cs            # DI, HttpClient, toolkit setup
│
├── FixIt.Models/                 # Domain models & enums (shared)
├── FixIt.Services/               # Business logic (shared)
├── FixIt.Data/                   # Data access — repositories, migrations
├── FixIt.ViewModels/             # DTOs and view models (web)
├── FixIt.Tests/                  # Unit & integration tests
├── Dockerfile                    # Multi-stage Docker build
└── FixIt.sln
```

---

## Getting Started

### Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [MongoDB](https://www.mongodb.com/try/download/community) (v6.0+) or a MongoDB Atlas account
- [Git](https://git-scm.com/)
- macOS + Xcode (for iOS mobile development)

### Web App

```bash
# 1. Clone
git clone https://github.com/denjiyy/FixIt.git
cd FixIt

# 2. Configure environment
cp .env.example .env
# Edit .env with your credentials (see Configuration section)

# 3. Restore & run
dotnet restore
cd FixIt
dotnet run

# 4. Open
# Web:     http://localhost:5092
# API:     http://localhost:5092/api
# Swagger: http://localhost:5092/swagger
```

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

---

## Mobile App

### Overview

`FixIt.Mobile` is a .NET MAUI app targeting iOS and Android. It talks to the same Railway-hosted REST API as the web app using JWT Bearer tokens.

### Key UX: Camera → Report Flow

1. User opens the app and logs in.
2. A prominent circular camera button appears in the centre of the bottom navigation dock.
3. Tapping it immediately opens the device camera.
4. After a photo is captured it is passed directly to the Report Issue page — the user never has to re-select it.
5. The user fills in title, description, category, and location, then submits.

If the user is not logged in, the centre button shows a lock icon and tapping it navigates to the login screen instead.

### Running on iOS Simulator

```bash
# Build for iOS
dotnet build FixIt.Mobile/FixIt.Mobile.csproj -f net9.0-ios -c Debug

# Run on simulator (from Visual Studio for Mac or Rider)
# Select an iOS simulator target and press Run
```

Xcode must be installed. A free Apple ID is sufficient for simulator builds — a paid Developer account is only required for device deployment.

### Running on Android

```bash
dotnet build FixIt.Mobile/FixIt.Mobile.csproj -f net9.0-android -c Debug
```

### Authentication

The mobile app authenticates against `/api/auth/login` and stores the returned JWT in `SecureStorage`. All subsequent API calls include the token as a Bearer header. Tokens expire after 30 minutes; a refresh endpoint is available at `/api/auth/refresh`.

---

## Configuration

### Environment Variables

| Variable | Description | Example |
|----------|-------------|---------|
| `MONGODB_URI` | MongoDB Atlas connection string | `mongodb+srv://user:pass@cluster.mongodb.net/` |
| `MONGODB_DATABASE` | Database name | `fixit` |
| `JWT_SECRET_KEY` | JWT signing key (min 32 chars) | `your-64-char-random-secret` |
| `GOOGLE_CLIENT_ID` | Google OAuth client ID | `123456.apps.googleusercontent.com` |
| `GOOGLE_CLIENT_SECRET` | Google OAuth secret | `GOCSPX-...` |
| `OPENAI_API_KEY` | OpenAI API key | `<openai-api-key>` |
| `AllowedHosts` | Accepted hostnames | `*` |
| `SMTP_HOST` | SMTP server | `smtp.gmail.com` |
| `SMTP_PORT` | SMTP port | `587` |
| `SMTP_USERNAME` | SMTP username | `noreply@fixit.app` |
| `SMTP_PASSWORD` | SMTP password | `...` |
| `DATA_PROTECTION_KEY_RING_PATH` | Key storage path | `/app/data-protection-keys` |

### appsettings.json

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
|--------|----------|-------------|------|
| `POST` | `/api/auth/login/{provider}` | Initiate OAuth login | No |
| `GET` | `/api/auth/signin-callback` | OAuth callback | No |
| `POST` | `/api/auth/logout` | Sign out | Yes |
| `POST` | `/api/auth/refresh` | Refresh JWT | No |
| `GET` | `/api/auth/user` | Current user info | Yes |

### Issues

| Method | Endpoint | Description | Auth |
|--------|----------|-------------|------|
| `POST` | `/api/issues` | Create issue | Yes |
| `GET` | `/api/issues/{id}` | Get issue | No |
| `GET` | `/api/issues/city/{cityId}` | Issues by city | No |
| `POST` | `/api/issues/city/{cityId}/search` | Search issues | No |
| `GET` | `/api/issues/my-issues` | My issues | Yes |
| `PUT` | `/api/issues/{id}/status` | Update status | Mod+ |
| `POST` | `/api/issues/{id}/vote` | Vote | Yes |
| `DELETE` | `/api/issues/{id}/vote` | Remove vote | Yes |
| `DELETE` | `/api/issues/{id}` | Delete | Owner/Admin |
| `POST` | `/api/issues/{id}/comments` | Add comment | Yes |
| `GET` | `/api/issues/{id}/comments` | Get comments | No |

### Safety

| Method | Endpoint | Description | Auth |
|--------|----------|-------------|------|
| `GET` | `/api/safety/nearby-hazards` | Nearby hazards | No |
| `GET` | `/api/safety/critical-hazards` | Critical hazards | No |
| `POST` | `/api/safety/report` | Report hazard | Yes |
| `POST` | `/api/safety/{id}/confirm` | Confirm hazard | Yes |
| `POST` | `/api/safety/{id}/resolve` | Resolve hazard | Admin |

### AI

| Method | Endpoint | Description | Auth |
|--------|----------|-------------|------|
| `POST` | `/api/analysis/analyze/{issueId}` | Analyse issue | No |
| `POST` | `/api/analysis/issue-draft-suggestions` | Draft suggestions | Yes |
| `POST` | `/api/suggestions/pending` | Pending suggestions | Admin |

Full interactive docs available at `/swagger` on any running instance.

---

## Database Schema

### Collections

| Collection | Description |
|------------|-------------|
| `users` | ApplicationUser — email, roles, reputation |
| `issues` | Issue reports with GeoJSON location |
| `hazards` | Safety hazards with severity and confirmation count |
| `comments` | Threaded comments on issues |
| `votes` | Issue votes (unique compound index on IssueId + UserId) |
| `media` | Uploaded file metadata |
| `tags` | Issue tags with usage counts |
| `cities` | City configurations with coordinates |
| `user_reputations` | Reputation profiles |
| `reputation_transactions` | Full points history |
| `leaderboards` | Weekly/monthly leaderboard snapshots |
| `issue_analyses` | AI analysis results |
| `audit_logs` | Admin action audit trail |

### Key Relationships

```
ApplicationUser (1) → (M) Issue
ApplicationUser (1) → (1) UserReputation
Issue           (1) → (M) Comment
Issue           (1) → (M) Vote
Issue           (1) → (1) IssueAnalysis
City            (1) → (M) Issue
City            (1) → (M) Hazard
```

---

## Gamification System

### Trust Levels

| Level | Name | Points | Permissions |
|-------|------|--------|-------------|
| 0 | New | 0–10 | Basic reporting |
| 1 | Active | 11–50 | Increased vote weight |
| 2 | Trusted | 51–150 | Priority vote weight, profile badge |
| 3 | Leader | 150+ | Maximum vote weight, leaderboard featured |

### Reputation Points

| Action | Points |
|--------|--------|
| Report a new issue | +5 |
| Issue confirmed by community | +3 |
| Post a comment | +2 |
| Receive an upvote | +1 |
| Issue marked resolved | +15 |
| Confirm a safety hazard | +5 |

### Achievements

| Achievement | Requirement | Points |
|-------------|-------------|--------|
| FirstReporter | Report first issue | 10 |
| HelpfulCommenter | Post 10 comments | 15 |
| CommunityHelper | Receive 50 upvotes | 25 |
| IssueSolver | 5 issues resolved | 30 |
| CivicContributor | Reach Trust Level 2 | 20 |
| CommunityChampion | Reach Trust Level 3 | 50 |
| CivicLeader | Top of weekly leaderboard | 40 |
| AccurateReporter | 90%+ issues confirmed | 35 |
| VerifiedCitizen | Verify email + phone | 15 |

---

## AI Features

The AI system runs automatically on new issue reports:

- **Categorisation** — Infrastructure, Public Safety, Environmental, etc.
- **Priority suggestion** — Low / Medium / High / Critical
- **Duplicate detection** — compares against existing open issues
- **Keyword extraction** — improves searchability
- **Tag suggestions** — drawn from the live tag database

Additional AI capabilities:

| Feature | Description |
|---------|-------------|
| `SuggestIssueDraft` | Category, priority, and department from title + description |
| `SummarizeIssueThread` | Summary of issue and all comments (streaming) |
| `GenerateHazardInsight` | Pattern analysis across hazard clusters |
| `TranslateIssueFilter` | Natural language → structured filter conversion |

Deterministic fallbacks are used when the OpenAI API is unavailable.

---

## Security

### Authentication Flows

- **Web:** Cookie-based session with CSRF protection
- **Mobile/API:** JWT Bearer with 30-minute expiry and 7-day refresh tokens
- **OAuth:** Google, Microsoft, Facebook via standard OAuth 2.0 callbacks

### Rate Limiting

| Endpoint group | Limit |
|----------------|-------|
| Auth endpoints | 5 req / 15 min |
| General API | 60 req / min |
| Reporting & analytics | 30 req / min |
| File uploads | 10 req / min |

### Security Headers

All responses include `X-Frame-Options`, `X-Content-Type-Options`, `X-XSS-Protection`, `Referrer-Policy`, and a configurable `Content-Security-Policy`.

### Audit Logging

Every admin action is logged with event type, resource, actor, IP address, user agent, and before/after values.

---

## Testing

```bash
# Run all tests
dotnet test

# Run a specific project
dotnet test FixIt.Tests/FixIt.Tests.csproj
```

### Test Structure

```
FixIt.Tests/
├── Services/       # Unit tests — IssueService, CivicAiService, JwtTokenService, ...
├── Controllers/    # Controller tests
├── Data/           # Repository tests
├── Middleware/     # Middleware tests
└── Security/       # Security tests
```

A running MongoDB instance is required. The test database is isolated from development data.

---

## Deployment

### Docker (Recommended)

```bash
# Development
docker compose up -d

# Production
cp .env.production.example .env.production
docker compose -f docker-compose.yml -f docker-compose.prod.yml \
  --env-file .env.production up -d
```

### Railway

The production deployment runs on Railway with MongoDB Atlas.

Key Railway environment variables to set:

```
MONGODB_URI          = mongodb+srv://...
MONGODB_DATABASE     = fixit
JWT_SECRET_KEY       = <64-char random secret>
GOOGLE_CLIENT_ID     = ...
GOOGLE_CLIENT_SECRET = ...
AllowedHosts         = *
ASPNETCORE_ENVIRONMENT = Production
```

### Production Checklist

- [ ] `ASPNETCORE_ENVIRONMENT=Production`
- [ ] JWT_SECRET_KEY is at least 64 random characters
- [ ] MongoDB Atlas IP Access List includes `0.0.0.0/0` or Railway egress IPs
- [ ] Google OAuth credentials set
- [ ] `AllowedHosts=*` (or your specific domain)
- [ ] SMTP configured for transactional email
- [ ] Rate limiting enabled
- [ ] Audit logging active

### Operational Scripts

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

For full documentation see:

- [`DOCKER.md`](./DOCKER.md) — container setup and troubleshooting
- [`SECRETS.md`](./SECRETS.md) — secrets management for all environments
- [`PRODUCTION_READINESS.md`](./PRODUCTION_READINESS.md) — implementation status
- [`BETA_RELEASE_CHECKLIST.md`](./BETA_RELEASE_CHECKLIST.md) — go/no-go checklist
- [`.github/CI-CD.md`](./.github/CI-CD.md) — GitHub Actions workflows

---

## Contributing

1. Fork the repository
2. Create a feature branch: `git checkout -b feature/my-feature`
3. Commit your changes: `git commit -m 'Add my feature'`
4. Push: `git push origin feature/my-feature`
5. Open a Pull Request

See [`CONTRIBUTING.md`](./CONTRIBUTING.md) for coding standards, testing requirements, and the PR process. Database schema changes require a migration — see [`MIGRATIONS.md`](./FixIt.Data/Infrastructure/Migrations/MIGRATIONS.md).

---

## License

MIT — see [`LICENSE`](./LICENSE) for details.