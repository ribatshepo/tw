# TLS/HTTPS Security - Category Consolidation

**Category:** TLS/HTTPS Security
**Total Findings:** 8
**Total Effort:** 30 hours
**Implementation Phase:** Phase 1 (P0: Day 4) + Phase 2 (P1: Week 2, Days 8-14)

---

## Overview

This document consolidates all findings related to TLS/HTTPS security, certificate management, and encrypted communications.

## Findings Summary

| Finding ID | Title | Priority | Effort | Phase |
|-----------|-------|----------|--------|-------|
| SEC-P0-008 | TrustServerCertificate=true in Production | P0 - CRITICAL | 3h | Phase 1 |
| SEC-P1-001 | Metrics Endpoint Over HTTP | P1 - HIGH | 2h | Phase 2 |
| SEC-P1-002 | HSTS Middleware Missing | P1 - HIGH | 1h | Phase 2 |
| SEC-P1-003 | Elasticsearch Default Uses HTTP | P1 - HIGH | 4h | Phase 2 |
| SEC-P1-012 | Certificate Automation Missing | P1 - HIGH | 8h | Phase 2 |
| SEC-P2-009 | Shell Shebang Portability | P2 - MEDIUM | 0.5h | Phase 3 |
| SEC-P3-001 | CRL/OCSP Checking Missing | P3 - LOW | 4h | Phase 4 |
| SEC-P3-002 | Certificate Expiration Monitoring | P3 - LOW | 3h | Phase 4 |

**Total Critical Path Effort:** 18 hours (P0 + P1)

---

## Critical Path Analysis

### Production Blocker (P0) - Week 1, Day 4

**SEC-P0-008: TrustServerCertificate=true**
- **Impact:** Man-in-the-middle attacks possible on PostgreSQL connections
- **Fix:** Generate PostgreSQL TLS certificates, update connection strings
- **Effort:** 3 hours
- **Blocking:** Production deployment

### Pre-Production (P1) - Week 2, Days 8-14

**SEC-P1-001: Metrics Endpoint HTTP**
- **Impact:** Prometheus metrics exposed over unencrypted HTTP
- **Fix:** Configure HTTPS on port 9091 for metrics endpoint
- **Effort:** 2 hours

**SEC-P1-002: HSTS Middleware Missing**
- **Impact:** Browser doesn't enforce HTTPS connections
- **Fix:** Add `app.UseHsts()` middleware with 1-year max-age
- **Effort:** 1 hour

**SEC-P1-003: Elasticsearch Default HTTP**
- **Impact:** Log data transmitted in plaintext
- **Fix:** Enable xpack.security.http.ssl in Elasticsearch, update default URL
- **Effort:** 4 hours

**SEC-P1-012: Certificate Automation Missing**
- **Impact:** Manual certificate renewal, potential service outages on expiration
- **Fix:** Deploy cert-manager for Kubernetes, configure auto-renewal
- **Effort:** 8 hours

---

## Common Themes

### 1. Certificate Management Gaps

**Current State:**
- Manual certificate generation
- No expiration monitoring
- No automated rotation
- Self-signed certificates for development

**Required State:**
- Automated cert issuance via cert-manager
- Prometheus monitoring of expiration dates
- 30-day renewal window
- Production CA-signed certificates

### 2. Insecure Defaults

**Current State:**
- `TrustServerCertificate=true` (skips validation)
- HTTP-only metrics endpoint
- Elasticsearch defaults to HTTP
- No HSTS enforcement

**Required State:**
- Full certificate validation
- HTTPS everywhere
- HSTS with preload
- Secure defaults in configuration classes

### 3. Missing Certificate Validation

**Current State:**
- No CRL checking
- No OCSP validation
- Certificate chain validation disabled for PostgreSQL

**Required State:**
- Full chain validation
- CRL/OCSP checking (optional, P3)
- Certificate revocation monitoring

---

## Dependency Graph

```
SEC-P0-008 (Fix TrustServerCertificate)
    ↓
SEC-P1-001 (HTTPS Metrics) ──┐
SEC-P1-002 (HSTS)           ──┤
SEC-P1-003 (ES HTTPS)       ──┼──→ SEC-P1-012 (Cert Automation)
                              │         ↓
                              │    SEC-P3-001 (CRL/OCSP)
                              │         ↓
                              └───→ SEC-P3-002 (Cert Monitoring)
```

---

## Implementation Strategy

### Phase 1: Critical Security (Week 1, Day 4) - 3 hours

**SEC-P0-008: PostgreSQL TLS Configuration**

1. Generate PostgreSQL server certificates
```bash
# Generate PostgreSQL CA
openssl req -new -x509 -days 3650 -nodes \
  -out /var/lib/postgresql/ca.crt \
  -keyout /var/lib/postgresql/ca.key \
  -subj "/CN=PostgreSQL CA"

# Generate server certificate
openssl req -new -nodes \
  -out /var/lib/postgresql/server.csr \
  -keyout /var/lib/postgresql/server.key \
  -subj "/CN=postgres.local"

openssl x509 -req -in /var/lib/postgresql/server.csr \
  -CA /var/lib/postgresql/ca.crt \
  -CAkey /var/lib/postgresql/ca.key \
  -CAcreateserial \
  -out /var/lib/postgresql/server.crt \
  -days 3650
```

2. Update PostgreSQL configuration
```conf
# postgresql.conf
ssl = on
ssl_cert_file = '/var/lib/postgresql/server.crt'
ssl_key_file = '/var/lib/postgresql/server.key'
ssl_ca_file = '/var/lib/postgresql/ca.crt'
```

