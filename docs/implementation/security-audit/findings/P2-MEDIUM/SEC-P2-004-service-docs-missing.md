# SEC-P2-004: Service Documentation Missing

## 1. Metadata

| Field | Value |
|-------|-------|
| **Finding ID** | SEC-P2-004 |
| **Title** | Four Services Have No README Documentation |
| **Priority** | P2 - MEDIUM |
| **Severity** | Medium |
| **Category** | Documentation |
| **Status** | Not Started |
| **Effort Estimate** | 12 hours |
| **Implementation Phase** | Phase 3 (Week 3, Day 3-5) |
| **Assigned To** | Service Teams (Go/Scala/Rust/C# Engineers) |
| **Created** | 2025-12-27 |

---

## 2. Cross-References

| Type | Reference |
|------|-----------|
| **Audit Report** | `/home/tshepo/projects/tw/COMPREHENSIVE_AUDIT_REPORT.md:494-500` |
| **Code Files** | Missing: `services/uccp/README.md`, `services/nccs/README.md`, `services/udps/README.md`, `services/stream-compute/README.md` |
| **Good Example** | `/home/tshepo/projects/tw/services/usp/` has excellent documentation |
| **Dependencies** | None |
| **Compliance Impact** | SOC 2 (CC1.4 - Documentation) |

---

## 3. Executive Summary

### Problem

Four services have no README documentation:
- UCCP (Unified Compute & Coordination Platform)
- NCCS (.NET Compute Client Service)
- UDPS (Unified Data Platform Service)
- Stream Compute Service

USP documentation is excellent (3 detailed guides) - use as template.

### Impact

- **No Service Overview:** Developers don't understand service purpose/architecture
- **Missing Setup Instructions:** Cannot run services locally
- **No API Documentation:** Endpoints and usage unclear

### Solution

Create comprehensive README.md for each service following USP documentation pattern.

---

## 4. Implementation Guide

### Template: services/[SERVICE]/README.md

Each service README should include:

1. **Service Overview** - What does this service do?
2. **Architecture** - High-level component diagram
3. **Prerequisites** - Dependencies and tools
4. **Quick Start** - Build, run, test commands
5. **Configuration** - Environment variables and config files
6. **API Documentation** - Key endpoints or gRPC methods
7. **Development** - Local dev setup, debugging
8. **Testing** - Unit, integration, E2E tests
9. **Deployment** - Docker, Kubernetes deployment
10. **Monitoring** - Metrics, logs, traces
11. **Troubleshooting** - Common issues
12. **Contributing** - Development workflow

### Example: services/uccp/README.md (3 hours)

```markdown
# UCCP - Unified Compute & Coordination Platform

**UCCP** is the control plane for distributed computing, service coordination, and ML operations in the TW platform.

## Overview

UCCP provides:
- **Distributed Coordination:** Raft consensus for leader election and distributed state
- **Task Scheduling:** GPU/TPU-aware job scheduling with resource quotas
- **Service Discovery:** Central service registry with health checking
- **ML Operations:** TensorFlow/PyTorch training orchestration

## Architecture

```
┌─────────────────────────────────────────┐
│           Client Services               │
│     (NCCS, UDPS, Stream Compute)        │
└───────────────┬─────────────────────────┘
                │ gRPC/TLS
    ┌───────────▼───────────┐
    │   API Gateway         │
    │  (HTTP + gRPC)        │
    └───────────┬───────────┘
                │
    ┌───────────▼──────────────────┐
    │   Core Services              │
    ├──────────────────────────────┤
    │ • Raft Consensus (etcd)      │
    │ • Task Scheduler             │
    │ • Service Registry           │
    │ • ML Training Manager        │
    │ • Distributed Locks          │
    └──────────────────────────────┘
```

## Prerequisites

- Go 1.24+
- Docker 24.0+ (for dependencies)
- PostgreSQL 16+ (for metadata)
- etcd 3.5+ (for Raft consensus)

## Quick Start

### Build

```bash
cd services/uccp

# Download dependencies
go mod download

# Build
go build -o uccp ./cmd/uccp

# Run tests
go test ./...
```

### Run Locally

```bash
# Start dependencies
docker-compose -f docker-compose.uccp.yml up -d

# Run UCCP
./uccp --config config/development.yaml

# Verify health
curl http://localhost:8080/health
# Expected: {"status":"healthy","raft":{"leader":true}}
```

## Configuration

### Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `UCCP_PORT` | HTTP API port | 8080 |
| `UCCP_GRPC_PORT` | gRPC port | 50000 |
| `UCCP_RAFT_PORT` | Raft peer port | 50061 |
| `UCCP_DB_URL` | PostgreSQL connection | postgresql://localhost:5432/uccp |
| `UCCP_ETCD_ENDPOINTS` | etcd cluster endpoints | http://localhost:2379 |

### Config File

See `config/development.yaml` for full configuration options.

## API Documentation

### gRPC APIs

**Task Scheduling:**
```protobuf
service TaskService {
  rpc SubmitTask(SubmitTaskRequest) returns (TaskResponse);
  rpc GetTaskStatus(TaskIdRequest) returns (TaskStatusResponse);
  rpc CancelTask(TaskIdRequest) returns (CancelResponse);
}
```

**Service Discovery:**
```protobuf
service DiscoveryService {
  rpc RegisterService(RegisterRequest) returns (RegisterResponse);
  rpc Heartbeat(HeartbeatRequest) returns (HeartbeatResponse);
  rpc LookupService(LookupRequest) returns (ServiceInfo);
}
```

### HTTP APIs

- `GET /health` - Health check
- `GET /metrics` - Prometheus metrics
- `GET /api/v1/tasks` - List tasks
- `POST /api/v1/tasks` - Submit task

## Development

### Project Structure

```
uccp/
├── cmd/uccp/          # Main entry point
├── internal/
│   ├── api/          # HTTP/gRPC handlers
│   ├── scheduler/    # Task scheduling logic
│   ├── raft/         # Raft consensus
│   └── registry/     # Service registry
├── pkg/              # Public packages
├── config/           # Configuration files
└── tests/            # Tests
```

### Running Tests

```bash
# Unit tests
go test ./internal/...

# Integration tests
go test -tags=integration ./tests/integration/...

# With coverage
go test -coverprofile=coverage.out ./...
go tool cover -html=coverage.out
```

## Deployment

### Docker

```bash
# Build image
docker build -t uccp:latest .

# Run container
docker run -p 8080:8080 -p 50000:50000 uccp:latest
```

### Kubernetes

```bash
# Deploy to Kubernetes
kubectl apply -f deploy/kubernetes/uccp/

# Verify deployment
kubectl get pods -l app=uccp
```

## Monitoring

- **Metrics:** http://localhost:8080/metrics (Prometheus format)
- **Logs:** JSON structured logs to stdout
- **Tracing:** OpenTelemetry spans exported to Jaeger

## Troubleshooting

### Issue: Raft Cluster Won't Form

**Cause:** Peer addresses unreachable

**Solution:**
```bash
# Check peer connectivity
ping uccp-peer-1
ping uccp-peer-2

# Verify Raft ports open
telnet uccp-peer-1 50061
```

### Issue: Tasks Not Scheduling

**Cause:** No worker nodes registered

**Solution:**
```bash
# Check registered workers
curl http://localhost:8080/api/v1/workers

# Register worker manually
curl -X POST http://localhost:8080/api/v1/workers/register \
  -d '{"node_id":"worker-1","capacity":{"cpu":8,"memory":16}}'
```

## Contributing

See [CONTRIBUTING.md](../../CONTRIBUTING.md) for development workflow.
```

### Similar READMEs for Other Services

Create similar comprehensive READMEs for:
- **services/nccs/README.md** (3 hours) - .NET client service
- **services/udps/README.md** (3 hours) - Scala/Java data platform
- **services/stream-compute/README.md** (3 hours) - Rust stream processing

---

## 5. Testing

- [ ] All 4 service READMEs created
- [ ] Build instructions verified
- [ ] Quick start tested on clean machine
- [ ] API documentation complete
- [ ] Troubleshooting section includes common issues

---

## 6. Compliance Evidence

**SOC 2 CC1.4:** All system components documented

---

## 7. Sign-Off

- [ ] **UCCP Team:** UCCP README complete
- [ ] **NCCS Team:** NCCS README complete
- [ ] **UDPS Team:** UDPS README complete
- [ ] **Stream Team:** Stream Compute README complete

---

**Finding Status:** Not Started
**Last Updated:** 2025-12-27

---

**End of SEC-P2-004**
