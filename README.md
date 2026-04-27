# FixIt - Civic Engagement Platform

A comprehensive civic engagement platform that enables citizens to report local infrastructure issues and safety hazards, while fostering community participation through gamification and AI-powered insights.

![.NET](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet)
![MongoDB](https://img.shields.io/badge/MongoDB-47A248?logo=mongodb)
![ASP.NET Core](https://img.shields.io/badge/ASP.NET%20Core-9.0-black?logo=asp.net.core)

## Table of Contents

- [Overview](#overview)
- [Features](#features)
- [Tech Stack](#tech-stack)
- [Architecture](#architecture)
- [Getting Started](#getting-started)
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

FixIt is a full-stack civic engagement platform built with ASP.NET Core 9.0 and MongoDB. It connects citizens with local government by providing a streamlined way to report infrastructure issues (potholes, broken streetlights, graffiti, etc.) and safety hazards (accidents, flooding, dangerous conditions).

The platform combines traditional issue tracking with modern features including:
- **AI-powered analysis** for automatic categorization and prioritization
- **Gamification** to encourage civic participation
- **Real-time safety alerts** for community hazards
- **Multi-language support** (English and Bulgarian)
- **Anonymous reporting** options for privacy-conscious users

---

## Features

### Core Functionality

| Feature | Description |
|---------|-------------|
| **Issue Reporting** | Report infrastructure problems with photos, location data, and detailed descriptions |
| **Safety Hazards** | Real-time hazard reporting with severity levels and community confirmation |
| **Voting System** | Upvote/downvote issues to prioritize community concerns |
| **Comments & Discussion** | Threaded discussions on issues with media attachments |
| **Official Responses** | Government entities can respond with status updates and resolution timelines |
| **Anonymous Reporting** | User-controlled privacy setting for anonymous submissions |

### Advanced Features

| Feature | Description |
|---------|-------------|
| **Gamification** | Reputation points, achievements/badges, trust levels, and leaderboards |
| **AI Analysis** | OpenAI integration for automatic categorization, duplicate detection, and priority suggestions |
| **Heatmaps** | Geospatial visualization of issue clusters and hotspots |
| **Health Reports** | City-level metrics including resolution rates and response times |
| **Multi-language** | Localized UI in English (en-US) and Bulgarian (bg-BG) |
| **OAuth Authentication** | Google, Microsoft, and Facebook login with JWT support for mobile clients |
| **Audit Logging** | Comprehensive security and compliance audit trail |
| **Rate Limiting** | DDoS protection with configurable rate limits per endpoint |

---

## Tech Stack

### Backend

| Component | Technology |
|-----------|------------|
| **Framework** | ASP.NET Core 9.0 |
| **Language** | C# 13 |
| **Database** | MongoDB (with GeoJSON support) |
| **Identity** | ASP.NET Core Identity + MongoDB provider |
| **Authentication** | Cookie-based + JWT Bearer tokens |
| **OAuth** | Google, Microsoft, Facebook |
| **AI** | OpenAI API (gpt-4o-mini) |
| **Email** | SMTP (SendGrid, Gmail, etc.) |

### Frontend

| Component | Technology |
|-----------|------------|
| **UI Framework** | Razor Pages + MVC |
| **Maps** | Leaflet.js + OpenStreetMap |
| **Styling** | Custom CSS with design tokens |
| **JavaScript** | Vanilla ES6+ modules |
| **Charts** | Custom SVG/CSS charts |

### DevOps & Tooling

| Component | Technology |
|-----------|------------|
| **IDE** | Rider / VS Code |
| **Version Control** | Git |
| **API Docs** | Swagger/OpenAPI |
| **Compression** | Brotli + Gzip |
| **Caching** | MemoryCache + OutputCache |

---

## Architecture

FixIt follows a **layered architecture** with clear separation of concerns:

```
┌─────────────────────────────────────────────────────────────┐
│                    Presentation Layer                        │
│  ┌─────────────┐  ┌──────────────┐  ┌──────────────────┐   │
│  │ Razor Pages │  │ API Controllers │  │ Admin Area (UI) │   │
│  └─────────────┘  └──────────────┘  └──────────────────┘   │
└─────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────┐
│                    ViewModels Layer                          │
│  ┌──────────────────────────────────────────────────────┐   │
│  │ DTOs, API Responses, Request/Response Models          │   │
│  └──────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────┐
│                    Services Layer                            │
│  ┌─────────┐ ┌──────────┐ ┌────────┐ ┌──────────────────┐  │
│  │ Issues  │ │ Media    │ │ AI     │ │ Gamification     │  │
│  │ Safety  │ │ Auth     │ │ Email  │ │ Analytics        │  │
│  └─────────┘ └──────────┘ └────────┘ └──────────────────┘  │
└─────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────┐
│                    Data Layer                                │
│  ┌─────────────────┐  ┌──────────────────────────────────┐ │
│  │ Repository<T>   │  │ MongoDB Context + Configuration  │ │
│  └─────────────────┘  └──────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────┐
│                    Domain Models                             │
│  Issue, User, Hazard, Comment, Vote, Media, Tag, etc.       │
└─────────────────────────────────────────────────────────────┘
```

### Project Structure

```
FixIt/
├── FixIt/                          # Main web application
│   ├── Areas/                      # Admin & Identity areas
│   ├── Controllers/                # API controllers
│   ├── Pages/                      # Razor pages
│   ├── Middleware/                 # Custom middleware
│   ├── Resources/                  # Localization files
│   ├── wwwroot/                    # Static assets
│   ├── Program.cs                  # Application entry point
│   └── appsettings.json            # Configuration
├── FixIt.Models/                   # Domain models & enums
│   ├── Issues/                     # Issue-related models
│   ├── Users/                      # User & identity models
│   ├── Engagement/                 # Comments, votes
│   ├── Safety/                     # Hazard models
│   ├── Media/                      # Media & uploads
│   ├── Gamification/               # Reputation, achievements
│   ├── AI/                         # AI analysis models
│   ├── Moderation/                 # Content moderation
│   ├── Locations/                  # Cities, neighborhoods
│   └── Enums/                      # Enum definitions
├── FixIt.Services/                 # Business logic layer
│   ├── Contracts/                  # Service interfaces
│   ├── AI/                         # AI services
│   ├── Analytics/                  # Health reports, heatmaps
│   ├── Authentication/             # Auth & JWT services
│   ├── Background/                 # Hosted services
│   ├── Gamification/               # Reputation system
│   ├── Safety/                     # Hazard service
│   └── Audit/                      # Audit logging
├── FixIt.Data/                     # Data access layer
│   ├── Configuration/              # MongoDB index config
│   ├── Infrastructure/             # DbContext, settings
│   └── Repository/                 # Generic repository
├── FixIt.ViewModels/               # View models & mappers
└── FixIt.Tests/                    # Unit & integration tests
```

---

## Getting Started

### Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [MongoDB](https://www.mongodb.com/try/download/community) (v6.0+)
- [Git](https://git-scm.com/)

### Installation

1. **Clone the repository**
   ```bash
   git clone https://github.com/denjiyy/FixIt.git
   cd FixIt
   ```

2. **Start MongoDB**
   ```bash
   # macOS (Homebrew)
   brew services start mongodb-community
   
   # Windows (if installed as service)
   net start MongoDB
   
   # Or run manually
   mongod --dbpath /data/db
   ```

3. **Configure environment variables**
   ```bash
   # Copy the example environment file
   cp .env.example .env
   
   # Edit .env with your credentials
   ```

4. **Restore dependencies**
   ```bash
   dotnet restore
   ```

5. **Run the application**
   ```bash
   cd FixIt
   dotnet run
   ```

6. **Access the application**
   - Web UI: http://localhost:5092
   - API: http://localhost:5092/api
   - Swagger: http://localhost:5092/swagger

### Development Admin User

To create a development admin user, add to `appsettings.Development.json`:

```json
{
  "Database": {
    "ResetOnStartup": false,
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

## Configuration

### Environment Variables

| Variable | Description | Example |
|----------|-------------|---------|
| `MONGODB_CONNECTION_STRING` | MongoDB connection string | `mongodb://localhost:27017` |
| `MONGODB_DATABASE_NAME` | Database name | `fixit` |
| `GOOGLE_CLIENT_ID` | Google OAuth client ID | `123456...apps.googleusercontent.com` |
| `GOOGLE_CLIENT_SECRET` | Google OAuth secret | `GOCSPX-...` |
| `GITHUB_CLIENT_ID` | GitHub OAuth client ID | `Iv1...` |
| `GITHUB_CLIENT_SECRET` | GitHub OAuth secret | `...` |
| `FACEBOOK_APP_ID` | Facebook app ID | `123456789` |
| `FACEBOOK_APP_SECRET` | Facebook app secret | `...` |
| `MICROSOFT_CLIENT_ID` | Microsoft OAuth client ID | `...` |
| `MICROSOFT_CLIENT_SECRET` | Microsoft OAuth secret | `...` |
| `OPENAI_API_KEY` | OpenAI API key | `<openai-api-key>` |
| `SMTP_HOST` | SMTP server host | `smtp.gmail.com` |
| `SMTP_PORT` | SMTP server port | `587` |
| `SMTP_USERNAME` | SMTP username | `noreply@fixit.local` |
| `SMTP_PASSWORD` | SMTP password (app password) | `...` |
| `JWT_SECRET_KEY` | JWT signing key (min 32 chars) | `your-super-secret-key-here` |

### appsettings.json Configuration

```json
{
  "MongoDB": {
    "ConnectionString": "mongodb://localhost:27017",
    "DatabaseName": "fixit"
  },
  "Media": {
    "StoragePath": "wwwroot/uploads",
    "MaxFileSizeBytes": 5242880,
    "MaxVideoFileSizeBytes": 104857600,
    "MaxFilesPerUpload": 10,
    "AllowedExtensions": [".jpg", ".jpeg", ".png", ".mp4", ".webm"]
  },
  "Security": {
    "RequireHttps": false,
    "RateLimiting": {
      "Enabled": true,
      "RequestsPerMinute": 60,
      "RequestsPerHour": 1000
    },
    "CorsAllowedOrigins": ["http://localhost:3000", "http://localhost:5092"]
  },
  "Jwt": {
    "Issuer": "FixIt",
    "Audience": "FixItClients",
    "AccessTokenExpirationMinutes": 30,
    "RefreshTokenExpirationDays": 7
  },
  "OpenAI": {
    "ApiKey": "${OPENAI_API_KEY}",
    "Model": "gpt-4o-mini",
    "Enabled": true,
    "TimeoutSeconds": 30
  }
}
```

---

## API Reference

### Authentication Endpoints

| Method | Endpoint | Description | Auth |
|--------|----------|-------------|------|
| `POST` | `/api/auth/login/{provider}` | Initiate OAuth login | No |
| `GET` | `/api/auth/signin-callback` | OAuth callback | No |
| `POST` | `/api/auth/logout` | Sign out | Yes |
| `POST` | `/api/auth/refresh` | Refresh access token | No |
| `GET` | `/api/auth/user` | Get current user | Yes |

### Issues Endpoints

| Method | Endpoint | Description | Auth |
|--------|----------|-------------|------|
| `POST` | `/api/issues` | Create issue | Yes |
| `GET` | `/api/issues/{id}` | Get issue by ID | No |
| `GET` | `/api/issues/city/{cityId}` | Get issues by city | No |
| `POST` | `/api/issues/city/{cityId}/search` | Search issues | No |
| `GET` | `/api/issues/my-issues` | Get user's issues | Yes |
| `PUT` | `/api/issues/{id}/status` | Update status | Mod+ |
| `PUT` | `/api/issues/{id}/priority` | Update priority | Mod+ |
| `POST` | `/api/issues/{id}/vote` | Vote on issue | Yes |
| `DELETE` | `/api/issues/{id}/vote` | Remove vote | Yes |
| `DELETE` | `/api/issues/{id}` | Delete issue | Owner/Admin |
| `POST` | `/api/issues/{id}/comments` | Add comment | Yes |
| `GET` | `/api/issues/{id}/comments` | Get comments | No |

### Safety Endpoints

| Method | Endpoint | Description | Auth |
|--------|----------|-------------|------|
| `GET` | `/api/safety/nearby-hazards` | Get nearby hazards | No |
| `GET` | `/api/safety/critical-hazards` | Get critical hazards | No |
| `POST` | `/api/safety/report` | Report hazard | Yes |
| `POST` | `/api/safety/{id}/confirm` | Confirm hazard | Yes |
| `POST` | `/api/safety/{id}/resolve` | Resolve hazard | Admin |
| `DELETE` | `/api/safety/{id}` | Delete hazard | Owner/Admin |

### AI & Suggestions Endpoints

| Method | Endpoint | Description | Auth |
|--------|----------|-------------|------|
| `POST` | `/api/analysis/analyze/{issueId}` | Analyze issue | No |
| `GET` | `/api/analysis/analyze/{issueId}` | Get analysis | No |
| `POST` | `/api/analysis/issue-draft-suggestions` | Get draft suggestions | Yes |
| `POST` | `/api/suggestions/pending` | Get pending suggestions | Admin+ |
| `POST` | `/api/suggestions/{id}/act` | Mark as acted | Admin+ |

### Admin Endpoints

| Method | Endpoint | Description | Auth |
|--------|----------|-------------|------|
| `GET` | `/api/admin/audit-logs` | Get audit logs | Admin |
| `GET` | `/api/admin/audit-logs/export/csv` | Export audit logs | Admin |

For complete API documentation, run the application and visit `/swagger`.

---

## Database Schema

### Collections

| Collection | Description | Key Indexes |
|------------|-------------|-------------|
| `users` | ApplicationUser documents | Email (unique), UserName (unique) |
| `issues` | Issue reports | Location (2dsphere), CityId+Status+CreatedAt |
| `hazards` | Safety hazards | Location (2dsphere), CityId+Severity |
| `comments` | User comments | IssueId+CreatedAt |
| `votes` | Issue votes | IssueId+UserId (unique compound) |
| `media` | Media file metadata | OwnerId+CreatedAt |
| `media_references` | Media usage tracking | MediaId, ReferenceId |
| `tags` | Issue tags | Name (unique), UsageCount |
| `cities` | City configurations | Name |
| `neighborhoods` | Neighborhood boundaries | CityId |
| `user_reputations` | Reputation profiles | UserId (unique) |
| `reputation_transactions` | Points history | UserId+CreatedAt |
| `leaderboards` | Leaderboard entries | Period+Rank |
| `issue_analyses` | AI analysis results | IssueId |
| `admin_suggestions` | AI suggestions | TargetEntityId+GeneratedAt |
| `content_reports` | User reports | TargetId+Status |
| `audit_logs` | Admin action logs | Timestamp, ActorId, ResourceType |
| `translations` | Translation history | ContentId+TargetLanguage |

### Key Model Relationships

```
ApplicationUser (1) ──→ (M) Issue (reports)
ApplicationUser (1) ──→ (M) Comment
ApplicationUser (1) ──→ (M) Vote
ApplicationUser (1) ──→ (M) Hazard (reports)
ApplicationUser (1) ──→ (1) UserReputation

Issue (1) ──→ (M) Comment
Issue (1) ──→ (M) Vote
Issue (1) ──→ (1) IssueAnalysis
Issue (1) ──→ (M) Media (via MediaReference)

City (1) ──→ (M) Issue
City (1) ──→ (M) Neighborhood
City (1) ──→ (M) Hazard
```

---

## Gamification System

### Trust Levels

| Level | Name | Points Required | Permissions |
|-------|------|-----------------|-------------|
| 0 | New | 0-10 | Basic reporting |
| 1 | Active | 11-50 | Increased vote weight |
| 2 | Trusted | 51-150 | Priority vote weight, badge on profile |
| 3 | Leader | 150+ | Maximum vote weight, leaderboard featured |

### Reputation Points

| Action | Points | Description |
|--------|--------|-------------|
| `issue_reported` | +5 | Report a new issue |
| `issue_confirmed` | +3 | Another user confirms your issue |
| `comment_posted` | +2 | Post a helpful comment |
| `received_upvote` | +1 | Receive an upvote (issue or comment) |
| `issue_resolved` | +15 | Issue marked as fixed |
| `hazard_confirmed` | +5 | Confirm a safety hazard |

### Achievements

| Achievement | Requirement | Points |
|-------------|-------------|--------|
| `FirstReporter` | Report first issue | 10 |
| `HelpfulCommenter` | Post 10 comments | 15 |
| `CommunityHelper` | Receive 50 upvotes | 25 |
| `IssueSolver` | Have 5 issues resolved | 30 |
| `CivicContributor` | Reach Trust Level 2 | 20 |
| `CommunityChampion` | Reach Trust Level 3 | 50 |
| `CivicLeader` | Top of weekly leaderboard | 40 |
| `AccurateReporter` | 90%+ issues confirmed | 35 |
| `VerifiedCitizen` | Verify email + phone | 15 |

---

## AI Features

### Issue Analysis

The AI system automatically analyzes new issue reports to:
- **Categorize** the issue (Infrastructure, Public Safety, Environmental, etc.)
- **Suggest priority** (Low, Medium, High, Critical)
- **Detect duplicates** by comparing with existing issues
- **Extract keywords** for better searchability
- **Suggest tags** from the tag database

### Civic AI Service

| Feature | Description |
|---------|-------------|
| `SuggestIssueDraft` | Get category/priority/department suggestions from title + description |
| `SummarizeIssueThread` | Generate summary of issue + comments (with streaming support) |
| `SummarizeReport` | Summarize content moderation reports |
| `GenerateHazardInsight` | Analyze hazard clusters for patterns |
| `TranslateIssueFilter` | Convert natural language queries to structured filters |

### Configuration

```json
{
  "OpenAI": {
    "ApiKey": "<openai-api-key>",
    "Model": "gpt-4o-mini",
    "Enabled": true,
    "TimeoutSeconds": 30
  }
}
```

The system includes deterministic fallbacks when the API is unavailable.

---

## Security

### Authentication

- **Cookie-based** authentication for web clients
- **JWT Bearer** tokens for mobile/API clients
- **OAuth 2.0** integration (Google, Microsoft, Facebook)
- **Refresh tokens** with configurable expiration (7 days default)

### Authorization

| Policy | Requirement |
|--------|-------------|
| `AdminOnly` | Admin role |
| `ModeratorOrAdmin` | Moderator or Admin role |
| `Authorize` | Any authenticated user |

### Rate Limiting

| Endpoint | Limit | Window |
|----------|-------|--------|
| Auth endpoints | 5 requests | 15 minutes |
| API endpoints | 60 requests | 1 minute |
| Reporting/Analytics | 30 requests | 1 minute |
| File uploads | 10 requests | 1 minute |

### Security Headers

- `X-Frame-Options: SAMEORIGIN` (clickjacking protection)
- `X-Content-Type-Options: nosniff` (MIME sniffing protection)
- `X-XSS-Protection: 1; mode=block` (XSS filter)
- `Referrer-Policy: strict-origin-when-cross-origin`
- `Content-Security-Policy` (configurable)

### Audit Logging

All admin actions are logged with:
- Event type and action
- Resource and resource ID
- Actor (user) information
- IP address and user agent
- Changes made (before/after)
- Reason for action

---

## Testing

### Run Tests

```bash
dotnet test
```

### Test Structure

```
FixIt.Tests/
├── Services/           # Unit tests for services
│   ├── IssueServiceTests.cs
│   ├── CivicAiServiceTests.cs
│   ├── JwtTokenServiceTests.cs
│   └── ...
├── Controllers/        # Controller tests
├── Data/              # Repository tests
├── Middleware/        # Middleware tests
└── Security/          # Security tests
```

### Test Requirements

- MongoDB instance must be running
- Test database is isolated from development data

---

## Deployment

### Production Checklist

- [ ] Set `ASPNETCORE_ENVIRONMENT=Production`
- [ ] Configure HTTPS (`RequireHttps: true`)
- [ ] Set secure JWT_SECRET_KEY (min 32 characters)
- [ ] Configure production MongoDB connection
- [ ] Set up OAuth credentials for production
- [ ] Configure SMTP for production email
- [ ] Set `CORS_ALLOWED_ORIGINS` to production domain
- [ ] Enable rate limiting
- [ ] Configure logging provider (Application Insights, Serilog, etc.)

### Environment Variables (Production)

```bash
# Database
MONGODB_CONNECTION_STRING=mongodb+srv://user:pass@cluster.mongodb.net/fixit
MONGODB_DATABASE_NAME=fixit

# Security
JWT_SECRET_KEY=your-super-secret-key-min-32-characters-long

# OAuth
GOOGLE_CLIENT_ID=...
GOOGLE_CLIENT_SECRET=...

# Email
EMAIL_PROVIDER=Smtp
SMTP_HOST=smtp.sendgrid.net
SMTP_PORT=587
SMTP_USERNAME=apikey
SMTP_PASSWORD=...

# Application
ASPNETCORE_ENVIRONMENT=Production
APP_BASE_URL=https://fixit.yourdomain.com
ALLOWED_HOSTS=fixit.yourdomain.com
```

### Docker (Future)

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "FixIt.dll"]
```

---

## Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

### Code Style

- Follow C# naming conventions (PascalCase for public members, camelCase for private)
- Use nullable reference types (`<Nullable>enable</Nullable>`)
- Add XML documentation for public APIs
- Keep methods small and focused
- Write unit tests for new features

---

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

---

## Support

For issues, questions, or contributions, please:
- Open an issue on [GitHub](https://github.com/denjiyy/FixIt/issues)
- Contact the maintainers

---

*Built with  for civic engagement and community improvement*
