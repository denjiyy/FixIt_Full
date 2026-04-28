# Docker Setup Documentation

## Overview

This project includes Docker configuration for both local development and production deployment.

### Files
- **Dockerfile** - Multi-stage build for production (includes tests and health checks)
- **docker-compose.yml** - Development environment with MongoDB
- **docker-compose.prod.yml** - Production overrides (security hardening)
- **.dockerignore** - Files excluded from Docker build

---

## Local Development Setup

### Prerequisites
- Docker & Docker Compose installed
- Port 5092 (app) and 27017 (MongoDB) available

### Quick Start

1. **Clone environment file:**
   ```bash
   cp .env.example .env
   ```

2. **Start services:**
   ```bash
   docker-compose up -d
   ```

3. **Verify services are running:**
   ```bash
   docker-compose ps
   ```

4. **View logs:**
   ```bash
   docker-compose logs -f fixit-app
   docker-compose logs -f mongodb
   ```

5. **Access the application:**
   - Web: http://localhost:5092
   - MongoDB: `mongodb://root:rootpassword@localhost:27017`

### Useful Commands

**Stop services:**
```bash
docker-compose down
```

**Clean up (including volumes):**
```bash
docker-compose down -v
```

**Rebuild after code changes:**
```bash
docker-compose build fixit-app
docker-compose up -d
```

**Run shell in container:**
```bash
docker-compose exec fixit-app /bin/bash
```

**Access MongoDB shell:**
```bash
docker-compose exec mongodb mongosh -u root -p rootpassword --authenticationDatabase admin
```

---

## Production Deployment

### Prerequisites
- Docker & Docker Compose installed
- Production database (MongoDB Atlas recommended)
- OAuth credentials configured
- Strong JWT secret key (32+ characters)
- SMTP credentials for email

### Setup

1. **Create production environment file:**
   ```bash
   cp .env.example .env.production
   # Edit with production values
   nano .env.production
   ```

2. **Deploy with production overrides:**
   ```bash
   docker-compose -f docker-compose.yml -f docker-compose.prod.yml --env-file .env.production up -d
   ```

3. **Monitor deployment:**
   ```bash
   docker-compose logs -f fixit-app
   ```

### Production Recommendations

1. **Reverse Proxy (Required)**
   - Use Nginx, Traefik, or cloud provider's load balancer
   - Handles HTTPS termination
   - Protects application from direct internet exposure

   Example Nginx config:
   ```nginx
   server {
       listen 443 ssl http2;
       server_name yourdomain.com;
       
       ssl_certificate /path/to/cert.pem;
       ssl_certificate_key /path/to/key.pem;
       
       location / {
           proxy_pass http://fixit-app:8080;
           proxy_set_header Host $host;
           proxy_set_header X-Real-IP $remote_addr;
           proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
           proxy_set_header X-Forwarded-Proto $scheme;
       }
   }
   ```

2. **Database (MongoDB Atlas)**
   - Don't run MongoDB in container for production
   - Use managed service with automatic backups
   - Enable IP whitelisting

3. **Environment Variables**
   - Use `.env.production` with strong secrets
   - **Never commit `.env.production` to git**
   - Use Docker secrets or secret management system

4. **Resource Limits**
   - docker-compose.prod.yml includes CPU/memory limits
   - Monitor actual usage and adjust

5. **Health Checks**
   - Application includes `/health/live` endpoint
   - Docker monitors container health
   - Orchestrator (K8s, Swarm) can auto-restart

6. **Logging**
   - Logs output to stdout (Docker captures)
   - Configure log driver: `json-file`, `awslogs`, `splunk`, etc.
   - Example: `docker logs fixit-app` or `docker-compose logs`

7. **Networking**
   - Docker creates isolated network `fixit-network`
   - Consider network policies for multi-container environments
   - Use internal DNS for service communication

---

## Kubernetes Deployment (Future)

For Kubernetes deployment, use Helm charts or raw manifests. The Docker image can be pushed to registry and deployed with:

```bash
docker build -t myregistry.azurecr.io/fixit:latest .
docker push myregistry.azurecr.io/fixit:latest
```

---

## Troubleshooting

### App won't start
```bash
docker-compose logs fixit-app
# Check MongoDB connection string, API keys
```

### MongoDB connection failed
```bash
docker-compose logs mongodb
# Verify MongoDB is healthy: docker-compose ps
```

### Port already in use
```bash
# Change ports in docker-compose.yml
# Or kill existing process
lsof -i :5092  # Find what's using port
```

### Rebuilding after dependencies change
```bash
docker-compose build --no-cache fixit-app
```

---

## Security Best Practices

✅ **Do:**
- Use strong JWT secret (32+ chars, random)
- Set unique MongoDB password
- Use environment variables for secrets
- Implement reverse proxy with HTTPS
- Limit container resources
- Run non-root user (configured in Dockerfile)
- Use MongoDB Atlas in production

❌ **Don't:**
- Commit `.env` files to git
- Use weak secrets
- Expose MongoDB port directly
- Run with `privileged: true`
- Skip health checks
- Disable rate limiting in production

---

## Performance Tuning

### MongoDB
- Connection pool: 100 max (configured in Program.cs)
- Consider indexes for frequently queried fields
- Monitor query performance

### Application
- Add caching layer (Redis) for frequently accessed data
- Configure static file caching
- Use CDN for assets

---

## Monitoring & Logging

### Docker Logs
```bash
# View real-time logs
docker-compose logs -f

# View last 100 lines
docker-compose logs --tail=100

# View logs since timestamp
docker-compose logs --since 2024-01-01
```

### Health Endpoint
```bash
curl http://localhost:5092/health/live
```

### Container Stats
```bash
docker stats
```

---

For questions or issues, see the main [README.md](README.md).
