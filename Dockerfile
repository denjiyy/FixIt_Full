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

# FIX: Restore dependencies for all projects in the solution
RUN dotnet restore "FixIt.sln"

# Copy entire source code
COPY . .

# Build the main project and test project
RUN dotnet build "FixIt/FixIt.csproj" -c Release --no-restore
RUN dotnet build "FixIt.Tests/FixIt.Tests.csproj" -c Release --no-restore

# Run tests (fail fast on test failure)
RUN dotnet test "FixIt.Tests/FixIt.Tests.csproj" -c Release --no-build --logger "console;verbosity=minimal"

# Publish application
RUN dotnet publish "FixIt/FixIt.csproj" -c Release -o /app/publish --no-build

RUN ls /app/publish/wwwroot/lib/bootstrap/dist/css/ || echo "MISSING - bootstrap css not in publish output"

# Ensure static vendor libraries are present in publish output for runtime static file serving
RUN mkdir -p /app/publish/wwwroot/lib && \
    cp -a FixIt/wwwroot/lib/. /app/publish/wwwroot/lib/