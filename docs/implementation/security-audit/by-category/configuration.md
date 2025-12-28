# Configuration & Infrastructure - Category Consolidation

**Category:** Configuration & Infrastructure
**Total Findings:** 3
**Total Effort:** 7.5 hours
**Implementation Phase:** Phase 3 (Week 3, Days 7-9)

---

## Overview

This document consolidates all findings related to configuration management, Docker infrastructure, and deployment setup.

## Findings Summary

| Finding ID | Title | Priority | Effort | Focus |
|-----------|-------|----------|--------|-------|
| SEC-P2-011 | Container Restart Limits Missing | P2 - MEDIUM | 0.5h | Docker Compose |
| SEC-P2-012 | Dockerfiles Missing | P2 - MEDIUM | 6h | Containerization |
| SEC-P1-010 | Schema Scripts Lack Transaction Wrapping | P1 - HIGH | 2h | Database |

**Total Effort:** 8.5 hours

**Note:** SEC-P1-010 technically belongs to Database/Infrastructure category but included here for configuration context.

---

## Critical Path Analysis

### Pre-Production (P1) - Week 2, Day 9

**SEC-P1-010: SQL Transaction Wrapping (2h)**
- **Impact:** Failed migrations leave database in inconsistent state
- **Risk:** Manual cleanup required after migration failures
- **Fix:** Wrap all DDL in BEGIN/COMMIT transactions

### Post-Production Enhancement (P2) - Week 3, Days 7-9

**SEC-P2-011: Container Restart Limits (30 minutes)**
- **Impact:** Infinite restart loops consume resources
- **Risk:** Resource exhaustion, log spam
- **Fix:** Add restart policies with max-attempts

**SEC-P2-012: Dockerfiles Missing (6 hours)**
- **Impact:** Cannot deploy services to Kubernetes
- **Risk:** No containerization strategy
- **Fix:** Create multi-stage Dockerfiles for all 5 services

---

## Infrastructure Architecture

### Current State

```
┌────────────────────────────────────┐
│  docker-compose.infra.yml          │
│  (Infrastructure Only)             │
├────────────────────────────────────┤
│  ✅ PostgreSQL                     │
│  ✅ Redis                          │
│  ✅ Kafka                          │
│  ✅ MinIO                          │
│  ⚠️  No restart limits             │
└────────────────────────────────────┘

┌────────────────────────────────────┐
│  Application Services              │
│  (Not Containerized)               │
├────────────────────────────────────┤
│  ❌ No Dockerfiles                 │
│  ❌ Running via dotnet run/go run  │
│  ❌ Not deployable to K8s          │
└────────────────────────────────────┘
```

### Target State

```
┌────────────────────────────────────┐
│  docker-compose.infra.yml          │
│  (Infrastructure with Limits)      │
├────────────────────────────────────┤
│  ✅ PostgreSQL (restart: 5 max)   │
│  ✅ Redis (restart: 5 max)        │
│  ✅ Kafka (restart: 5 max)        │
│  ✅ Monitoring stack               │
└────────────────────────────────────┘

┌────────────────────────────────────┐
│  Application Services              │
│  (Fully Containerized)             │
├────────────────────────────────────┤
│  ✅ USP Dockerfile (.NET)          │
│  ✅ UCCP Dockerfile (Go)           │
│  ✅ NCCS Dockerfile (.NET)         │
│  ✅ UDPS Dockerfile (Scala/Java)   │
│  ✅ Stream Dockerfile (Rust)       │
│  ✅ K8s Deployments                │
└────────────────────────────────────┘
```

---

## Implementation Strategy

### Phase 1: Database Configuration (Week 2, Day 9) - 2 hours

**SEC-P1-010: SQL Transaction Wrapping (2h)**

Update all schema scripts with transaction wrapping:

```sql
-- 04-uccp-schema.sql
BEGIN;  -- ✅ Add transaction wrapper

-- Grant schema access
GRANT USAGE ON SCHEMA uccp TO uccp_user;

-- Create tables
CREATE TABLE uccp.task_definitions (
    task_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    -- ... table definition
);

-- Create indexes
CREATE INDEX idx_tasks_status ON uccp.tasks(status);

-- If any statement fails, entire transaction rolls back
COMMIT;  -- ✅ Commit all changes
```

