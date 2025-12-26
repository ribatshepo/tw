# =============================================================================
# Build Stage
# =============================================================================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS builder

# OCI Labels - Build Stage
LABEL org.opencontainers.image.title="USP - Unified Security Platform"
LABEL org.opencontainers.image.description="Production-ready .NET 8 Security Platform for Authentication, Authorization, Secrets Management, and PAM"
LABEL org.opencontainers.image.version="1.0.0"
LABEL org.opencontainers.image.authors="GBMM Engineering Team"
LABEL org.opencontainers.image.vendor="GBMM"
LABEL org.opencontainers.image.licenses="Proprietary"
LABEL org.opencontainers.image.source="https://github.com/yourorg/tw"
LABEL org.opencontainers.image.documentation="https://docs.example.com/usp"
LABEL maintainer="devops@example.com"
LABEL dotnet.version="8.0"
LABEL stage="builder"

WORKDIR /src

# Copy solution file
COPY ["services/usp/USP.sln", "./"]

# Copy project files for dependency restoration (layer caching optimization)
COPY ["services/usp/src/USP.Api/USP.Api.csproj", "src/USP.Api/"]
COPY ["services/usp/src/USP.Core/USP.Core.csproj", "src/USP.Core/"]
COPY ["services/usp/src/USP.Infrastructure/USP.Infrastructure.csproj", "src/USP.Infrastructure/"]
COPY ["services/usp/src/USP.Shared/USP.Shared.csproj", "src/USP.Shared/"]

# Copy test project files (optional, for building test projects)
COPY ["services/usp/tests/USP.UnitTests/USP.UnitTests.csproj", "tests/USP.UnitTests/"]
COPY ["services/usp/tests/USP.IntegrationTests/USP.IntegrationTests.csproj", "tests/USP.IntegrationTests/"]
COPY ["services/usp/tests/USP.E2ETests/USP.E2ETests.csproj", "tests/USP.E2ETests/"]

# Restore NuGet packages with caching
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore "src/USP.Api/USP.Api.csproj"

# Copy remaining source code
COPY ["services/usp/", "./"]

# Build arguments for versioning
ARG BUILD_CONFIGURATION=Release
ARG BUILD_VERSION=1.0.0
ARG BUILD_DATE
ARG VCS_REF

WORKDIR "/src/src/USP.Api"

# Build application
RUN dotnet build "USP.Api.csproj" \
    -c ${BUILD_CONFIGURATION} \
    -o /app/build \
    --no-restore \
    /p:Version=${BUILD_VERSION}

# Publish application with optimizations
RUN dotnet publish "USP.Api.csproj" \
    -c ${BUILD_CONFIGURATION} \
    -o /app/publish \
    --no-restore \
    --no-build \
    /p:UseAppHost=false \
    /p:Version=${BUILD_VERSION} \
    /p:PublishReadyToRun=true \
    /p:PublishReadyToRunShowWarnings=true

# =============================================================================
# Runtime Stage
# =============================================================================
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime

# OCI Labels - Runtime Stage
LABEL org.opencontainers.image.title="USP - Unified Security Platform"
LABEL org.opencontainers.image.description="Production-ready .NET 8 Security Platform"
LABEL org.opencontainers.image.version="1.0.0"
LABEL org.opencontainers.image.authors="GBMM Engineering Team"
LABEL org.opencontainers.image.vendor="GBMM"
LABEL org.opencontainers.image.licenses="Proprietary"
LABEL maintainer="devops@example.com"
LABEL stage="runtime"

# Install curl for health checks
RUN apt-get update && \
    apt-get install -y --no-install-recommends curl && \
    rm -rf /var/lib/apt/lists/*

# Set working directory
WORKDIR /app

# Create directory for certificates (mTLS support)
RUN mkdir -p /app/certs && \
    mkdir -p /app/logs

# Copy published application from builder
COPY --from=builder /app/publish .

# Configure environment variables
# Note: Sensitive values should be provided at runtime via secrets
ENV ASPNETCORE_URLS="https://+:8443;http://+:8080;https://+:50005" \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    ASPNETCORE_Kestrel__Certificates__Default__Path=/app/certs/usp.pfx \
    TZ=UTC

# Expose ports
# 8443: HTTPS (primary REST API)
# 8080: HTTP (health checks, fallback)
# 50005: gRPC with TLS
# 9090: Prometheus metrics
EXPOSE 8443 8080 50005 9090

# Health check using the /health/live endpoint
# Uses HTTP port 8080 to avoid certificate issues
HEALTHCHECK --interval=30s --timeout=5s --start-period=60s --retries=3 \
    CMD curl -f http://localhost:8080/health/live || exit 1

# Create non-root user for security
RUN groupadd -r usp && \
    useradd -r -g usp -u 1000 uspuser && \
    chown -R uspuser:usp /app

# Run as non-root user
USER uspuser

# Start application
ENTRYPOINT ["dotnet", "USP.Api.dll"]
