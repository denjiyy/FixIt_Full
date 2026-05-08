# Quick Reference Guide

A quick reference for developers on the FixIt project.

---

## 🚀 Getting Started (5 minutes)

### First Time Setup

```bash
# Clone repository
git clone https://github.com/denjiyy/FixIt.git
cd FixIt

# Copy environment template
cp .env.example .env

# Start development environment
docker compose up -d

# View application
open http://localhost:5092
```

### View Logs

```bash
# All services
docker compose logs -f

# Just the app
docker compose logs -f fixit-app

# Just MongoDB
docker compose logs -f mongodb
```

---

## 📚 Important Documentation

### For Deployment
→ **[DOCKER.md](./DOCKER.md)**
- Local development
- Production setup
- Troubleshooting

### For Secrets
→ **[SECRETS.md](./SECRETS.md)**
- Dev environment setup
- Staging/prod configuration
- Secret rotation

### For Database Changes
→ **[FixIt.Data/Infrastructure/Migrations/MIGRATIONS.md](./FixIt.Data/Infrastructure/Migrations/MIGRATIONS.md)**
- Creating migrations
- Schema changes
- Best practices

### For CI/CD
→ **[.github/CI-CD.md](./.github/CI-CD.md)**
- Build pipeline
- Testing automation
- Release process

### For Contributing
→ **[CONTRIBUTING.md](./CONTRIBUTING.md)**
- Coding standards
- Testing requirements
- PR process

---

## 💻 Common Development Tasks

### Run Tests

```bash
# Run all tests
dotnet test

# Run specific test file
dotnet test FixIt.Tests/Services/IssueServiceTests.cs

# With coverage
dotnet test /p:CollectCoverage=true
```

### Build Solution

```bash
# Development build
dotnet build

# Production build
dotnet build -c Release
```

### Database Access

```bash
# Connect to MongoDB
docker compose exec mongodb mongosh -u root -p rootpassword --authenticationDatabase admin

# View databases
show dbs

# Switch to fixit
use fixit

# View collections
show collections
```

### Add NuGet Package

```bash
cd FixIt
dotnet add package PackageName
# or
dotnet add package PackageName --version 1.2.3
```

### Create Database Migration

1. Create file: `FixIt.Data/Infrastructure/Migrations/Migration_YYYYMMDD_001_Description.cs`
2. Implement `IMigration` interface
3. Write `UpAsync` method
4. Test locally with `docker compose down -v && docker compose up -d`

See [MIGRATIONS.md](./FixIt.Data/Infrastructure/Migrations/MIGRATIONS.md) for examples.

---

## 🔧 Configuration

### Local Development (.env)

```bash
# Required for local testing
MONGODB_CONNECTION_STRING=mongodb://root:<local-db-password>@mongodb:27017
GOOGLE_CLIENT_ID=dev-id
GOOGLE_CLIENT_SECRET=dev-secret
JWT_SECRET_KEY=<local-32-char-minimum-secret>
EMAIL_PROVIDER=Console  # Logs emails to console
```

### Production Deployment

See [SECRETS.md](./SECRETS.md) for complete guide on configuring:
- Azure Key Vault / AWS Secrets Manager
- Database credentials
- OAuth keys
- API keys

---

## 🐳 Docker Commands

### Start Services

```bash
# Development (with hot reload support)
docker compose up -d

# Production
docker compose -f docker-compose.yml -f docker-compose.prod.yml --env-file .env.prod up -d
```

### Stop Services

```bash
docker compose down
```

### Clean Everything

```bash
# Remove containers and volumes
docker compose down -v

# Rebuild after code changes
docker compose build --no-cache fixit-app
docker compose up -d
```

### Execute Command in Container

```bash
# Run bash in app container
docker compose exec fixit-app /bin/bash

# Run bash in MongoDB
docker compose exec mongodb bash

# View environment variables
docker compose exec fixit-app env | grep ASPNETCORE
```