Apply to all schema scripts:
- `04-uccp-schema.sql`
- `05-usp-schema.sql`
- `06-nccs-schema.sql`
- `07-udps-schema.sql`
- `08-stream-schema.sql`

Test rollback behavior:
```bash
# Create test script with intentional error
cat > test-rollback.sql <<'EOF'
BEGIN;
CREATE TABLE test.table1 (id INT);
CREATE TABLE test.table2 (id INT);
CREATE TABLE test.SYNTAX ERROR;  -- Intentional error
COMMIT;
EOF

# Run script
psql -f test-rollback.sql
# Expected: ERROR, tables not created (rollback worked)
```

### Phase 2: Docker Restart Policies (Week 3, Day 7) - 30 minutes

**SEC-P2-011: Container Restart Limits (30m)**

```yaml
# docker-compose.infra.yml

services:
  postgres:
    image: postgres:16-alpine
    restart: on-failure:5  # ✅ Max 5 restart attempts
    deploy:
      restart_policy:
        condition: on-failure
        max_attempts: 5
        window: 120s
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U postgres"]
      interval: 10s
      timeout: 5s
      retries: 5

  redis:
    image: redis:7-alpine
    restart: on-failure:5
    deploy:
      restart_policy:
        condition: on-failure
        max_attempts: 5
        window: 120s
    healthcheck:
      test: ["CMD", "redis-cli", "ping"]
      interval: 10s
      timeout: 5s
      retries: 5

  kafka:
    image: bitnami/kafka:3.6
    restart: on-failure:5
    deploy:
      restart_policy:
        condition: on-failure
        max_attempts: 5
        window: 120s
    healthcheck:
      test: ["CMD-SHELL", "kafka-broker-api-versions.sh --bootstrap-server localhost:9092"]
      interval: 30s
      timeout: 10s
      retries: 5
```

### Phase 3: Service Containerization (Week 3, Days 7-9) - 6 hours

**SEC-P2-012: Create Dockerfiles (6h)**

**1. USP Dockerfile (.NET) - 1.5h**

```dockerfile
# services/usp/Dockerfile

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src

COPY src/USP.API/USP.API.csproj src/USP.API/
COPY src/USP.Core/USP.Core.csproj src/USP.Core/
COPY src/USP.Infrastructure/USP.Infrastructure.csproj src/USP.Infrastructure/

RUN dotnet restore src/USP.API/USP.API.csproj

COPY src/ src/
RUN dotnet publish src/USP.API/USP.API.csproj -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS runtime
WORKDIR /app

# Create non-root user
RUN addgroup -g 1000 usp && adduser -D -u 1000 -G usp usp

COPY --from=build /app/publish .
RUN chown -R usp:usp /app

USER usp
EXPOSE 5001 9091

HEALTHCHECK --interval=30s --timeout=5s --retries=3 \
  CMD wget --spider https://localhost:5001/health || exit 1

ENTRYPOINT ["dotnet", "USP.API.dll"]
```

**2. UCCP Dockerfile (Go) - 1.5h**

```dockerfile
# services/uccp/Dockerfile

FROM golang:1.24-alpine AS build
WORKDIR /src

RUN apk add --no-cache git make

COPY go.mod go.sum ./
RUN go mod download

COPY . .
RUN CGO_ENABLED=0 GOOS=linux go build -a -installsuffix cgo -o /app/uccp ./cmd/uccp

FROM alpine:3.19 AS runtime
WORKDIR /app

RUN apk add --no-cache ca-certificates

RUN addgroup -g 1000 uccp && adduser -D -u 1000 -G uccp uccp

COPY --from=build /app/uccp .
RUN chown -R uccp:uccp /app

USER uccp
EXPOSE 8080 50000

HEALTHCHECK CMD wget --spider http://localhost:8080/health || exit 1

ENTRYPOINT ["./uccp"]
```

