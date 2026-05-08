# Secrets Management Guide

## Overview

This document covers best practices for managing sensitive data (API keys, database credentials, passwords) in the FixIt application across development, staging, and production environments.

---

## Secrets Categories

| Category | Examples | Sensitivity | Storage |
|----------|----------|-------------|---------|
| **Credentials** | DB password, API keys | CRITICAL | Vault/KMS |
| **Configuration** | Email SMTP, OAuth IDs | HIGH | Environment variables |
| **Tokens** | JWT secret, refresh tokens | CRITICAL | Vault/KMS |
| **Certificates** | SSL/TLS keys | CRITICAL | Dedicated service |

---

## Development Environment

### Local Setup (.env file)

1. **Create `.env` from template:**
   ```bash
   cp .env.example .env
   ```

2. **Never commit `.env` to git:**
   ```bash
   # Already in .gitignore, but verify:
   cat .gitignore | grep "\.env"
   ```

3. **Use development-safe defaults:**
   ```env
   # .env (local development)
   MONGODB_CONNECTION_STRING=mongodb://root:<local-db-password>@localhost:27017
   GOOGLE_CLIENT_ID=dev-client-id-not-used
   OPENAI_API_KEY=<local-non-production-key>
   JWT_SECRET_KEY=<local-32-char-minimum-secret>
   EMAIL_PROVIDER=Console  # Logs emails instead of sending
   ```

### Docker Development

**Using docker compose with secrets:**

```bash
# Create .env file (git-ignored)
cp .env.example .env
# Edit with local values
nano .env

# Start with environment file
docker compose --env-file .env up -d

# Or use default environment in docker-compose.yml
docker compose up -d
```

### User Secrets (Local Development)

**.NET User Secrets** - Separate from project files:

```bash
# Initialize user secrets for project
cd FixIt
dotnet user-secrets init

# Set secret (stored in OS keychain, not in files)
dotnet user-secrets set "Jwt:SecretKey" "my-super-secret-key-32-chars"
dotnet user-secrets set "OpenAI:ApiKey" "<openai-api-key>"

# View all secrets
dotnet user-secrets list

# Clear all secrets
dotnet user-secrets clear
```

**Where secrets are stored:**
- **Windows:** `%APPDATA%\Microsoft\UserSecrets\{UserSecretsId}\secrets.json`
- **Linux/macOS:** `~/.microsoft/usersecrets/{UserSecretsId}/secrets.json`
- **Never committed** to source control

---

## Staging Environment

### Recommended: Azure Key Vault

**Setup:**

```bash
# Create Key Vault in Azure
az keyvault create --name fixit-staging-kv --resource-group fixit-staging

# Add secrets
az keyvault secret set --vault-name fixit-staging-kv \
  --name "mongodb-connection-string" \
  --value "mongodb+srv://user:pass@..."

# List secrets
az keyvault secret list --vault-name fixit-staging-kv
```

**Application configuration:**

```csharp
// Program.cs - Add Key Vault to configuration
if (!builder.Environment.IsDevelopment())
{
    var keyVaultEndpoint = new Uri(builder.Configuration["KeyVault:VaultUri"]);
    builder.Configuration.AddAzureKeyVault(
        keyVaultEndpoint,
        new DefaultAzureCredential()
    );
}
```

### Alternative: GitHub Secrets (CI/CD)

For GitHub Actions workflows:

```yaml
# .github/workflows/deploy-staging.yml
name: Deploy Staging

on:
  push:
    branches: [develop]

jobs:
  deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      
      - name: Deploy to staging
        env:
          MONGODB_CONNECTION_STRING: ${{ secrets.STAGING_MONGODB_CONNECTION_STRING }}
          OPENAI_API_KEY: ${{ secrets.OPENAI_API_KEY }}
          JWT_SECRET_KEY: ${{ secrets.JWT_SECRET_KEY }}
        run: |
          docker build -t fixit:staging .
          # Deploy with secrets...
```

**Adding GitHub Secrets:**
1. Repository → Settings → Secrets and variables → Actions
2. Click "New repository secret"
3. Name: `STAGING_MONGODB_CONNECTION_STRING`
4. Value: `mongodb+srv://...`

---

## Production Environment

### Recommended: AWS Secrets Manager

**Setup:**

```bash
# Create secret
aws secretsmanager create-secret \
  --name fixit/prod/mongodb \
  --secret-string '{"ConnectionString":"mongodb+srv://..."}'

# Retrieve secret (in application)
aws secretsmanager get-secret-value --secret-id fixit/prod/mongodb

# Rotate secret (AWS handles expiration)
aws secretsmanager rotate-secret --secret-id fixit/prod/mongodb
```

**Application configuration:**