---

## ✅ CI/CD Pipeline

### GitHub Actions Workflows

Located in `.github/workflows/`:

- **dotnet.yml** - Runs on every PR/commit to main/develop
  - Builds solution
  - Runs tests
  - Checks dependencies
  - Code quality analysis

- **security.yml** - Runs daily + on PR/commit
  - Trivy vulnerability scan
  - Dependency checking
  - CVE detection

- **docker.yml** - Runs on push to main
  - Builds Docker image
  - Publishes to GHCR
  - Tags with version

### Triggering Workflows

Workflows trigger automatically on:
- Push to `main` or `develop` branch
- Pull requests to `main` or `develop`
- Tags matching `v*` (release tags)
- Daily at 2 AM UTC (security scan)

### View Workflow Status

- Repository → Actions tab
- Select workflow from left sidebar
- View recent runs

---

## 🔍 Debugging

### Application Won't Start

```bash
# Check logs
docker compose logs fixit-app

# Common issues:
# - MongoDB connection string wrong
# - API keys not configured
# - Port already in use (5092 or 27017)
```

### Tests Failing Locally

```bash
# Ensure MongoDB is running
docker compose ps

# Restart services
docker compose restart

# Run tests with verbose output
dotnet test -v detailed
```

### Port Already in Use

```bash
# Find process using port
lsof -i :5092

# Kill process
kill -9 <PID>

# Or change port in docker-compose.yml
```

---

## 📊 Monitoring & Health

### Health Check

```bash
# Application health
curl http://localhost:5092/health/live

# Expected response:
# {"status":"healthy","timestamp":"...","checks":{...}}
```

### Container Stats

```bash
# Real-time stats for all containers
docker stats

# Stop with Ctrl+C
```

### Database Connection Test

```bash
# From container
docker compose exec fixit-app curl http://localhost:8080/health/ready

# Or manually
docker compose exec mongodb mongosh -u root -p rootpassword ping
```

---

## 🚢 Deployment Checklist

### Before Committing

- [ ] Code builds without errors: `dotnet build`
- [ ] Tests pass: `dotnet test`
- [ ] No hardcoded secrets or passwords
- [ ] Database migrations created if schema changed
- [ ] Documentation updated

### Before Merging to Main

- [ ] CI/CD pipeline passes (GitHub Actions)
- [ ] Code review approved
- [ ] Tests coverage maintained
- [ ] No security warnings

### Before Production Deployment

- [ ] Secrets configured in vault (AWS/Azure)
- [ ] Database backup created
- [ ] Staging environment tested
- [ ] Deployment runbook reviewed
- [ ] Team notified

---

## 🔐 Security Reminders

✅ **DO:**
- Use strong random secrets (32+ chars)
- Rotate secrets quarterly
- Check environment variables for production deployments
- Use HTTPS in production
- Enable rate limiting

❌ **DON'T:**
- Commit `.env` files
- Log sensitive data
- Use `localhost` in production configs
- Run with `privileged: true` in Docker
- Share API keys via chat/email

See [SECRETS.md](./SECRETS.md) for complete security guide.

---

## 📖 Additional Resources

- [MongoDB Documentation](https://docs.mongodb.com/)
- [ASP.NET Core Docs](https://docs.microsoft.com/aspnet/core/)
- [Docker Official Images](https://hub.docker.com/_/dotnet)
- [GitHub Actions Docs](https://docs.github.com/en/actions)
- [OWASP Security Guides](https://owasp.org/)

---

## 💬 Getting Help

**Documentation:**
- See [README.md](./README.md) for full documentation
- Check specific guides in docs referenced above

**Issues:**
- [GitHub Issues](https://github.com/denjiyy/FixIt/issues)

**Contact:**
- Maintainers: @denjiyy

---

**Last Updated:** April 28, 2026  
**Status:** ✅ Production Ready (Core Infrastructure)
