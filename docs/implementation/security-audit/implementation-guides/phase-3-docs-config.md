# Phase 3: Documentation & Configuration - Week 3 Implementation Guide

**Phase:** 3 of 6
**Duration:** Week 3 (7 days)
**Focus:** P2 Medium Priority - Documentation, Configuration, Code Quality
**Team:** Backend + DevOps + Technical Writers
**Deliverable:** Complete documentation suite, production-ready configuration

---

## Overview

Phase 3 addresses all **15 P2 Medium Priority findings** focused on documentation completeness, configuration hardening, and code quality improvements. While not blocking production, these are essential for maintainability, developer onboarding, and operational excellence.

**Dependencies:** Phase 1 (P0) and Phase 2 (P1) must be complete.

---

## Findings Roadmap

| Day | Finding ID | Title | Hours | Team |
|-----|-----------|-------|-------|------|
| 13 | SEC-P2-001 | Root README Empty | 8h | Tech Writers |
| 13 | SEC-P2-002 | GETTING_STARTED Missing | 4h | Tech Writers |
| 14 | SEC-P2-003 | Stub READMEs Empty (6 files) | 6h | Tech Writers |
| 14 | SEC-P2-004 | Service Documentation Missing | 8h | Tech Writers |
| 15 | SEC-P2-005 | API.http Outdated | 4h | Backend |
| 15 | SEC-P2-006 | DEPLOYMENT Guide Missing | 8h | DevOps |
| 15 | SEC-P2-007 | TROUBLESHOOTING Missing | 8h | SRE |
| 15 | SEC-P2-008 | External Path References | 1h | Backend |
| 16 | SEC-P2-009 | Shell Shebang Portability | 1h | DevOps |
| 16 | SEC-P2-010 | Certificate Password Hardcoded | 2h | Security |
| 17 | SEC-P2-011 | Container Restart Limits Missing | 0.5h | DevOps |
| 17 | SEC-P2-012 | Dockerfiles Missing (5 services) | 6h | DevOps |
| 18 | SEC-P2-013 | XML Documentation Missing | 4h | Backend |
| 18 | SEC-P2-014 | AuthenticationService Naming | 0.25h | Backend |
| 19 | SEC-P2-015 | Magic Numbers to Constants | 2h | Backend |

**Total Effort:** 62.75 hours (7 days, ~9 hours/day with 2 people)

---

## Day 13: Core Documentation (12 hours)

### Objective
Create comprehensive root README and getting started guide.

### Prerequisites
- [ ] All P0 and P1 findings complete
- [ ] Services operational
- [ ] Documentation reviewed from specs

---

### Morning: Root README (8 hours)

**SEC-P2-001: Create Comprehensive Root README**

**Problem:** Root `README.md` contains only project title.

**Solution:** Create detailed README with architecture, setup instructions, and navigation.

#### Implementation Steps

**1. Create README.md (8 hours)**

```bash
cat > README.md <<'EOF'
# TW Platform - Global Business Management & ML

**Version:** 1.0.0
**Status:** Production-Ready
**License:** Proprietary

---

## 📋 Table of Contents

- [Overview](#overview)
- [Architecture](#architecture)
- [Core Services](#core-services)
- [Quick Start](#quick-start)
- [Documentation](#documentation)
- [Development](#development)
- [Deployment](#deployment)
- [Security](#security)
- [Contributing](#contributing)
- [Support](#support)

---

## Overview

The **TW Platform** is an enterprise-grade distributed system providing:

- **Distributed Computing**: ML training, batch/stream processing via UCCP
- **.NET Integration**: Seamless .NET client access via NCCS
- **Data Platform**: Columnar storage, SQL queries, data governance via UDPS
- **Security Platform**: Authentication, secrets management, encryption via USP
- **Stream Processing**: Ultra-low latency SIMD processing via Stream Compute

### Key Features

✅ **Multi-Tenant Isolation**: Namespace-based resource isolation with Row-Level Security
✅ **High Availability**: Raft-based consensus, automatic failover, circuit breakers
✅ **Zero Trust Security**: mTLS everywhere, granular RBAC/ABAC, HSM support
✅ **Observability**: Prometheus, Grafana, Jaeger, Elasticsearch integration
✅ **Compliance Ready**: SOC 2, HIPAA, PCI-DSS, GDPR certified architecture
✅ **ML Operations**: AutoML, model serving, feature stores, GPU/TPU scheduling

---

## Architecture

```
┌─────────────────────────────────────────────────┐
│           Client Applications                    │
│  (.NET, Web, Mobile, Console, Desktop)          │
└──────────────┬──────────────────────────────────┘
               │
    ┌──────────┼──────────┬──────────┬────────────┐
    │          │          │          │            │
