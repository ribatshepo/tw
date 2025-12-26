# USP Docker Deployment Guide

This guide explains how to build and run the Unified Security Platform (USP) using Docker.

## Prerequisites

- Docker 20.10+ with BuildKit enabled
- Docker Compose 2.0+
- 8GB RAM minimum (16GB recommended)
- 20GB disk space

## Quick Start (Development)

### 1. Build the Image

```bash
cd /home/tshepo/projects/tw

# Build USP image
docker build -f services/usp/Dockerfile -t usp:dev .
```

### 2. Start the Stack

```bash
cd services/usp

# Start all services (USP + dependencies)
docker-compose up -d

# View logs
docker-compose logs -f usp

# Check health
curl http://localhost:8080/health/live
```

### 3. Access Services

- **USP API**: https://localhost:8443
- **Swagger UI**: https://localhost:8443/swagger
- **Prometheus**: http://localhost:9091
- **Grafana**: http://localhost:3000 (admin/admin)
- **Kibana**: http://localhost:5601
- **Jaeger**: http://localhost:16686
- **RabbitMQ**: http://localhost:15672 (usp_user/rabbitmq_password_dev)
- **MailHog**: http://localhost:8025

### 4. Stop the Stack

```bash
docker-compose down

# Remove volumes (WARNING: deletes all data)
docker-compose down -v
```

## Production Build

### 1. Set Environment Variables

```bash
# Create .env file
cat > .env <<EOF
# Version
VERSION=1.0.0
BUILD_DATE=$(date -u +'%Y-%m-%dT%H:%M:%SZ')
VCS_REF=$(git rev-parse --short HEAD)

# Registry
REGISTRY=myregistry.azurecr.io

# Database
DB_HOST=your-postgres-host
DB_PORT=5432
DB_NAME=usp_prod_db
DB_USERNAME=usp_user
DB_PASSWORD=your-secure-password

# Redis
REDIS_HOST=your-redis-host
REDIS_PORT=6379
REDIS_PASSWORD=your-redis-password

# RabbitMQ
RABBITMQ_HOST=your-rabbitmq-host
RABBITMQ_PORT=5671
RABBITMQ_USERNAME=usp_user
RABBITMQ_PASSWORD=your-rabbitmq-password
RABBITMQ_VHOST=/

# JWT
JWT_SECRET=your-256-bit-secret
JWT_ISSUER=usp-prod
JWT_AUDIENCE=usp-clients-prod

# Email
SMTP_SERVER=smtp.example.com
SMTP_PORT=587
SMTP_USERNAME=noreply@example.com
SMTP_PASSWORD=your-smtp-password
EMAIL_FROM=noreply@example.com
EMAIL_FROM_NAME=USP

# Observability
ELASTICSEARCH_URL=http://elasticsearch:9200
JAEGER_ENDPOINT=http://jaeger:4317

# Frontend
FRONTEND_URL=https://app.example.com

# Certificates
CERT_PATH=/path/to/certs
CERT_PASSWORD=your-cert-password
EOF
```

### 2. Build Production Image

```bash
cd /home/tshepo/projects/tw

docker build \
  --build-arg BUILD_CONFIGURATION=Release \
  --build-arg BUILD_VERSION=${VERSION} \
  --build-arg BUILD_DATE=${BUILD_DATE} \
  --build-arg VCS_REF=${VCS_REF} \
  -f services/usp/Dockerfile \
  -t ${REGISTRY}/usp:${VERSION} \
  -t ${REGISTRY}/usp:latest \
  .
```

### 3. Push to Registry

```bash
# Login to registry
docker login ${REGISTRY}

# Push images
docker push ${REGISTRY}/usp:${VERSION}
docker push ${REGISTRY}/usp:latest
```

### 4. Deploy with Docker Compose (Production)

```bash
cd services/usp

# Load environment variables
source .env

# Deploy
docker-compose -f docker-compose.prod.yml up -d
```

## Docker Image Details

