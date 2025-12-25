# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

The **tw** repository is a multi-service distributed platform for GBMM (Global Business Management and ML), designed to provide enterprise-grade distributed computing, ML operations, data management, security, and real-time stream processing capabilities.

**Current Status:** Specification/design phase. The repository contains comprehensive architectural specifications in `docs/specs/` but no implementation code yet.

## Core Services Architecture

The platform consists of five primary services that work together:

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
│ (Go/   │ │(.NET 8)│ │(Scala/ │ │ (.NET 8) │ │  Compute   │
│ Rust)  │ │        │ │ Java)  │ │          │ │(Rust/Flink)│
│        │ │        │ │        │ │          │ │            │
│Control │ │.NET    │ │Data    │ │Security  │ │Real-time   │
│Plane   │ │Client  │ │Platform│ │Platform  │ │Processing  │
└────────┘ └────────┘ └────────┘ └──────────┘ └────────────┘
```

### 1. Unified Compute & Coordination Platform (UCCP)
- **Technology:** Go 1.24, Rust, Python 3.11+
- **Ports:** gRPC/TLS 50000, HTTPS 8443, Raft 50061, Metrics 9100
- **Purpose:** Central control plane for distributed computing, service coordination, and ML operations
- **Key Features:**
  - Raft-based distributed consensus
  - Service discovery and registration
  - Task scheduling with GPU/TPU support
  - ML training and serving (TensorFlow, PyTorch, JAX, XGBoost)
  - Feature store and AutoML
  - Distributed locking and leader election

### 2. .NET Compute Client Service (NCCS)
- **Technology:** .NET 8, C# 12, ASP.NET Core
- **Ports:** HTTPS 5001, gRPC/TLS 5002, Metrics 9200
- **Purpose:** .NET client interface to UCCP
- **Key Features:**
  - REST API gateway with OpenAPI/Swagger
  - SignalR for real-time communication
  - NuGet SDK package for .NET developers
  - Redis caching and RabbitMQ integration
  - Polly resilience patterns

### 3. Unified Data Platform Service (UDPS)
- **Technology:** Scala 2.13, Java 17, Apache Calcite, Arrow, Parquet
- **Ports:** gRPC/TLS 50060, HTTPS 8443, Metrics 9090, Health 8081
- **Purpose:** Columnar storage, SQL query execution, and data cataloging
- **Key Features:**
  - Columnar storage with multi-codec compression
  - SQL query engine with cost-based optimizer
  - Data lineage tracking and governance
  - Time travel queries and ACID transactions
  - Real-time streaming ingest

### 4. Unified Security Platform (USP)
- **Technology:** .NET 8, ASP.NET Core, C# 12
- **Ports:** HTTPS 8443, HTTPS 5001, gRPC 50005, Metrics 9090
- **Purpose:** Authentication, authorization, secrets management, and encryption
- **Key Features:**
  - JWT authentication with MFA and WebAuthn/FIDO2
  - RBAC and ABAC access control
  - Vault-compatible secrets management
  - AES-256-GCM encryption with HSM support
  - Privileged Access Management (PAM)
  - SOC 2, HIPAA, PCI-DSS compliance

### 5. Stream Compute Service
- **Technology:** Rust, Scala 2.12, Apache Flink, Kafka
- **Ports:** gRPC 50060 (TLS), Flink JobManager 8081, Metrics 9096, Health 8082
- **Purpose:** Ultra-low-latency SIMD processing and stream analytics
- **Key Features:**
  - SIMD-accelerated processing (AVX2/AVX-512)
  - Ultra-low latency (<1ms p99) with Rust
  - Apache Flink for advanced stream processing
  - Complex event processing (CEP)
  - Stateful stream joins and anomaly detection

## Technology Stack Summary

| Service | Primary Languages | Key Technologies |
|---------|------------------|------------------|
| UCCP | Go, Rust, Python | Raft, gRPC, Ray, TensorFlow, PyTorch |
| NCCS | C# 12, .NET 8 | ASP.NET Core, SignalR, Entity Framework |
| UDPS | Scala, Java | Calcite, Arrow, Parquet, Akka |
| USP | C# 12, .NET 8 | ASP.NET Core, PostgreSQL, HSM |
| Stream Compute | Rust, Scala | Apache Flink, Kafka, SIMD |

## Specification Documents

All comprehensive specifications are located in `docs/specs/`:

1. **`unified-compute-coordination-platform.md`** (82KB)
   - UCCP control plane architecture
   - NCCS .NET client service (included in same file)
   - Service coordination, ML ops, distributed computing

2. **`data-platform.md`** (58KB)
   - UDPS architecture
   - Columnar storage, SQL engine, data catalog
   - Data governance and lineage tracking

3. **`security.md`** (70KB)
   - USP architecture
   - Authentication, authorization, secrets management
   - Encryption, PAM, compliance

4. **`streaming.md`** (36KB)
   - Stream Compute Service architecture
   - Rust SIMD engine and Flink processing
   - Real-time analytics and CEP

**Read these specs first** when working on any service - they contain detailed API designs, data models, deployment configurations, and architectural decisions.

## Inter-Service Communication

All services communicate via **gRPC with mTLS** for security:

- **Authentication:** All inter-service calls use mutual TLS certificates
- **Authorization:** Service identities verified via certificate CNs
- **Encryption:** TLS 1.3 for all gRPC communication
- **Service Discovery:** UCCP maintains service registry

### Key Integration Points

- **NCCS → UCCP:** gRPC client for all compute/ML operations
- **UDPS → UCCP:** Service registration and coordination
- **USP → All Services:** Authentication, secrets, encryption services
- **Stream Compute → Kafka:** SSL/SASL secured event streaming
- **All Services → USP:** Secret retrieval and credential rotation

## Infrastructure Dependencies

When implementing services, these infrastructure components are required:

**Storage:**
- PostgreSQL (metadata, transactional data)
- Redis (caching, sessions)
- MinIO/S3 (object storage, ML artifacts, checkpoints)

**Messaging & Streaming:**
- Apache Kafka (event streaming, CDC)
- RabbitMQ (message queuing)

**Observability:**
- Prometheus (metrics collection)
- Jaeger (distributed tracing)
- Elasticsearch (log aggregation)
- Grafana (visualization)

**Security & Secrets:**
- Certificate Authority (mTLS certificates)

**Note:** Secrets management is provided by USP service (application layer), not external infrastructure.

**Orchestration:**
- Kubernetes (container orchestration)
- Docker (containerization)

## Expected Development Commands

Once implementation begins, the following patterns are expected (based on specifications):

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

# Build with optimizations
make build-release
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

# Load/performance tests
make test-load
```

