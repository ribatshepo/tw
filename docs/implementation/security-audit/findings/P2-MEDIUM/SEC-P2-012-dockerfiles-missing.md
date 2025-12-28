# SEC-P2-012: Dockerfiles Missing

## 1. Metadata

| Field | Value |
|-------|-------|
| **Finding ID** | SEC-P2-012 |
| **Title** | No Dockerfiles Found for Service Containerization |
| **Priority** | P2 - MEDIUM |
| **Severity** | Medium |
| **Category** | Configuration / Infrastructure |
| **Status** | Not Started |
| **Effort Estimate** | 6 hours |
| **Implementation Phase** | Phase 3 (Week 3, Day 8-9) |
| **Assigned To** | DevOps Engineer |
| **Created** | 2025-12-27 |

---

## 2. Cross-References

| Type | Reference |
|------|-----------|
| **Audit Report** | `/home/tshepo/projects/tw/COMPREHENSIVE_AUDIT_REPORT.md:352-355` |
| **Code Files** | None (Dockerfiles missing) |
| **Dependencies** | None |
| **Compliance Impact** | SOC 2 (CC8.1 - Deployment Consistency) |

---

## 3. Executive Summary

### Problem

Services not yet containerized. No Dockerfiles found for USP, UCCP, NCCS, UDPS, or Stream Compute.

### Impact

- **No Container Deployment:** Cannot deploy services to Kubernetes
- **Development/Production Parity:** Different environments run services differently
- **No Image Scanning:** Cannot scan for vulnerabilities without images

### Solution

Create secure, multi-stage Dockerfiles for all services with non-root users and minimal base images.

---

## 4. Implementation Guide

### Template 1: USP Dockerfile (1.5 hours)

```dockerfile
# services/usp/Dockerfile

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src

# Copy project files
COPY src/USP.API/USP.API.csproj src/USP.API/
COPY src/USP.Core/USP.Core.csproj src/USP.Core/
COPY src/USP.Infrastructure/USP.Infrastructure.csproj src/USP.Infrastructure/

# Restore dependencies
RUN dotnet restore src/USP.API/USP.API.csproj

# Copy source code
COPY src/ src/

# Build application
RUN dotnet build src/USP.API/USP.API.csproj -c Release -o /app/build
RUN dotnet publish src/USP.API/USP.API.csproj -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS runtime
WORKDIR /app

# Create non-root user
RUN addgroup -g 1000 usp && \
    adduser -D -u 1000 -G usp usp

# Copy published app
COPY --from=build /app/publish .

# Change ownership
RUN chown -R usp:usp /app

# Switch to non-root user
USER usp

# Expose ports
EXPOSE 5001 9091

# Health check
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
  CMD wget --no-verbose --tries=1 --spider https://localhost:5001/health || exit 1

# Entry point
ENTRYPOINT ["dotnet", "USP.API.dll"]
```

### Template 2: UCCP Dockerfile (1.5 hours)

```dockerfile
# services/uccp/Dockerfile

# Build stage
FROM golang:1.24-alpine AS build
WORKDIR /src

# Install build dependencies
RUN apk add --no-cache git make

# Copy go mod files
COPY go.mod go.sum ./
RUN go mod download

# Copy source code
COPY . .

# Build binary
RUN CGO_ENABLED=0 GOOS=linux go build -a -installsuffix cgo -o /app/uccp ./cmd/uccp

# Runtime stage
FROM alpine:3.19 AS runtime
WORKDIR /app

# Install runtime dependencies
RUN apk add --no-cache ca-certificates

# Create non-root user
RUN addgroup -g 1000 uccp && \
    adduser -D -u 1000 -G uccp uccp

# Copy binary
COPY --from=build /app/uccp .

# Change ownership
RUN chown -R uccp:uccp /app

# Switch to non-root user
USER uccp

# Expose ports
EXPOSE 8080 50000

# Health check
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
  CMD wget --no-verbose --tries=1 --spider http://localhost:8080/health || exit 1

# Entry point
ENTRYPOINT ["./uccp"]
```