**3. NCCS Dockerfile (.NET) - 1h**

```dockerfile
# services/nccs/Dockerfile (similar to USP)

FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src

COPY src/NCCS.API/NCCS.API.csproj src/NCCS.API/
RUN dotnet restore src/NCCS.API/NCCS.API.csproj

COPY src/ src/
RUN dotnet publish src/NCCS.API/NCCS.API.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS runtime
WORKDIR /app

RUN addgroup -g 1000 nccs && adduser -D -u 1000 -G nccs nccs

COPY --from=build /app/publish .
RUN chown -R nccs:nccs /app

USER nccs
EXPOSE 5001 5002
HEALTHCHECK CMD wget --spider http://localhost:5001/health || exit 1
ENTRYPOINT ["dotnet", "NCCS.API.dll"]
```

**4. UDPS Dockerfile (Scala/Java) - 1h**

```dockerfile
# services/udps/Dockerfile

FROM sbtscala/scala-sbt:eclipse-temurin-jammy-17.0.5_8_1.8.2_2.13.10 AS build
WORKDIR /src

COPY build.sbt .
COPY project/ project/
RUN sbt update

COPY src/ src/
RUN sbt assembly

FROM eclipse-temurin:17-jre-alpine AS runtime
WORKDIR /app

RUN addgroup -g 1000 udps && adduser -D -u 1000 -G udps udps

COPY --from=build /src/target/scala-2.13/udps-assembly-1.0.0.jar app.jar
RUN chown -R udps:udps /app

USER udps
EXPOSE 8080 50060
HEALTHCHECK CMD wget --spider http://localhost:8080/health || exit 1
ENTRYPOINT ["java", "-jar", "app.jar"]
```

**5. Stream Compute Dockerfile (Rust) - 1h**

```dockerfile
# services/stream-compute/Dockerfile

FROM rust:1.75-alpine AS build
WORKDIR /src

RUN apk add --no-cache musl-dev

COPY Cargo.toml Cargo.lock ./
COPY src/ src/

RUN cargo build --release

FROM alpine:3.19 AS runtime
WORKDIR /app

RUN apk add --no-cache libgcc

RUN addgroup -g 1000 stream && adduser -D -u 1000 -G stream stream

COPY --from=build /src/target/release/stream-compute .
RUN chown -R stream:stream /app

USER stream
EXPOSE 8080 50060
HEALTHCHECK CMD wget --spider http://localhost:8080/health || exit 1
ENTRYPOINT ["./stream-compute"]
```

---

## Testing Strategy

### SQL Transaction Testing

```bash
# Test rollback on failure
psql -f services/usp/migrations/sql/05-usp-schema.sql

# Verify idempotent execution (can re-run after failure)
psql -f services/usp/migrations/sql/05-usp-schema.sql
# Expected: SUCCESS or "already exists" errors (harmless)
```

### Docker Restart Testing

```bash
# Kill container repeatedly
for i in {1..10}; do docker kill postgres; sleep 5; done

# Check restart count
docker inspect postgres | jq '.[0].RestartCount'
# Expected: ≤ 5 (then stopped)
```

### Container Build Testing

```bash
# Build all images
docker build -t usp:latest services/usp/
docker build -t uccp:latest services/uccp/
docker build -t nccs:latest services/nccs/
docker build -t udps:latest services/udps/
docker build -t stream:latest services/stream-compute/

# Verify images created
docker images | grep -E "usp|uccp|nccs|udps|stream"

# Test image runs
docker run --rm -p 5001:5001 usp:latest

# Verify health check
docker inspect usp:latest | jq '.[0].Config.Healthcheck'
```

---

## Success Criteria

✅ **Complete when:**
- All schema scripts wrapped in BEGIN/COMMIT transactions
- All docker-compose services have restart limits
- Dockerfiles created for all 5 services
- All images build successfully
- All containers run with non-root users
- Health checks working on all containers
- Images pass vulnerability scans (Trivy/Snyk)

---

**Status:** Not Started
**Last Updated:** 2025-12-27
**Category Owner:** DevOps + Infrastructure Team
