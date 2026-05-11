# MongoDB SSL/TLS Connection Troubleshooting

## Issue
```
SSL Handshake failed with OpenSSL error - SSL_ERROR_SSL
error:0A000438:SSL routines::tlsv1 alert internal error
System.TimeoutException: A timeout occurred after 29999ms selecting a server...
```

## Root Cause (Railway Specific)
Railway container environments have issues with:
1. **Certificate revocation checking** - CRL/OCSP servers are not accessible
2. **OpenSSL compatibility** - Container OpenSSL version may not match MongoDB Atlas expectations
3. **CA certificate availability** - Let's Encrypt root certificates might not be properly cached

## Solutions Applied

### 1. ✅ Updated Dockerfile
Enhanced container SSL/TLS stack:
```dockerfile
RUN apt-get update && \
    apt-get install -y --no-install-recommends \
        curl \
        ca-certificates \
        libssl3 \
        openssl && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/* && \
    update-ca-certificates
```

### 2. ✅ Updated Program.cs MongoClient Configuration
Added explicit SSL settings with certificate revocation check disabled:
```csharp
if (mongoConnectionString.Contains("mongodb+srv://", StringComparison.OrdinalIgnoreCase))
{
    settings.UseTls = true;
    settings.AllowInsecureTls = false;
    
    // Disable certificate revocation checking for container environments
    var sslSettings = new SslSettings 
    { 
        CheckCertificateRevocation = false,
        EnabledProtocols = System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13
    };
    settings.SslSettings = sslSettings;
}
```

### 3. ✅ Updated appsettings.Production.json
Added MongoDB connection string parameter:
```json
"MongoDB": {
  "ConnectionString": "${MONGODB_URI:}?tlsDisableCertificateRevocationCheck=true"
}
```

### 4. ✅ Increased Server Selection Timeout
Changed `ServerSelectionTimeout` from 10 to 30 seconds to allow Railway's slower network:
```csharp
settings.ServerSelectionTimeout = TimeSpan.FromSeconds(30);
```

## MongoDB URI Format Requirements

Ensure your `MONGODB_URI` environment variable in Railway follows this format:
```
mongodb+srv://username:password@cluster.bzs2h7e.mongodb.net/database?retryWrites=true&w=majority
```

**Important:** Do NOT include `tlsDisableCertificateRevocationCheck` in the environment variable - it's added automatically by appsettings.Production.json

## Railway Deployment Checklist

1. **Set Environment Variables in Railway:**
   - `MONGODB_URI` - Your MongoDB Atlas connection string (format above)
   - `MONGODB_DATABASE` - Database name (e.g., `fixit-prod`)
   - Other required variables (API keys, JWT secret, etc.)

2. **MongoDB Atlas Whitelist:**
   - Go to MongoDB Atlas → Network Access → IP Whitelist
   - Add `0.0.0.0/0` to allow Railway's dynamic IPs (or use Railway VPC connector)

3. **Rebuild Container:**
   ```bash
   docker build -t fixit:latest .
   ```

4. **Push to Railway:**
   The app should now connect successfully to MongoDB Atlas

## Troubleshooting Additional Issues

If you still see SSL errors after these changes:

1. **Check MongoDB Atlas cluster status** - Ensure all replicas are healthy
2. **Verify credentials** - Ensure username/password in MONGODB_URI are correct
3. **Enable debug logging** in Program.cs:
   ```csharp
   settings.LoggingSettings = new LoggingSettings(new ConsoleEventSubscriber());
   ```
4. **Use Railway logs viewer** to see detailed error messages

## Testing Connection
To verify SSL/TLS connection works:

```bash
# Inside container
mongosh "mongodb+srv://user:password@cluster.bzs2h7e.mongodb.net" --eval "db.adminCommand('ping')"
```

## If Issues Persist

### Debug Option 1: Allow Insecure TLS (Development Only)
In `Program.cs`:
```csharp
settings.AllowInsecureTls = true;  // ⚠️ NOT for production
```

### Debug Option 2: Rebuild Clean
```bash
docker-compose down
docker-compose build --no-cache
docker-compose up
```

### Debug Option 3: Check OpenSSL Version
```bash
# Inside container
openssl version
# Should show: OpenSSL 3.x or higher
```

## MongoDB Atlas Checklist
- [ ] Connection string uses `mongodb+srv://`
- [ ] Username and password are URL-encoded (special chars like @, %, etc.)
- [ ] IP address is whitelisted in MongoDB Atlas Network Access
- [ ] Database user has correct permissions
- [ ] Connection string includes database name and query parameters

## Common Issues

| Issue | Cause | Solution |
|-------|-------|----------|
| `tlsv1 alert internal error` | Certificate validation issue | Ensure CA certificates are installed |
| `connection timeout` | Network/firewall issue | Check IP whitelist in MongoDB Atlas |
| `authentication failed` | Wrong credentials | Verify username/password in connection string |
| `SSL_ERROR_SSL` | OpenSSL version mismatch | Update base Docker image |

## References
- [MongoDB .NET Driver SSL/TLS](https://www.mongodb.com/docs/drivers/csharp/current/fundamentals/connection/tls/)
- [MongoDB Atlas Connection String](https://www.mongodb.com/docs/manual/reference/connection-string/)