┌───▼────┐ ┌──▼─────┐ ┌─▼──────┐ ┌─▼────────┐ ┌─▼──────────┐
│  UCCP  │ │  NCCS  │ │  UDPS  │ │   USP    │ │  Stream    │
│  (Go)  │ │(.NET 8)│ │(Scala) │ │ (.NET 8) │ │  (Rust)    │
│        │ │        │ │        │ │          │ │            │
│Control │ │.NET    │ │Data    │ │Security  │ │Real-time   │
│Plane   │ │Client  │ │Platform│ │Platform  │ │Processing  │
└────┬───┘ └────┬───┘ └────┬───┘ └────┬─────┘ └─────┬──────┘
     │          │          │          │             │
     └──────────┴──────────┴──────────┴─────────────┘
                         │
              ┌──────────┴───────────┐
              │  Infrastructure      │
              ├──────────────────────┤
              │  PostgreSQL 16       │
              │  Redis 7             │
              │  Kafka 3.6           │
              │  MinIO (S3)          │
              │  Prometheus/Grafana  │
              └──────────────────────┘
```

### Service Communication

All inter-service communication uses **gRPC with mutual TLS**:

- **Authentication**: mTLS certificate validation
- **Authorization**: Service identity from certificate CN
- **Encryption**: TLS 1.3 for all communication
- **Service Discovery**: UCCP maintains distributed registry

---

## Core Services

### 1. Unified Compute & Coordination Platform (UCCP)

**Technology:** Go 1.24+, Rust, Python 3.11+
**Ports:** gRPC/TLS 50000, HTTPS 8443, Metrics 9100
**Repository:** `services/uccp/`

**Purpose:** Central control plane for distributed computing and ML operations.

**Key Capabilities:**
- Raft-based distributed consensus
- Service discovery and health monitoring
- Task scheduling with GPU/TPU support
- ML model training (TensorFlow, PyTorch, JAX)
- Feature store and AutoML
- Distributed locking and leader election

**Quick Start:**
```bash
cd services/uccp
go build ./cmd/uccp
./uccp --config config/uccp.yaml
```

**API Endpoints:**
- `POST /api/v1/tasks` - Submit compute tasks
- `GET /api/v1/tasks/{id}` - Get task status
- `POST /api/v1/ml/train` - Train ML model
- `POST /api/v1/ml/serve` - Serve model for inference

---

### 2. .NET Compute Client Service (NCCS)

**Technology:** .NET 8, C# 12, ASP.NET Core
**Ports:** HTTPS 5001, gRPC/TLS 5002, Metrics 9200
**Repository:** `services/nccs/`

**Purpose:** .NET client interface to UCCP with REST and gRPC APIs.

**Key Capabilities:**
- REST API gateway with OpenAPI/Swagger
- SignalR for real-time communication
- NuGet SDK for .NET developers
- Redis caching and RabbitMQ messaging
- Polly resilience patterns (retry, circuit breaker, timeout)

**Quick Start:**
```bash
cd services/nccs
dotnet build
dotnet run --project src/NCCS.API
```

**NuGet Package:**
```bash
dotnet add package TW.NCCS.SDK --version 1.0.0
```

**Usage:**
```csharp
var client = new NCCSClient("https://nccs:5001");
var result = await client.SubmitTaskAsync(new TaskRequest
{
    Type = TaskType.Train,
    ModelType = "tensorflow",
    DatasetPath = "/data/training.parquet"
});
```

---

### 3. Unified Data Platform Service (UDPS)

**Technology:** Scala 2.13, Java 17, Apache Calcite, Arrow, Parquet
**Ports:** gRPC/TLS 50060, HTTPS 8443, Metrics 9090
**Repository:** `services/udps/`

**Purpose:** Columnar storage, SQL queries, and data governance.

**Key Capabilities:**
- Columnar storage with Parquet/ORC/Arrow
- SQL query engine with cost-based optimizer
- Data lineage and governance
- Time travel queries (point-in-time snapshots)
- ACID transactions with MVCC
- Real-time streaming ingest from Kafka

**Quick Start:**
```bash
cd services/udps
sbt compile
sbt run
```

**SQL Examples:**
```sql
-- Query historical data (time travel)
SELECT * FROM users AS OF TIMESTAMP '2025-01-01 00:00:00';

-- Create lineage-tracked table
CREATE TABLE analytics.revenue (
    date DATE,
    amount DECIMAL(10,2)
) WITH (
    lineage = true,
    retention_days = 365
);
```

---

### 4. Unified Security Platform (USP)

**Technology:** .NET 8, ASP.NET Core, C# 12
**Ports:** HTTPS 5001, gRPC/TLS 50005, Metrics 9091
**Repository:** `services/usp/`

**Purpose:** Authentication, authorization, secrets management, encryption.

**Key Capabilities:**
- JWT authentication with MFA (TOTP, Email, WebAuthn/FIDO2)
- RBAC and ABAC access control
- HashiCorp Vault-compatible secrets API
- AES-256-GCM encryption with HSM support
- Privileged Access Management (PAM)
- Automated credential rotation
- Audit logging with tamper-proof storage

**Quick Start:**
```bash
cd services/usp
dotnet build
dotnet run --project src/USP.API
```

**Authentication:**
```bash
# Login
curl -X POST https://usp:5001/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"admin123"}'

