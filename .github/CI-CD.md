# GitHub Actions CI/CD Documentation

## Overview

This project includes three GitHub Actions workflows for automated quality assurance and deployment:

- **dotnet.yml** - Build, test, and code quality checks
- **security.yml** - Vulnerability scanning and dependency checking
- **docker.yml** - Docker image building and publishing

---

## Build & Test Workflow (.NET)

**Triggers:**
- On every push to `main` or `develop` branches
- On every pull request to `main` or `develop`

**What it does:**
1. Checks out code
2. Sets up .NET 9.0 runtime
3. Restores NuGet dependencies (cached)
4. Builds solution in Release mode
5. Runs unit tests with code coverage
6. Uploads coverage to Codecov
7. Checks for NuGet vulnerabilities
8. Runs code quality analysis

**Status Checks:**
- ✅ Build succeeds
- ✅ All tests pass
- ✅ No critical vulnerabilities
- ⚠️ Code style violations (non-blocking)

**Example PR Status:**
```
✅ build-and-test — All checks passed
✅ code-quality — All checks passed  
✅ docker-build — All checks passed
```

---

## Security Workflow

**Triggers:**
- On every push to `main` or `develop`
- On every pull request
- Daily at 2 AM UTC (scheduled)

**What it does:**
1. **Trivy Scan** - Filesystem vulnerability scanning
   - Checks for known CVEs in dependencies
   - Fails on CRITICAL/HIGH severity issues
   - Uploads results to GitHub Security tab

2. **Dependency Check** - NuGet package analysis
   - Lists vulnerable packages
   - Identifies transitive dependencies
   - Flags outdated versions

**Viewing Results:**
- Navigate to repo → Security tab → Code scanning alerts
- Resolve issues before merging to main

---

## Docker Build & Push

**Triggers:**
- On every push to `main` branch
- On tag push (v*.*)
- Manual trigger via workflow_dispatch

**What it does:**
1. Checks out code
2. Sets up Docker buildx for multi-platform builds
3. Logs into GitHub Container Registry (GHCR)
4. Extracts version metadata from git tags
5. Builds multi-stage Dockerfile
6. Pushes image to GHCR with tags:
   - `latest` (for main branch)
   - `v1.2.3` (for release tags)
   - `sha-abc123` (git commit SHA)
   - `main`, `develop` (branch names)

**Image Registry:**
- Images stored at: `ghcr.io/denjiyy/fixit:latest`
- Pull image: `docker pull ghcr.io/denjiyy/fixit:latest`

**Authentication:**
- Uses GITHUB_TOKEN (automatically provided)
- No manual authentication needed
- Images are private by default (configure repo settings to make public)

---

## Local Testing

### Run tests locally before pushing:

```bash
# Restore dependencies
dotnet restore

# Build in Release mode
dotnet build -c Release

# Run all tests
dotnet test

# Run specific test file
dotnet test FixIt.Tests/Services/IssueServiceTests.cs

# Run with code coverage
dotnet test /p:CollectCoverage=true
```

### Build Docker image locally:

```bash
# Build
docker build -t fixit:dev .

# Run with docker-compose
docker-compose up -d
```

---

## Pull Request Workflow

### When you create a PR:

1. **GitHub automatically runs all workflows**
2. **Required checks must pass:**
   - ✅ dotnet.yml (build-and-test)
   - ✅ security.yml (security-scan)

3. **You can merge when all checks pass**

4. **On merge to main:**
   - Docker image is built and pushed
   - Available for deployment

---

## Release Workflow

### To create a release:

```bash
# Create and push a version tag
git tag -a v1.0.0 -m "Release version 1.0.0"
git push origin v1.0.0
```

**What happens:**
- GitHub Actions triggers docker.yml
- Builds and tags image as `ghcr.io/denjiyy/fixit:v1.0.0`
- Also tags as `latest`
- Create GitHub Release with artifact links

---

## Monitoring Workflows

### View workflow runs:
1. Go to repo → Actions tab
2. Select workflow from left sidebar
3. Click on a run to see details

### View recent runs:
```bash
gh run list --repo denjiyy/fixit
```

### View specific run logs:
```bash
gh run view <run-id> --repo denjiyy/fixit
```

### Download artifacts:
```bash
gh run download <run-id> --repo denjiyy/fixit
```

---

## Troubleshooting

### Build failed - Dependencies not found
```
❌ dotnet restore failed
```
**Solution:** Check `.csproj` files for correct package references

### Tests failed
```
❌ dotnet test failed
```
**Solution:**
1. Run tests locally: `dotnet test`
2. Check test output for failures
3. Fix code and push again

### Docker push failed
```
❌ Login to registry failed
```
**Solution:** GitHub Container Registry uses `GITHUB_TOKEN` (automatic)

### Workflow not triggering
**Check:**
- Is branch protection enabled?
- Is workflow enabled? (Actions tab → workflow name → ⋯ → Enable)
- Does branch match trigger condition?

---

## Secrets Management

### Adding secrets for CI/CD:

Some workflows may need secrets (credentials, API keys, etc.):

1. Go to repo → Settings → Secrets and variables → Actions
2. Click "New repository secret"
3. Add secret name and value
4. Use in workflow: `${{ secrets.SECRET_NAME }}`

**Do not commit secrets!**

---

## Next Steps

### Current Setup:
- ✅ Build & test on every PR
- ✅ Security scanning daily
- ✅ Docker image building

### Recommended Additions:
1. **Code Coverage Reporting** - Set minimum coverage threshold
2. **Deployment Pipeline** - Auto-deploy to staging/production
3. **Performance Testing** - Load tests on PRs
4. **Documentation Generation** - Auto-generate API docs

---

For questions, see [README.md](../README.md) or [DOCKER.md](../DOCKER.md).