3. Update connection strings
```csharp
// REMOVE: TrustServerCertificate=true
// ADD: SSL Mode=Require;Root Certificate=/path/to/ca.crt
```

### Phase 2: HTTPS Enforcement (Week 2) - 15 hours

**SEC-P1-001: HTTPS Metrics (2h)**
```csharp
// Configure HTTPS for metrics endpoint
builder.WebHost.ConfigureKestrel(options =>
{
    options.Listen(IPAddress.Any, 9091, listenOptions =>
    {
        listenOptions.UseHttps(LoadMetricsCertificate());
    });
});
```

**SEC-P1-002: HSTS Middleware (1h)**
```csharp
builder.Services.AddHsts(options =>
{
    options.MaxAge = TimeSpan.FromDays(365);
    options.IncludeSubDomains = true;
    options.Preload = true;
});

app.UseHsts();
```

**SEC-P1-003: Elasticsearch HTTPS (4h)**
```yaml
# docker-compose.yml
elasticsearch:
  environment:
    - xpack.security.http.ssl.enabled=true
    - xpack.security.http.ssl.certificate=/certs/elasticsearch.crt
    - xpack.security.http.ssl.key=/certs/elasticsearch.key
```

```csharp
// ObservabilityOptions.cs
public string ElasticsearchUrl { get; set; } = "https://elasticsearch:9200";
```

**SEC-P1-012: Certificate Automation (8h)**
```bash
# Install cert-manager
kubectl apply -f https://github.com/cert-manager/cert-manager/releases/download/v1.13.0/cert-manager.yaml

# Create ClusterIssuer
kubectl apply -f config/cert-manager/cluster-issuer.yaml

# Create Certificate resources
kubectl apply -f deploy/kubernetes/usp/certificate.yaml
```

### Phase 3: Portability Fix (Week 3) - 0.5 hours

**SEC-P2-009: Shell Shebang**
```bash
# Update all .sh files
find . -name "*.sh" -exec sed -i '1s|^#!/bin/bash|#!/usr/bin/env bash|' {} \;
```

### Phase 4: Advanced Features (Week 4+) - 7 hours

**SEC-P3-001: CRL/OCSP (4h)**
```csharp
httpsOptions.CheckCertificateRevocation = true;
httpsOptions.OnAuthenticate = (context, sslOptions) =>
{
    sslOptions.CertificateRevocationCheckMode = X509RevocationMode.Online;
    sslOptions.RevocationMode = X509RevocationFlag.EntireChain;
};
```

**SEC-P3-002: Certificate Monitoring (3h)**
```yaml
# Deploy certificate exporter
kubectl apply -f deploy/kubernetes/monitoring/certificate-exporter.yaml

# Configure Prometheus alerts
alerting_rules:
  - alert: CertificateExpiringSoon
    expr: (x509_cert_not_after - time()) / 86400 < 30
```

---

## Testing Strategy

### Certificate Validation Tests
```bash
# Test PostgreSQL TLS connection
psql "sslmode=require sslrootcert=/path/to/ca.crt" -h postgres -U usp_user

# Test metrics endpoint HTTPS
curl https://localhost:9091/metrics --cacert certs/ca.crt

# Test HSTS header
curl -I https://localhost:5001/health | grep Strict-Transport-Security

# Test Elasticsearch HTTPS
curl -k -u elastic:password https://localhost:9200
```

### Security Scans
```bash
# SSL Labs scan (production)
ssllabs-scan --grade A usp.example.com

# testssl.sh scan
testssl.sh --full https://localhost:5001

# Certificate validation
openssl s_client -connect localhost:5001 -CAfile certs/ca.crt
```

---

## Compliance Mapping

| Finding | SOC 2 | HIPAA | PCI-DSS | GDPR |
|---------|-------|-------|---------|------|
| SEC-P0-008 | CC6.6 | 164.312(e)(1) | Req 4.1 | Art 32 |
| SEC-P1-001 | CC6.6 | 164.312(e)(1) | Req 4.1 | Art 32 |
| SEC-P1-002 | CC6.6 | 164.312(e)(1) | Req 4.1 | Art 32 |
| SEC-P1-003 | CC6.6 | 164.312(e)(1) | Req 4.1 | Art 32 |
| SEC-P1-012 | CC6.6 | - | - | - |
| SEC-P3-001 | CC6.6 | - | - | - |
| SEC-P3-002 | CC7.2 | - | - | - |

**Compliance Status:** ❌ **NON-COMPLIANT** until P0 + P1 findings resolved.

---

## Risk Assessment

| Risk | Pre-Implementation | Post-Implementation |
|------|-------------------|---------------------|
| MITM attacks on DB | 🔴 CRITICAL | 🟢 LOW |
| Metrics interception | 🟠 HIGH | 🟢 LOW |
| Log data exposure | 🟠 HIGH | 🟢 LOW |
| Certificate expiration | 🟡 MEDIUM | 🟢 LOW |
| Downgrade attacks | 🟠 HIGH | 🟢 LOW |

---

## Success Criteria

✅ **Complete when:**
- All database connections use TLS with certificate validation
- All HTTP endpoints migrated to HTTPS
- HSTS enforced on all services
- cert-manager operational with auto-renewal
- Prometheus monitoring certificate expiration
- No TLS/SSL scan findings rated below A-

---

**Status:** Not Started
**Last Updated:** 2025-12-27
**Category Owner:** Security + DevOps Teams