### Size Optimization

The multi-stage Dockerfile produces a minimal runtime image:

- **Build stage**: ~2.5GB (includes SDK)
- **Runtime stage**: ~200-250MB (ASP.NET runtime only)

### Security Features

- ✅ Runs as non-root user (`uspuser`)
- ✅ Minimal base image (ASP.NET 8.0)
- ✅ No secrets in layers
- ✅ Health checks configured
- ✅ ReadyToRun compilation for faster startup

### Ports Exposed

| Port | Protocol | Purpose |
|------|----------|---------|
| 8443 | HTTPS | Primary REST API |
| 8080 | HTTP | Health checks |
| 50005 | gRPC/TLS | Inter-service communication |
| 9090 | HTTP | Prometheus metrics |

## Database Migrations

### Run Migrations Manually

```bash
# Connect to running container
docker exec -it usp-dev bash

# Run migrations
dotnet ef database update --project /app/USP.Api.dll
```

### Automatic Migrations on Startup

Migrations run automatically when the USP container starts (configured in `Program.cs`).

## Troubleshooting

### Container Won't Start

```bash
# Check logs
docker-compose logs usp

# Common issues:
# 1. Database not ready - wait for postgres health check
# 2. Missing secrets - check environment variables
# 3. Port conflicts - ensure ports are available
```

### Database Connection Errors

```bash
# Test database connectivity
docker exec -it usp-postgres psql -U usp_user -d usp_db -c "SELECT 1"

# Check network
docker network inspect usp-network
```

### High Memory Usage

```bash
# Check container stats
docker stats usp-dev

# Adjust memory limits in docker-compose.yml
deploy:
  resources:
    limits:
      memory: 1G
```

### SSL Certificate Errors

```bash
# For development, use self-signed certificates
# Generate dev certificate
dotnet dev-certs https -ep certs/usp.pfx -p DevCertPassword

# Mount certificate
# Volume: ./certs:/app/certs:ro
```

## Health Checks

### Liveness Probe

```bash
curl http://localhost:8080/health/live
```

### Readiness Probe

```bash
curl http://localhost:8080/health/ready
```

## Performance Tuning

### BuildKit Cache

```bash
# Enable BuildKit for faster builds
export DOCKER_BUILDKIT=1

# Build with inline cache
docker build \
  --cache-from ${REGISTRY}/usp:latest \
  --build-arg BUILDKIT_INLINE_CACHE=1 \
  -t usp:dev .
```

### Layer Caching

The Dockerfile is optimized for layer caching:
1. Dependencies restored first (rarely change)
2. Source code copied last (changes frequently)

## Security Scanning

### Scan with Docker Scout

```bash
docker scout cves usp:dev
docker scout recommendations usp:dev
```

### Scan with Trivy

```bash
trivy image usp:dev
```

### Scan with Snyk

```bash
snyk container test usp:dev --file=Dockerfile
```

## Kubernetes Deployment

For production Kubernetes deployment, use the Helm charts:

```bash
cd /home/tshepo/projects/tw/deploy/helm/usp

# Install
helm install usp . \
  --namespace usp \
  --create-namespace \
  --values values.prod.yaml
```

See `/deploy/helm/usp/README.md` for detailed Kubernetes deployment instructions.

## CI/CD Integration

Example GitHub Actions workflow:

```yaml
name: Build USP Docker Image

on:
  push:
    branches: [ main ]

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3

      - name: Build image
        run: |
          docker build \
            -f services/usp/Dockerfile \
            -t usp:${{ github.sha }} \
            .

      - name: Scan image
        run: trivy image usp:${{ github.sha }}

      - name: Push to registry
        run: |
          docker tag usp:${{ github.sha }} ${REGISTRY}/usp:latest
          docker push ${REGISTRY}/usp:latest
```

## Support

For issues or questions:
- Check logs: `docker-compose logs -f usp`
- Review documentation: `/docs/`
- Contact: devops@example.com