### Local Development

```bash
# Start local infrastructure (PostgreSQL, Redis, Kafka, etc.)
docker-compose up -d infrastructure

# Start all services locally
docker-compose up -d

# View logs
docker-compose logs -f [service-name]

# Stop all services
docker-compose down
```

### Service-Specific Commands

**Go (UCCP):**
```bash
cd services/uccp
go build ./cmd/uccp
go test ./...
go test -run TestSpecificFunction
```

**.NET (NCCS, USP):**
```bash
cd services/nccs  # or services/usp
dotnet build
dotnet test
dotnet test --filter "FullyQualifiedName~SpecificTest"
dotnet run
```

**Scala/Java (UDPS):**
```bash
cd services/udps
sbt compile
sbt test
sbt "testOnly *SpecificSpec"
sbt run
```

**Rust (Stream Compute):**
```bash
cd services/stream-compute
cargo build
cargo test
cargo test specific_test
cargo run
```

## Key Architectural Patterns

Understanding these patterns is essential when working across services:

### Distributed Coordination
- **Raft Consensus:** UCCP uses Raft for distributed state management
- **Leader Election:** Automatic failover with lease-based leadership
- **Distributed Locking:** Lease-based locks with configurable TTL

### Multi-Tenancy
- **Namespace Isolation:** All resources scoped to namespaces
- **Quota Management:** Per-namespace and per-user resource limits
- **Access Control:** RBAC/ABAC enforced at all service boundaries

### Resilience Patterns
- **Circuit Breakers:** Prevent cascading failures
- **Retries with Backoff:** Exponential backoff for transient failures
- **Bulkhead Isolation:** Resource pools to limit blast radius
- **Timeouts:** All operations have configurable timeouts

### Observability
- **Structured Logging:** JSON logs with correlation IDs
- **Distributed Tracing:** OpenTelemetry/Jaeger spans for all requests
- **Metrics:** Prometheus metrics on all services (port 909X)
- **Health Checks:** All services expose `/health` and `/ready` endpoints

### Security by Default
- **mTLS Everywhere:** All inter-service communication encrypted
- **Secrets Rotation:** Automatic credential rotation via USP
- **Audit Logging:** All privileged operations logged to tamper-proof storage
- **Zero Trust:** No implicit trust between services

## Development Workflow Expectations

When implementation begins:

1. **Start with Specifications:** Read the relevant spec in `docs/specs/` first
2. **Infrastructure First:** Ensure local infrastructure is running (docker-compose)
3. **TLS Certificates:** Generate dev certificates for mTLS testing
4. **Service Registration:** Register services with UCCP for discovery
5. **Integration Tests:** Write tests that span multiple services
6. **Observability:** Ensure metrics, logs, and traces are working
7. **Security:** Never bypass mTLS, always use USP for secrets

## Notes for Implementation

- **gRPC First:** All services expose gRPC APIs; REST is secondary (NCCS only)
- **Proto Definitions:** Define Protocol Buffers in `proto/` directory
- **Configuration:** Use environment variables + config files (YAML/JSON)
- **Database Migrations:** Version-controlled migrations for PostgreSQL schemas
- **Kubernetes:** Helm charts in `deploy/helm/` for each service
- **Documentation:** Update specs as implementation details are finalized
