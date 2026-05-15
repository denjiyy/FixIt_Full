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
RUN dotnet restore "FixIt/FixIt.csproj"

# Copy entire source code
COPY . .

# Build the solution
RUN dotnet build "FixIt/FixIt.csproj" -c Release --no-restore

# Run tests (fail fast on test failure)
RUN dotnet test "FixIt.Tests/FixIt.Tests.csproj" -c Release --no-build --logger "console;verbosity=minimal"

# Publish application
RUN dotnet publish "FixIt/FixIt.csproj" -c Release -o /app/publish --no-build

RUN ls /app/publish/wwwroot/lib/bootstrap/dist/css/ || echo "MISSING - bootstrap css not in publish output"

# Ensure static vendor libraries are present in publish output for runtime static file serving
RUN mkdir -p /app/publish/wwwroot/lib && \
    cp -a FixIt/wwwroot/lib/. /app/publish/wwwroot/lib/

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Install curl and CA certificates
RUN apt-get update && \
    apt-get install -y --no-install-recommends \
        curl \
        ca-certificates && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/* && \
    update-ca-certificates

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