### Template 3: NCCS Dockerfile (1 hour)

```dockerfile
# services/nccs/Dockerfile
# Similar to USP, but for NCCS service

FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src

COPY src/NCCS.API/NCCS.API.csproj src/NCCS.API/
RUN dotnet restore src/NCCS.API/NCCS.API.csproj

COPY src/ src/
RUN dotnet publish src/NCCS.API/NCCS.API.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS runtime
WORKDIR /app

RUN addgroup -g 1000 nccs && \
    adduser -D -u 1000 -G nccs nccs

COPY --from=build /app/publish .
RUN chown -R nccs:nccs /app

USER nccs
EXPOSE 5001 5002
HEALTHCHECK CMD wget --spider http://localhost:5001/health || exit 1
ENTRYPOINT ["dotnet", "NCCS.API.dll"]
```

### Template 4: UDPS Dockerfile (1 hour)

```dockerfile
# services/udps/Dockerfile

# Build stage
FROM sbtscala/scala-sbt:eclipse-temurin-jammy-17.0.5_8_1.8.2_2.13.10 AS build
WORKDIR /src

# Copy sbt files
COPY build.sbt .
COPY project/ project/

# Download dependencies
RUN sbt update

# Copy source code
COPY src/ src/

# Build fat JAR
RUN sbt assembly

# Runtime stage
FROM eclipse-temurin:17-jre-alpine AS runtime
WORKDIR /app

# Create non-root user
RUN addgroup -g 1000 udps && \
    adduser -D -u 1000 -G udps udps

# Copy JAR
COPY --from=build /src/target/scala-2.13/udps-assembly-1.0.0.jar app.jar

# Change ownership
RUN chown -R udps:udps /app

USER udps
EXPOSE 8080 50060
HEALTHCHECK CMD wget --spider http://localhost:8080/health || exit 1
ENTRYPOINT ["java", "-jar", "app.jar"]
```

### Template 5: Stream Compute Dockerfile (1 hour)

```dockerfile
# services/stream-compute/Dockerfile

# Build stage
FROM rust:1.75-alpine AS build
WORKDIR /src

# Install build dependencies
RUN apk add --no-cache musl-dev

# Copy Cargo files
COPY Cargo.toml Cargo.lock ./

# Copy source code
COPY src/ src/

# Build release binary
RUN cargo build --release

# Runtime stage
FROM alpine:3.19 AS runtime
WORKDIR /app

# Install runtime dependencies
RUN apk add --no-cache libgcc

# Create non-root user
RUN addgroup -g 1000 stream && \
    adduser -D -u 1000 -G stream stream

# Copy binary
COPY --from=build /src/target/release/stream-compute .

# Change ownership
RUN chown -R stream:stream /app

USER stream
EXPOSE 8080 50060
HEALTHCHECK CMD wget --spider http://localhost:8080/health || exit 1
ENTRYPOINT ["./stream-compute"]
```

### Step 6: Create .dockerignore Files (30 minutes)

```bash
# services/usp/.dockerignore
bin/
obj/
*.user
*.suo
.vs/
.env
certs/
vault-unseal-keys.json

# services/uccp/.dockerignore
.git/
.idea/
*.test
vendor/
tmp/

# services/udps/.dockerignore
target/
.bsp/
.idea/
project/target/

# services/stream-compute/.dockerignore
target/
.cargo/
```

---

## 5. Testing

- [ ] All 5 service Dockerfiles created
- [ ] Images build successfully
- [ ] Images run without root user
- [ ] Health checks working
- [ ] Image sizes optimized (multi-stage builds)
- [ ] Vulnerability scans pass (Trivy/Snyk)

---

## 6. Compliance Evidence

**SOC 2 CC8.1:** Consistent deployment via containers

---

## 7. Sign-Off

- [ ] **DevOps:** All Dockerfiles created and tested
- [ ] **Security:** Images scanned, no critical vulnerabilities

---

**Finding Status:** Not Started
**Last Updated:** 2025-12-27

---

**End of SEC-P2-012**
