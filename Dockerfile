# Multi-stage build for FixIt ASP.NET Core 9.0 application
# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and project files
COPY ["FixIt.sln", "."]
COPY ["FixIt/FixIt.csproj", "FixIt/"]
COPY ["FixIt.Models/FixIt.Models.csproj", "FixIt.Models/"]
COPY ["FixIt.Services/FixIt.Services.csproj", "FixIt.Services/"]
COPY ["FixIt.ViewModels/FixIt.ViewModels.csproj", "FixIt.ViewModels/"]
COPY ["FixIt.Data/FixIt.Data.csproj", "FixIt.Data/"]
COPY ["FixIt.Tests/FixIt.Tests.csproj", "FixIt.Tests/"]

# Restore dependencies
RUN dotnet restore "FixIt.sln"

# Copy entire source code
COPY . .

# Build the solution
RUN dotnet build "FixIt.sln" -c Release --no-restore

# Run tests (fail fast on test failure)
RUN dotnet test "FixIt.Tests/FixIt.Tests.csproj" -c Release --no-build --logger "console;verbosity=minimal"

# Publish application
RUN dotnet publish "FixIt/FixIt.csproj" -c Release -o /app/publish --no-build

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Install curl, ca-certificates, and modern OpenSSL for MongoDB Atlas TLS support
RUN apt-get update && \
    apt-get install -y --no-install-recommends \
        curl \
        ca-certificates \
        libssl3 \
        openssl && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/* && \
    update-ca-certificates

# Configure OpenSSL for Railway container environment
# Disable certificate verification at OpenSSL level for MongoDB Atlas
ENV OPENSSL_CONF=/etc/ssl/openssl.cnf
ENV SSL_CERT_FILE=/etc/ssl/certs/ca-certificates.crt
ENV SSL_CERT_DIR=/etc/ssl/certs
ENV OPENSSL_ALLOW_PROXY_CERTS=1

# Create minimal openssl config for Railway - reduce security level to allow connections
RUN mkdir -p /tmp && cat > /tmp/openssl.cnf << 'EOF'
[ default_conf ]
ssl_conf = ssl_sect

[ssl_sect]
system_default = system_default_sect

[system_default_sect]
MinProtocol = TLSv1.0
MaxProtocol = TLSv1.3
CipherString = DEFAULT:@SECLEVEL=1
EOF

RUN cat /tmp/openssl.cnf >> /etc/ssl/openssl.cnf 2>/dev/null || true

# Copy published application from build stage
COPY --from=build /app/publish .

# Create non-root user for security
RUN useradd -m -u 1000 appuser \
    && mkdir -p /app/wwwroot/uploads /app/data-protection-keys \
    && chown -R appuser:appuser /app
USER appuser

# Expose port
EXPOSE 8080

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:8080/health/live || exit 1

# Start application
ENTRYPOINT ["dotnet", "FixIt.dll"]