```csharp
// FixIt/Program.cs - Add AWS Secrets Manager
if (builder.Environment.IsProduction())
{
    var configuration = builder.Configuration;
    var client = new SecretsManagerClient(RegionEndpoint.USEast1);
    
    var secret = client.GetSecretValueAsync(new GetSecretValueRequest 
    { 
        SecretId = "fixit/prod/mongodb" 
    }).Result;
    
    var mongoConnectionString = JsonDocument.Parse(secret.SecretString)
        .RootElement.GetProperty("ConnectionString").GetString();
    
    configuration["MongoDB:ConnectionString"] = mongoConnectionString;
}
```

### Alternative: Google Cloud Secret Manager

```bash
# Create secret
gcloud secrets create mongodb-connection-string \
  --replication-policy="automatic"

# Add version
echo -n 'mongodb+srv://user:pass@...' | \
  gcloud secrets versions add mongodb-connection-string --data-file=-

# Grant access to service account
gcloud secrets add-iam-policy-binding mongodb-connection-string \
  --member=serviceAccount:fixit-app@project.iam.gserviceaccount.com \
  --role=roles/secretmanager.secretAccessor
```

### Environment Variables via Docker Secrets (Swarm/K8s)

**Docker Compose:**
```yaml
services:
  fixit-app:
    environment:
      MONGODB_CONNECTION_STRING: ${MONGODB_CONNECTION_STRING}
      JWT_SECRET_KEY: ${JWT_SECRET_KEY}
```

**Kubernetes:**
```yaml
# Deploy with secrets injected as environment variables
apiVersion: v1
kind: Secret
metadata:
  name: fixit-secrets
type: Opaque
stringData:
  MONGODB_CONNECTION_STRING: mongodb+srv://...
  JWT_SECRET_KEY: your-secret-key
---
apiVersion: apps/v1
kind: Deployment
metadata:
  name: fixit-app
spec:
  template:
    spec:
      containers:
      - name: fixit-app
        envFrom:
        - secretRef:
            name: fixit-secrets
```

---

## Secrets Rotation

### Passwords & API Keys

**Schedule:** Every 90 days

```bash
# 1. Generate new secret
NEW_KEY=$(openssl rand -base64 32)

# 2. Update in secret manager
aws secretsmanager update-secret --secret-id fixit/prod/jwt-key \
  --secret-string "$NEW_KEY"

# 3. Deploy application (picks up new secret on restart)
docker compose restart fixit-app

# 4. Remove old secret (after verification)
# (keep in history for emergency rollback)
```

### Database Credentials

**If using MongoDB Atlas:**
```bash
# 1. Create new database user
az cosmosdb sql role definition create \
  --account-name fixit-prod \
  --resource-group prod \
  --body @role-definition.json

# 2. Update connection string in secrets manager
# 3. Redeploy application
# 4. Delete old database user
```

### JWT Secret Keys

⚠️ **Important:** Rotating JWT secret invalidates all existing tokens
- Current sessions will be logged out
- Consider off-peak time rotation
- Plan 1-2 hour maintenance window

```bash
# 1. Add new key (keep old as fallback)
KEYS=["new-key-2024", "old-key-2023"]

# 2. Update validation to accept both
# 3. Remove old key after grace period
```

---

## Security Checklist

### ✅ Development

- [ ] `.env` file in `.gitignore`
- [ ] No real API keys in code
- [ ] User Secrets initialized
- [ ] Dummy values in `.env.example`
- [ ] `.env.local` ignored (if used)

### ✅ Staging

- [ ] Secrets stored in Key Vault / Secrets Manager
- [ ] Database password ≠ production
- [ ] API keys have rate limits
- [ ] Secrets rotated quarterly
- [ ] Access logs enabled

### ✅ Production

- [ ] All secrets in dedicated vault
- [ ] No plaintext in logs
- [ ] Encryption at rest enabled
- [ ] Encryption in transit (HTTPS/TLS)
- [ ] Audit logs for secret access
- [ ] MFA required for secret retrieval
- [ ] Automated secret rotation
- [ ] Backup/disaster recovery tested
- [ ] Least privilege access control

---

## Anti-Patterns ❌

### Never Do This

```csharp
// ❌ NEVER hardcode secrets
var mongoConnection = "mongodb+srv://user:PASSWORD123@...";

// ❌ NEVER log secrets
Console.WriteLine($"API Key: {apiKey}");
_logger.LogInformation($"Token: {jwtToken}");

// ❌ NEVER commit .env files
git add .env  # WRONG!

// ❌ NEVER use weak secrets
var secret = "123456";  // Too short, predictable

// ❌ NEVER commit secrets to comments
// MongoDB password: "MyPassword123"

// ❌ NEVER use same secret everywhere
DATABASE_PASSWORD=admin123
API_KEY=admin123  // Reusing password!

// ❌ NEVER share secrets in chat/email
Slack: "Hey, JWT_SECRET_KEY is xyz123..."
```