# Response: {"token":"eyJhbGc..."}
```

**Secrets Management:**
```bash
# Store secret
curl -X POST https://usp:5001/api/v1/secrets \
  -H "Authorization: Bearer $TOKEN" \
  -d '{"path":"/db/password","data":{"value":"secret123"}}'

# Retrieve secret
curl -X GET https://usp:5001/api/v1/secrets/db/password \
  -H "Authorization: Bearer $TOKEN"
```

---

### 5. Stream Compute Service

**Technology:** Rust 1.75+, Scala 2.12, Apache Flink, Kafka
**Ports:** gRPC/TLS 50060, Flink JobManager 8081, Metrics 9096
**Repository:** `services/stream-compute/`

**Purpose:** Ultra-low latency SIMD processing and stream analytics.

**Key Capabilities:**
- SIMD-accelerated processing (AVX2/AVX-512)
- Sub-millisecond latency (<1ms p99)
- Apache Flink for complex stream processing
- Complex Event Processing (CEP)
- Stateful stream joins
- Real-time anomaly detection

**Quick Start:**
```bash
cd services/stream-compute
cargo build --release
./target/release/stream-compute
```

**Stream Processing:**
```rust
// SIMD-accelerated aggregation
let result = stream
    .map_simd(|chunk| simd_transform(chunk))
    .window(Duration::seconds(10))
    .aggregate();
```

---

## Quick Start

### Prerequisites

- **Docker** 24.0+ and **Docker Compose** 2.23+
- **Kubernetes** 1.28+ with **Helm** 3.13+ (for production)
- **Go** 1.24+, **.NET** 8.0+, **Scala** 2.13+, **Rust** 1.75+

### Local Development

**1. Clone Repository**

```bash
git clone https://github.com/your-org/tw.git
cd tw
```

**2. Start Infrastructure**

```bash
# Start PostgreSQL, Redis, Kafka, MinIO
docker-compose -f docker-compose.infra.yml up -d

# Wait for services to be healthy
docker-compose -f docker-compose.infra.yml ps
```

**3. Generate TLS Certificates**

```bash
# Generate development certificates
bash scripts/generate-dev-certs.sh
```

**4. Initialize Database**

```bash
# Run migrations
export VAULT_TOKEN="dev-root-token"
bash scripts/db/apply-migrations.sh
```

**5. Start Services**

```bash
# Option 1: Docker Compose (recommended)
docker-compose up -d

# Option 2: Manual (for development)
# Terminal 1: UCCP
cd services/uccp && go run ./cmd/uccp

# Terminal 2: NCCS
cd services/nccs && dotnet run --project src/NCCS.API

# Terminal 3: UDPS
cd services/udps && sbt run

# Terminal 4: USP
cd services/usp && dotnet run --project src/USP.API

# Terminal 5: Stream Compute
cd services/stream-compute && cargo run
```

**6. Verify Services**

```bash
# Check all services are healthy
curl https://usp:5001/health
curl https://nccs:5001/health
curl http://uccp:8443/health
curl http://udps:8443/health
curl http://stream:8082/health
```

**7. Access UIs**

- **Grafana**: http://localhost:3000 (admin/admin)
- **Prometheus**: http://localhost:9090
- **Jaeger**: http://localhost:16686
- **Flink Dashboard**: http://localhost:8081

---

## Documentation

### Specifications

Detailed specifications in `docs/specs/`:

- **[unified-compute-coordination-platform.md](docs/specs/unified-compute-coordination-platform.md)** - UCCP and NCCS architecture
- **[data-platform.md](docs/specs/data-platform.md)** - UDPS columnar storage and SQL engine
- **[security.md](docs/specs/security.md)** - USP authentication, authorization, secrets
- **[streaming.md](docs/specs/streaming.md)** - Stream Compute SIMD engine

### Implementation Guides

- **[GETTING_STARTED.md](GETTING_STARTED.md)** - New developer onboarding
- **[DEPLOYMENT.md](DEPLOYMENT.md)** - Kubernetes deployment guide
- **[TROUBLESHOOTING.md](TROUBLESHOOTING.md)** - Common issues and solutions
- **[CODING_GUIDELINES.md](CODING_GUIDELINES.md)** - Code standards and patterns

### API Documentation

- **[USP API Reference](services/usp/API_REFERENCE.md)** - REST and gRPC APIs
- **[Swagger UI](https://usp:5001/swagger)** - Interactive API explorer
- **[Proto Definitions](proto/)** - gRPC service definitions

---

## Development

### Building Services

```bash
# Build all services
make build-all

# Build individual services
make build-uccp
make build-nccs
make build-udps
make build-usp
make build-stream
```

### Running Tests

```bash
# Run all tests
make test-all

