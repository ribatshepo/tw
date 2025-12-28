# SEC-P2-003: Stub READMEs Empty

## 1. Metadata

| Field | Value |
|-------|-------|
| **Finding ID** | SEC-P2-003 |
| **Title** | Six README Files Contain Only Headings, No Content |
| **Priority** | P2 - MEDIUM |
| **Severity** | Medium |
| **Category** | Documentation |
| **Status** | Not Started |
| **Effort Estimate** | 8 hours |
| **Implementation Phase** | Phase 3 (Week 3, Day 2-3) |
| **Assigned To** | Technical Writer + Engineering Team |
| **Created** | 2025-12-27 |

---

## 2. Cross-References

| Type | Reference |
|------|-----------|
| **Audit Report** | `/home/tshepo/projects/tw/COMPREHENSIVE_AUDIT_REPORT.md:487-493` |
| **Code Files** | `proto/README.md`, `config/README.md`, `deploy/README.md`, `tests/integration/README.md`, `tests/e2e/README.md`, `tests/load/README.md` |
| **Dependencies** | None |
| **Compliance Impact** | SOC 2 (CC1.4 - Documentation) |

---

## 3. Executive Summary

### Problem

Six README files exist with only headings and no content:
- proto/README.md
- config/README.md
- deploy/README.md
- tests/integration/README.md
- tests/e2e/README.md
- tests/load/README.md

### Impact

- **Incomplete Documentation:** Key directories not explained
- **Poor Navigation:** Developers don't understand directory purpose
- **Missing Guidance:** No instructions for using proto files, configs, tests

### Solution

Fill all stub READMEs with comprehensive content explaining purpose, structure, usage, and examples.

---

## 4. Implementation Guide

### File 1: proto/README.md (1.5 hours)

```markdown
# Protocol Buffer Definitions

This directory contains Protocol Buffer (protobuf) definitions for gRPC communication between TW platform services.

## Overview

All inter-service communication uses gRPC with mTLS for security. Protocol Buffers provide the API contract definitions.

## Directory Structure

```
proto/
├── common/           # Shared message types
│   ├── types.proto  # Common data types (UUID, Timestamp, etc.)
│   └── errors.proto # Standard error responses
├── uccp/            # UCCP service definitions
│   ├── task.proto   # Task scheduling APIs
│   └── ml.proto     # ML operations APIs
├── usp/             # USP service definitions
│   ├── auth.proto   # Authentication APIs
│   ├── secrets.proto # Secrets management APIs
│   └── vault.proto  # Vault seal/unseal APIs
├── udps/            # UDPS service definitions
│   └── query.proto  # SQL query execution APIs
└── stream/          # Stream Compute definitions
    └── job.proto    # Stream job management APIs
```

## Generating Code

### For Go (UCCP)

```bash
cd proto/uccp
protoc --go_out=. --go-grpc_out=. *.proto
```

### For C# (USP, NCCS)

```bash
cd proto/usp
protoc --csharp_out=. --grpc_out=. --plugin=protoc-gen-grpc=grpc_csharp_plugin *.proto
```

### For Scala/Java (UDPS)

```bash
cd proto/udps
protoc --java_out=. --grpc-java_out=. *.proto
```

## Using Generated Code

### Client Example (C#)

```csharp
using Grpc.Net.Client;
using USP.Protos;

var channel = GrpcChannel.ForAddress("https://usp:5001");
var client = new AuthService.AuthServiceClient(channel);

var response = await client.LoginAsync(new LoginRequest
{
    Username = "admin",
    Password = "password"
});
```

## Versioning

All proto files use semantic versioning:
- Breaking changes: Increment major version
- New fields: Increment minor version
- Bug fixes: Increment patch version

## Best Practices

1. **Never Remove Fields:** Mark deprecated instead
2. **Use Reserved Numbers:** Prevent field number reuse
3. **Document All Fields:** Add comments for clarity
4. **Validate Input:** Always validate in service implementation
```

### File 2: config/README.md (1.5 hours)

```markdown
# Configuration Files

This directory contains configuration files for all TW platform services and infrastructure.

## Directory Structure

```
config/
├── prometheus/              # Prometheus monitoring config
│   ├── prometheus.yml      # Scrape configurations
│   └── alerts/             # Alert rules
│       ├── certificate-expiration.yaml
│       ├── security-metrics.yaml
│       └── service-health.yaml
├── grafana/                # Grafana dashboards
│   ├── dashboards/         # Dashboard JSON files
│   │   ├── usp-overview.json
│   │   ├── usp-security.json
│   │   └── usp-secrets.json
│   └── datasources/        # Datasource configs
│       └── prometheus.yaml
├── elasticsearch/          # Elasticsearch configuration
│   └── certs/             # TLS certificates
├── postgres/              # PostgreSQL configuration
│   └── postgresql.conf    # Database tuning
└── kubernetes/            # Kubernetes configs
    ├── namespaces/
    ├── configmaps/
    └── secrets/
```

## Prometheus Configuration

### Adding Scrape Target

Edit `prometheus/prometheus.yml`:

```yaml
scrape_configs:
  - job_name: 'my-service'
    static_configs:
      - targets: ['my-service:9090']
    scheme: https
    tls_config:
      insecure_skip_verify: false
      ca_file: /etc/prometheus/certs/ca.crt
```

### Creating Alert Rules

Create `prometheus/alerts/my-alerts.yaml`:

```yaml
groups:
  - name: my_service_alerts
    interval: 1m
    rules:
      - alert: HighErrorRate
        expr: rate(http_requests_total{status=~"5.."}[5m]) > 0.05
        for: 5m
        labels:
          severity: critical
        annotations:
          summary: "High error rate on {{ $labels.instance }}"
```

## Grafana Dashboards

Dashboards are provisioned automatically from `grafana/dashboards/`. To add a new dashboard:

1. Create dashboard in Grafana UI
2. Export as JSON
3. Save to `grafana/dashboards/my-dashboard.json`
4. Restart Grafana

## Environment-Specific Configs

- **Development:** Use `.env` files in service directories
- **Production:** Use Kubernetes ConfigMaps and Secrets
- **Staging:** Use `config/staging/` overrides

## Secrets Management

**Never commit secrets to this directory.** Use:
- USP Vault for application secrets
- Kubernetes Secrets for infrastructure credentials
- Environment variables for local development
```

### Files 3-6: Test READMEs (2 hours each)

*Due to space constraints, I'll provide a template structure. Each file follows similar pattern but with test-type-specific content.*

**tests/integration/README.md:**
- Integration test overview
- Running integration tests
- Writing new integration tests
- Test data management
- CI/CD integration

**tests/e2e/README.md:**
- End-to-end test scenarios
- Running E2E tests
- Test environment setup
- Browser automation (if applicable)
- Performance benchmarks

**tests/load/README.md:**
- Load testing overview
- k6/Gatling/JMeter setup
- Running load tests
- Performance baselines
- Analyzing results