### Better Patterns ✅

```csharp
// ✅ Use environment variables or secrets manager
var mongoConnection = Environment.GetEnvironmentVariable("MONGODB_CONNECTION_STRING");

// ✅ Secure logging (mask sensitive fields)
_logger.LogInformation("User authenticated with identity: {UserId}", userId);

// ✅ Use .gitignore
# .gitignore
.env
.env.local
secrets.json

// ✅ Strong random secrets
var secret = Convert.ToBase64String(
    System.Security.Cryptography.RandomNumberGenerator
        .GetBytes(32)
);

// ✅ Use dedicated secrets manager
var client = new SecretsManagerClient();
var secret = await client.GetSecretAsync("fixit/prod/jwt-key");

// ✅ Different secrets per environment
PROD_DATABASE_PASSWORD=<strong-random>
STAGING_DATABASE_PASSWORD=<different-strong-random>
```

---

## Emergency Procedures

### Leaked API Key

1. **Immediate action:**
   ```bash
   # Disable key immediately
   aws secretsmanager update-secret --secret-id api-key \
     --secret-string "REVOKED"
   ```

2. **Rollback:**
   ```bash
   # If had backup key
   aws secretsmanager update-secret --secret-id api-key \
     --secret-string "$BACKUP_KEY"
   ```

3. **Regenerate:**
   ```bash
   # Generate new key with external service
   NEW_KEY=$(generate-new-key.sh)
   aws secretsmanager update-secret --secret-id api-key \
     --secret-string "$NEW_KEY"
   ```

4. **Investigate:**
   - Check audit logs for unauthorized access
   - Review git history for accidental commits
   - Scan production logs for leaked key usage

### Compromised Database Credentials

1. **Contain:** Revoke old credentials immediately
   ```bash
   # MongoDB Atlas
   Delete user account
   ```

2. **Create new user** with strong password
3. **Update secrets manager**
4. **Redeploy application**
5. **Monitor for suspicious queries**
6. **Notify ops team**

---

## Tools & Services

### Secret Managers (Recommended)

| Service | Best For | Cost | Setup Time |
|---------|----------|------|-----------|
| **Azure Key Vault** | Azure-first environments | $0.50/10k operations | 5 min |
| **AWS Secrets Manager** | AWS environments | $0.40/secret/month | 5 min |
| **Google Secret Manager** | GCP environments | $0.06/secret/month | 5 min |
| **HashiCorp Vault** | Self-hosted/hybrid | Open source | 1-2 hours |
| **Sealed Secrets** | Kubernetes clusters | Open source | 30 min |

### Local Development

- **dotnet user-secrets** - Built-in, no setup needed
- **.env file** - Simple, human-readable
- **1Password CLI** - Enterprise-grade local storage

### Monitoring & Rotation

- **Vault** - Automatic rotation policies
- **AWS Secrets Manager** - Lambda-based rotation
- **HashiCorp Vault** - Native rotation support

---

## Deployment Integration

### GitHub Actions + AWS Secrets Manager

```yaml
# .github/workflows/deploy.yml
jobs:
  deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      
      - name: Configure AWS credentials
        uses: aws-actions/configure-aws-credentials@v2
        with:
          role-to-assume: ${{ secrets.AWS_ROLE }}
          aws-region: us-east-1
      
      - name: Deploy with secrets
        run: |
          export MONGODB_CONNECTION_STRING=$(aws secretsmanager get-secret-value \
            --secret-id fixit/prod/mongodb --query SecretString --output text)
          docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d
```

---

## Compliance & Auditing

### PCI-DSS (Payment Cards)
- Secrets encrypted at rest and in transit
- Access logs maintained for 1 year
- Quarterly rotation of access keys

### HIPAA (Health Data)
- Secrets manager audit logging
- Encryption with HSM-backed keys
- Access control with least privilege

### GDPR (Personal Data)
- Data deletion policies
- Encryption for sensitive data
- Data retention limits

---

## Resources

- [OWASP Secrets Management](https://cheatsheetseries.owasp.org/cheatsheets/Secrets_Management_Cheat_Sheet.html)
- [Azure Key Vault Best Practices](https://docs.microsoft.com/azure/key-vault/general/best-practices)
- [AWS Secrets Manager Guide](https://docs.aws.amazon.com/secretsmanager/)
- [HashiCorp Vault Documentation](https://www.vaultproject.io/docs)

---

For deployment guides, see [DOCKER.md](../DOCKER.md) and [.github/CI-CD.md](../.github/CI-CD.md).