# Unit tests per service
make test-unit-uccp
make test-unit-nccs
make test-unit-udps
make test-unit-usp
make test-unit-stream

# Integration tests
make test-integration

# End-to-end tests
make test-e2e

# Load tests
make test-load
```

### Code Quality

```bash
# Run linters
make lint

# Format code
make format

# Security scan
make security-scan
```

---

## Deployment

### Kubernetes Deployment

**1. Install Helm Charts**

```bash
# Add Helm repository
helm repo add tw https://charts.tw.local
helm repo update

# Install all services
helm install tw-platform tw/tw-platform \
  --namespace tw-platform \
  --create-namespace \
  --values values-production.yaml
```

**2. Verify Deployment**

```bash
# Check pods
kubectl get pods -n tw-platform

# Check services
kubectl get svc -n tw-platform

# Check ingresses
kubectl get ingress -n tw-platform
```

**3. Monitor Health**

```bash
# Port-forward to Grafana
kubectl port-forward -n monitoring svc/grafana 3000:80

# Open: http://localhost:3000
# Dashboard: "TW Platform - Overview"
```

See **[DEPLOYMENT.md](DEPLOYMENT.md)** for comprehensive deployment guide.

---

## Security

### Authentication

All API endpoints require JWT authentication:

```bash
# Get token
TOKEN=$(curl -X POST https://usp:5001/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"user","password":"pass"}' | jq -r '.token')

# Use token
curl -H "Authorization: Bearer $TOKEN" https://usp:5001/api/v1/secrets
```

### Secrets Management

Never commit secrets to git. Use USP Vault:

```bash
# Store secret
curl -X POST https://usp:5001/api/v1/secrets \
  -H "Authorization: Bearer $TOKEN" \
  -d '{"path":"/app/db-password","data":{"value":"secret"}}'

# Application retrieves at runtime
var dbPassword = await vaultClient.GetSecretAsync("/app/db-password");
```

### mTLS Certificates

All inter-service communication uses mTLS:

```bash
# Generate certificates (development)
bash scripts/generate-dev-certs.sh

# Production: Use cert-manager
kubectl apply -f deploy/kubernetes/cert-manager/
```

### Compliance

- **SOC 2 Type II**: Audit logging, access controls, encryption
- **HIPAA**: PHI encryption, audit trails, access logs
- **PCI-DSS**: No plaintext credentials, encrypted storage
- **GDPR**: Data lineage, right to deletion, consent tracking

---

## Contributing

### Development Workflow

1. **Create Feature Branch**
   ```bash
   git checkout -b feature/my-feature
   ```

2. **Write Tests**
   ```bash
   # TDD: Write tests first
   dotnet test  # or go test, sbt test, cargo test
   ```

3. **Implement Feature**
   ```csharp
   // Follow coding guidelines
   public async Task<Result> MyFeature() { ... }
   ```

4. **Run Linters**
   ```bash
   make lint
   make format
   ```

5. **Submit Pull Request**
   - Title: `[UCCP] Add distributed locking`
   - Description: Problem, solution, testing
   - Link to issue: `Closes #123`

### Code Review Standards

- ✅ All tests pass
- ✅ Code coverage ≥80%
- ✅ No security vulnerabilities (Trivy/Snyk)
- ✅ Documentation updated
- ✅ Changelog entry added

---

## Support

### Getting Help

- **Documentation**: See `docs/` directory
- **Issues**: [GitHub Issues](https://github.com/your-org/tw/issues)
- **Slack**: #tw-platform channel
- **Email**: support@tw.local

### Common Issues

See **[TROUBLESHOOTING.md](TROUBLESHOOTING.md)** for solutions to:

- Service startup failures
- Database connection errors
- Certificate issues
- Performance problems

---

## License

**Proprietary** - All rights reserved.

Copyright © 2025 TW Platform. Unauthorized copying or distribution is prohibited.

---

## Acknowledgments

Built with:

- [ASP.NET Core](https://docs.microsoft.com/aspnet/core) - .NET web framework
- [Go](https://golang.org) - Systems programming language
- [Apache Flink](https://flink.apache.org) - Stream processing
- [PostgreSQL](https://postgresql.org) - Relational database
- [Prometheus](https://prometheus.io) - Monitoring and alerting
- [Jaeger](https://jaegertracing.io) - Distributed tracing

---

**Built with ❤️ by the TW Platform Team**
EOF
```

---

### Afternoon: GETTING_STARTED Guide (4 hours)

**SEC-P2-002: Create Developer Onboarding Guide**

**Problem:** No step-by-step guide for new developers.

**Solution:** Create comprehensive getting started guide.

#### Implementation Steps

**1. Create GETTING_STARTED.md (4 hours)**

```bash
cat > GETTING_STARTED.md <<'EOF'
# Getting Started with TW Platform Development

**Estimated Setup Time:** 60-90 minutes

This guide will walk you through setting up a complete local development environment for the TW Platform.

---

## Table of Contents

1. [Prerequisites](#prerequisites)
2. [Environment Setup](#environment-setup)
3. [Infrastructure Setup](#infrastructure-setup)
4. [Service Setup](#service-setup)
5. [Verification](#verification)
6. [Your First Contribution](#your-first-contribution)
7. [Troubleshooting](#troubleshooting)

---

## Prerequisites

### Required Software

Install the following before proceeding:

#### Core Tools

- **Git** 2.40+
  ```bash
  # macOS
  brew install git

  # Ubuntu/Debian
  sudo apt install git

  # Verify
  git --version
  ```

- **Docker** 24.0+ and **Docker Compose** 2.23+
  ```bash
  # macOS
  brew install --cask docker

  # Ubuntu
  curl -fsSL https://get.docker.com | sh
  sudo usermod -aG docker $USER

  # Verify
  docker --version
  docker-compose --version
  ```

#### Development Runtimes

- **Go** 1.24+ (for UCCP)
  ```bash
  # macOS
  brew install go@1.24

  # Ubuntu
  wget https://go.dev/dl/go1.24.linux-amd64.tar.gz
  sudo tar -C /usr/local -xzf go1.24.linux-amd64.tar.gz
  export PATH=$PATH:/usr/local/go/bin

  # Verify
  go version
  ```

- **.NET SDK** 8.0+ (for USP, NCCS)
  ```bash
  # macOS
  brew install dotnet@8

  # Ubuntu
  wget https://dot.net/v1/dotnet-install.sh
  bash dotnet-install.sh --channel 8.0

  # Verify
  dotnet --version
  ```

- **Scala** 2.13+ and **sbt** 1.9+ (for UDPS)
  ```bash
  # macOS
  brew install scala sbt

  # Ubuntu
  echo "deb https://repo.scala-sbt.org/scalasbt/debian all main" | sudo tee /etc/apt/sources.list.d/sbt.list
  sudo apt update && sudo apt install sbt

  # Verify
  scala -version
  sbt --version
  ```

- **Rust** 1.75+ (for Stream Compute)
  ```bash
  # All platforms
  curl --proto '=https' --tlsv1.2 -sSf https://sh.rustup.rs | sh

  # Verify
  rustc --version
  cargo --version
  ```

#### Optional Tools

- **kubectl** 1.28+ (for Kubernetes deployments)
- **Helm** 3.13+ (for Helm charts)
- **jq** (JSON processing)
- **curl** (API testing)

---

## Environment Setup

### 1. Clone Repository

```bash
# Clone repository
git clone https://github.com/your-org/tw.git
cd tw

# Verify structure
ls -la
# Expected directories: services/, docs/, proto/, config/, deploy/
```

### 2. Configure Environment Variables

```bash
# Copy example environment file
cp .env.example .env

# Edit .env with your settings
nano .env
```

**Required variables:**

```bash
# Infrastructure
POSTGRES_HOST=localhost
POSTGRES_PORT=5432
REDIS_HOST=localhost
REDIS_PORT=6379
KAFKA_BROKER=localhost:9092

# USP Vault
VAULT_TOKEN=dev-root-token
VAULT_ADDR=https://localhost:5001

# Observability
PROMETHEUS_URL=http://localhost:9090
GRAFANA_URL=http://localhost:3000
JAEGER_URL=http://localhost:16686
```

### 3. Install Dependencies

```bash
# Go dependencies (UCCP)
cd services/uccp
go mod download
cd ../..

# .NET dependencies (USP, NCCS)
cd services/usp
dotnet restore
cd ../nccs
dotnet restore
cd ../..

# Scala dependencies (UDPS)
cd services/udps
sbt update
cd ../..

# Rust dependencies (Stream Compute)
cd services/stream-compute
cargo fetch
cd ../..
```

---

## Infrastructure Setup

### 1. Start Infrastructure Services

```bash
# Start PostgreSQL, Redis, Kafka, MinIO
docker-compose -f docker-compose.infra.yml up -d

# Wait for services to be healthy (60 seconds)
echo "Waiting for infrastructure to be ready..."
sleep 60

# Verify all services are running
docker-compose -f docker-compose.infra.yml ps
```

**Expected output:**

```
NAME                  STATUS    PORTS
postgres              Up        5432->5432
redis                 Up        6379->6379
kafka                 Up        9092->9092
zookeeper             Up        2181->2181
minio                 Up        9000->9000
```

### 2. Generate Development Certificates

```bash
# Generate self-signed certificates for mTLS
bash scripts/generate-dev-certs.sh

# Verify certificates created
ls -la config/certs/
# Expected: ca.crt, ca.key, server.crt, server.key, client.crt, client.key
```

### 3. Initialize Database

```bash
# Set Vault token for credential retrieval
export VAULT_TOKEN="dev-root-token"

# First, start USP to initialize Vault
cd services/usp
dotnet run --project src/USP.API &
USP_PID=$!

# Wait for USP to start (30 seconds)
sleep 30

# Store database credentials in Vault
bash scripts/db/seed-vault-credentials.sh

# Run database migrations
bash scripts/db/apply-migrations.sh

# Stop USP (will restart later)
kill $USP_PID
cd ../..
```

**Verify database initialized:**

```bash
psql -h localhost -U postgres -c "\l"
# Expected databases: postgres, usp_dev, uccp_dev, nccs_dev, udps_dev, stream_dev
```

---

## Service Setup

### 1. Start USP (Unified Security Platform)

```bash
cd services/usp

# Build
dotnet build

# Run
export VAULT_TOKEN="dev-root-token"
dotnet run --project src/USP.API

# In another terminal, verify USP is running
curl -k https://localhost:5001/health
# Expected: {"status":"Healthy","vault":{"sealed":false}}
```

### 2. Unseal USP Vault

```bash
# Get unseal keys from initial setup
cat config/vault-unseal-keys.txt

# Unseal with 3 of 5 keys
curl -k -X POST https://localhost:5001/api/v1/vault/seal/unseal \
  -H "X-Vault-Token: $VAULT_TOKEN" \
  -d '{"key":"<unseal-key-1>"}'

curl -k -X POST https://localhost:5001/api/v1/vault/seal/unseal \
  -H "X-Vault-Token: $VAULT_TOKEN" \
  -d '{"key":"<unseal-key-2>"}'

curl -k -X POST https://localhost:5001/api/v1/vault/seal/unseal \
  -H "X-Vault-Token: $VAULT_TOKEN" \
  -d '{"key":"<unseal-key-3>"}'

# Verify unsealed
curl -k https://localhost:5001/api/v1/vault/status
# Expected: {"sealed":false,"unsealProgress":0}
```

### 3. Start NCCS (.NET Compute Client)

```bash
# New terminal
cd services/nccs

dotnet build
dotnet run --project src/NCCS.API

# Verify
curl https://localhost:5001/health
```

### 4. Start UCCP (Unified Compute & Coordination)

```bash
# New terminal
cd services/uccp

go build ./cmd/uccp
./uccp --config config/uccp.yaml

# Verify
curl http://localhost:8443/health
```

### 5. Start UDPS (Unified Data Platform)

```bash
# New terminal
cd services/udps

sbt compile
sbt run

# Verify
curl http://localhost:8443/health
```

### 6. Start Stream Compute

```bash
# New terminal
cd services/stream-compute

cargo build --release
./target/release/stream-compute

# Verify
curl http://localhost:8082/health
```

---

## Verification

### 1. Check All Services

```bash
# Script to check all services
bash scripts/verify-services.sh
```

**Expected output:**

```
✅ USP       - https://localhost:5001/health - Healthy
✅ NCCS      - https://localhost:5001/health - Healthy
✅ UCCP      - http://localhost:8443/health - Healthy
✅ UDPS      - http://localhost:8443/health - Healthy
✅ Stream    - http://localhost:8082/health - Healthy
✅ Postgres  - localhost:5432 - Connected
✅ Redis     - localhost:6379 - Connected
✅ Kafka     - localhost:9092 - Connected
```

### 2. Test Authentication

```bash
# Create admin user
curl -k -X POST https://localhost:5001/api/v1/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "username": "admin",
    "email": "admin@tw.local",
    "password": "Admin123!"
  }'

# Login
TOKEN=$(curl -k -X POST https://localhost:5001/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"Admin123!"}' | jq -r '.token')

echo "JWT Token: $TOKEN"
```

### 3. Test Secrets Management

```bash
# Store a secret
curl -k -X POST https://localhost:5001/api/v1/secrets \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "path": "/test/my-secret",
    "data": {
      "username": "testuser",
      "password": "testpass"
    }
  }'

# Retrieve secret
curl -k -X GET https://localhost:5001/api/v1/secrets/test/my-secret \
  -H "Authorization: Bearer $TOKEN"

# Expected: {"path":"/test/my-secret","data":{"username":"testuser","password":"testpass"}}
```

### 4. Test Metrics

```bash
# Check USP metrics
curl http://localhost:9091/metrics | grep usp_login_attempts

# Check NCCS metrics
curl http://localhost:9200/metrics | grep nccs_requests

# Check UCCP metrics
curl http://localhost:9100/metrics | grep uccp_tasks
```

---

## Your First Contribution

### Example: Add New API Endpoint to USP

**1. Create Feature Branch**

```bash
git checkout -b feature/add-user-profile-endpoint
```

**2. Write Test (TDD)**

Create `tests/unit/USP.API.Tests/Controllers/ProfileControllerTests.cs`:

```csharp
[Fact]
public async Task GetProfile_ReturnsUserProfile()
{
    // Arrange
    var userId = "test-user-id";
    var controller = new ProfileController(_mockUserService.Object);

    // Act
    var result = await controller.GetProfile(userId);

    // Assert
    var okResult = Assert.IsType<OkObjectResult>(result);
    var profile = Assert.IsType<UserProfile>(okResult.Value);
    Assert.Equal(userId, profile.UserId);
}
```

**3. Implement Feature**

Create `src/USP.API/Controllers/ProfileController.cs`:

```csharp
[ApiController]
[Route("api/v1/profile")]
public class ProfileController : ControllerBase
{
    private readonly IUserService _userService;

    [HttpGet("{userId}")]
    [RequirePermission("profile", "read")]
    public async Task<IActionResult> GetProfile(string userId)
    {
        var profile = await _userService.GetProfileAsync(userId);
        return Ok(profile);
    }
}
```

**4. Run Tests**

```bash
cd services/usp
dotnet test

# Expected: All tests pass
```

**5. Commit and Push**

```bash
git add .
git commit -m "feat(usp): Add user profile endpoint

- Add ProfileController with GET /api/v1/profile/{userId}
- Add unit tests for ProfileController
- Add RequirePermission attribute for authorization

Closes #123"

git push origin feature/add-user-profile-endpoint
```

**6. Create Pull Request**

- Title: `[USP] Add user profile endpoint`
- Description: Explain problem, solution, testing
- Link issue: `Closes #123`

---

## Troubleshooting

### Service Won't Start

**Problem:** `dotnet run` fails with "Address already in use"

**Solution:**
```bash
# Find process using port 5001
lsof -i :5001

# Kill process
kill -9 <PID>

# Or use different port
dotnet run --urls "https://localhost:5002"
```

---

### Database Connection Fails

**Problem:** `Npgsql.NpgsqlException: Connection refused`

**Solution:**
```bash
# Check PostgreSQL is running
docker ps | grep postgres

# Check logs
docker logs postgres

# Restart PostgreSQL
docker-compose -f docker-compose.infra.yml restart postgres
```

---

### Vault Sealed

**Problem:** `{"sealed":true}` when calling USP

**Solution:**
```bash
# Unseal vault with 3 keys
# (See "Unseal USP Vault" section above)
```

---

## Next Steps

- **Read Architecture Specs**: See `docs/specs/` for detailed design
- **Explore Examples**: See `examples/` directory for sample code
- **Join Slack**: #tw-platform for questions
- **Attend Standup**: Daily at 9:00 AM

**Welcome to the TW Platform team! 🎉**
EOF
```

### Deliverable (Day 13)
- [ ] Comprehensive root README.md created (1,500+ lines)
- [ ] Architecture diagrams included
- [ ] All 5 services documented with quick start guides
- [ ] API examples provided
- [ ] GETTING_STARTED.md created (1,000+ lines)
- [ ] Step-by-step setup instructions with verification
- [ ] First contribution guide included

---

## Day 14: Stub READMEs & Service Documentation (14 hours)

### Morning: Fill Stub READMEs (6 hours)

**SEC-P2-003: Fill 6 Empty README Files**

**Problem:** 6 README files exist with only headings.

**Solution:** Fill each with comprehensive documentation.

#### Files to Complete

1. **proto/README.md** - Protocol Buffers documentation
2. **config/README.md** - Configuration management
3. **deploy/README.md** - Deployment guides
4. **tests/integration/README.md** - Integration testing
5. **tests/e2e/README.md** - End-to-end testing
6. **tests/load/README.md** - Load testing

**Implementation:** See detailed content in finding document SEC-P2-003.

---

### Afternoon: Service Documentation (8 hours)

**SEC-P2-004: Document Undocumented Services**

**Problem:** 4 of 5 services lack documentation (only USP documented).

**Solution:** Create comprehensive READMEs for UCCP, NCCS, UDPS, Stream Compute.

#### Services to Document

1. **services/uccp/README.md** - UCCP architecture and API
2. **services/nccs/README.md** - NCCS .NET client
3. **services/udps/README.md** - UDPS data platform
4. **services/stream-compute/README.md** - Stream processing

**Implementation:** See detailed content in finding document SEC-P2-004.

### Deliverable (Day 14)
- [ ] All 6 stub READMEs filled with comprehensive content
- [ ] 4 service READMEs created (UCCP, NCCS, UDPS, Stream)
- [ ] API examples included in each service README
- [ ] Configuration examples provided
- [ ] Testing guides included

---

## Day 15: Operational Documentation (21 hours)

### Morning: API.http & DEPLOYMENT (12 hours)

**SEC-P2-005: Update USP.API.http File** (4 hours)

```http
### USP API - Comprehensive HTTP Client Collection

@baseUrl = https://localhost:5001
@token = {{login.response.body.$.token}}

### 1. HEALTH CHECK
GET {{baseUrl}}/health

### 2. AUTHENTICATION - Register
POST {{baseUrl}}/api/v1/auth/register
Content-Type: application/json

{
  "username": "testuser",
  "email": "test@tw.local",
  "password": "Test123!"
}

### 3. AUTHENTICATION - Login
# @name login
POST {{baseUrl}}/api/v1/auth/login
Content-Type: application/json

{
  "username": "testuser",
  "password": "Test123!"
}

### 4. AUTHENTICATION - MFA Setup
POST {{baseUrl}}/api/v1/auth/mfa/setup
Authorization: Bearer {{token}}
Content-Type: application/json

{
  "method": "totp"
}

### 5. SECRETS - Create
POST {{baseUrl}}/api/v1/secrets
Authorization: Bearer {{token}}
Content-Type: application/json

{
  "path": "/database/postgres",
  "data": {
    "username": "postgres",
    "password": "secret123"
  }
}

### 6. SECRETS - List
GET {{baseUrl}}/api/v1/secrets?path=/database
Authorization: Bearer {{token}}

### 7. SECRETS - Get
GET {{baseUrl}}/api/v1/secrets/database/postgres
Authorization: Bearer {{token}}

### 8. SECRETS - Update
PUT {{baseUrl}}/api/v1/secrets/database/postgres
Authorization: Bearer {{token}}
Content-Type: application/json

{
  "data": {
    "username": "postgres",
    "password": "newsecret456"
  }
}

### 9. SECRETS - Delete
DELETE {{baseUrl}}/api/v1/secrets/database/postgres
Authorization: Bearer {{token}}

### 10. VAULT - Seal
POST {{baseUrl}}/api/v1/vault/seal
X-Vault-Token: {{$dotenv VAULT_TOKEN}}

### 11. VAULT - Unseal (Key 1)
POST {{baseUrl}}/api/v1/vault/seal/unseal
X-Vault-Token: {{$dotenv VAULT_TOKEN}}
Content-Type: application/json

{
  "key": "unseal-key-1"
}

### 12. VAULT - Status
GET {{baseUrl}}/api/v1/vault/status

# ... (25+ more endpoints)
```

**SEC-P2-006: Create DEPLOYMENT.md** (8 hours)

Comprehensive Kubernetes deployment guide with Helm charts, rollback procedures, monitoring setup.

**Implementation:** See detailed content in finding document SEC-P2-006 (too large to include here).

---

### Afternoon: Troubleshooting Guide (9 hours)

**SEC-P2-007: Create TROUBLESHOOTING.md** (8 hours)

**SEC-P2-008: Fix External Path References** (1 hour)

Edit `CODING_GUIDELINES.md`:

```markdown
# Before:
See examples at /Users/username/projects/examples

# After:
See examples at examples/coding-patterns/
```

### Deliverable (Day 15)
- [ ] USP.API.http updated with 27+ endpoints
- [ ] DEPLOYMENT.md created with Helm charts
- [ ] TROUBLESHOOTING.md created with solutions
- [ ] External path references fixed
- [ ] All documentation cross-referenced

---

## Days 16-17: Configuration Hardening (7.5 hours)

Implementation of shell script portability, certificate password randomization, Docker restart limits, and Dockerfile creation for all 5 services.

**Detailed steps:** See finding documents SEC-P2-009 through SEC-P2-012.

---

## Days 18-19: Code Quality (6.25 hours)

Implementation of XML documentation, naming convention fixes, and magic number extraction.

**Detailed steps:** See finding documents SEC-P2-013 through SEC-P2-015.

---

## End of Week 3: Verification Checklist

### Documentation Completeness

- [ ] Root README.md comprehensive and up-to-date
- [ ] GETTING_STARTED.md provides step-by-step onboarding
- [ ] All 6 stub READMEs filled
- [ ] All 4 services documented (UCCP, NCCS, UDPS, Stream)
- [ ] USP.API.http contains all 27+ endpoints
- [ ] DEPLOYMENT.md covers Kubernetes/Helm deployment
- [ ] TROUBLESHOOTING.md addresses common issues
- [ ] No external path references in documentation

### Configuration Security

- [ ] Shell scripts use portable shebangs (`#!/usr/bin/env bash`)
- [ ] Certificate passwords generated randomly and stored in Vault
- [ ] Docker Compose has restart limits (max 5 attempts)
- [ ] All 5 services have production-ready Dockerfiles
- [ ] Dockerfiles use non-root users
- [ ] Health checks configured in all Dockerfiles

### Code Quality

- [ ] XML documentation on all public APIs
- [ ] Parameter naming follows conventions (no `_` prefix)
- [ ] All magic numbers extracted to constants
- [ ] Code compiles without warnings
- [ ] Static analysis passes

---

## Handoff to Phase 4

**Phase 3 Complete Criteria:**
- All 15 P2 findings resolved
- Complete documentation suite for onboarding
- Production-ready configuration
- Code quality improved

**Phase 4 Preview:**
- Service implementation features (Weeks 4-12)
- ML operations, data governance, advanced features
- Integration across all 5 services

---

**Status:** Ready to Start
**Last Updated:** 2025-12-27
**Phase Owner:** Backend + DevOps + Technical Writing Teams
