# Monitoring & Observability - Category Consolidation

**Category:** Monitoring & Observability
**Total Findings:** 7
**Total Effort:** 35 hours
**Implementation Phase:** Phase 2 (Week 2, Days 10-14)

---

## Overview

This document consolidates all findings related to monitoring, observability, metrics, tracing, logging, and alerting infrastructure.

## Findings Summary

| Finding ID | Title | Priority | Effort | Phase |
|-----------|-------|----------|--------|-------|
| SEC-P1-004 | Metrics Endpoint Mapping Broken | P1 - HIGH | 2h | Phase 2 |
| SEC-P1-005 | Metric Recording Inactive | P1 - HIGH | 4h | Phase 2 |
| SEC-P1-006 | Distributed Tracing Not Implemented | P1 - HIGH | 6h | Phase 2 |
| SEC-P1-007 | Observability Stack Missing | P1 - HIGH | 12h | Phase 2 |
| SEC-P3-004 | Prometheus Alerts Missing | P3 - LOW | 4h | Phase 4 |
| SEC-P3-005 | Alertmanager Not Configured | P3 - LOW | 3h | Phase 4 |
| SEC-P3-006 | SLO Tracking Not Implemented | P3 - LOW | 6h | Phase 4 |

**Total Critical Path Effort:** 24 hours (P1 only)

---

## Critical Path Analysis

### Pre-Production (P1) - Week 2

**Day 10: Metrics Foundation (6h)**
- SEC-P1-004: Fix MapMetrics (2h)
- SEC-P1-005: Activate metric recording (4h)

**Day 11-12: Observability Stack (12h)**
- SEC-P1-007: Deploy Prometheus, Grafana, Jaeger, Elasticsearch

**Day 13-14: Distributed Tracing (6h)**
- SEC-P1-006: Implement OpenTelemetry tracing

### Post-Production Enhancement (P3) - Week 4+

**Alerting & SLOs (13h)**
- SEC-P3-004: Configure Prometheus alert rules (4h)
- SEC-P3-005: Deploy Alertmanager (3h)
- SEC-P3-006: Implement SLO tracking (6h)

---

## Architecture Overview

```
┌─────────────────────────────────────────────┐
│           Application Services              │
│  (USP, UCCP, NCCS, UDPS, Stream)           │
└────┬────────┬──────────┬──────────┬─────────┘
     │        │          │          │
     │ Metrics│  Traces  │  Logs    │ Health
     │        │          │          │
     ▼        ▼          ▼          ▼
┌─────────┐ ┌────────┐ ┌──────────┐ ┌────────┐
│Prometheus│ │ Jaeger │ │Elasticsearch│ │ Health│
│  :9090  │ │ :16686 │ │  :9200   │ │ Checks│
└────┬────┘ └────┬───┘ └─────┬────┘ └────────┘
     │           │            │
     └───────────┼────────────┘
                 │
            ┌────▼────┐
            │ Grafana │
            │  :3000  │
            └─────────┘
                 │
            ┌────▼────────┐
            │Alertmanager │
            │   :9093     │
            └─────────────┘
```

---

## Common Themes

### 1. Missing Observability Infrastructure

**Current State:**
- Prometheus/Grafana/Jaeger commented out in docker-compose
- No observability stack deployed
- Dashboards created but not functional

**Required State:**
- Full observability stack operational
- Metrics collected from all services
- Traces captured for all requests
- Logs aggregated in Elasticsearch

### 2. Inactive Monitoring Code

**Current State:**
- SecurityMetrics class defined but methods never called
- Metrics endpoint broken (MapMetrics issue)
- OpenTelemetry configured but not initialized

**Required State:**
- All metrics actively recorded
- Metrics endpoint exposing data at /metrics
- OpenTelemetry fully operational

### 3. No Alerting

**Current State:**
- No Prometheus alert rules
- No Alertmanager deployment
- No incident notifications (Slack, PagerDuty)

**Required State:**
- Comprehensive alert rules (health, performance, security)
- Alertmanager routing alerts by severity
- Integration with notification channels

---

## Dependency Graph

```
SEC-P1-004 (Fix Metrics Endpoint)
    ↓
SEC-P1-005 (Activate Metric Recording)
    ↓
SEC-P1-007 (Deploy Observability Stack)
    ↓
SEC-P1-006 (Implement Tracing)
    ↓
SEC-P3-004 (Prometheus Alerts)
    ↓
SEC-P3-005 (Alertmanager)
    ↓
SEC-P3-006 (SLO Tracking)
```

**Sequential Implementation Required:** Each step depends on the previous.

---

## Implementation Strategy

### Phase 1: Metrics Foundation (Week 2, Day 10) - 6 hours

**SEC-P1-004: Fix Metrics Endpoint (2h)**

```bash
# Install package
dotnet add package prometheus-net.AspNetCore
```

```csharp
// Program.cs
using Prometheus;

app.UseHttpMetrics();
app.MapMetrics("/metrics");
```

**SEC-P1-005: Activate Metric Recording (4h)**

```csharp
// AuthController.cs
[HttpPost("login")]
public async Task<IActionResult> Login([FromBody] LoginRequest request)
{
    var result = await _authService.AuthenticateAsync(request.Username, request.Password);

    // ✅ Record metric
    _securityMetrics.RecordLoginAttempt(
        username: request.Username,
        success: result.Success,
        mfaUsed: result.MfaUsed
    );

    return result.Success ? Ok(result) : Unauthorized();
}
```

Apply to:
- AuthController: Login attempts, MFA usage
- AuthorizationService: Permission checks
- SecretsController: Secret access operations
- VaultController: Seal/unseal operations

### Phase 2: Observability Stack (Week 2, Day 11-12) - 12 hours

**SEC-P1-007: Deploy Stack (12h)**

```yaml
# docker-compose.yml

prometheus:
  image: prom/prometheus:v2.48.0
  ports:
    - "9090:9090"
  volumes:
    - ./config/prometheus:/etc/prometheus
    - prometheus_data:/prometheus

grafana:
  image: grafana/grafana:10.2.0
  ports:
    - "3000:3000"
  environment:
    - GF_SECURITY_ADMIN_PASSWORD=${GRAFANA_ADMIN_PASSWORD}
  volumes:
    - ./config/grafana:/etc/grafana/provisioning
    - grafana_data:/var/lib/grafana

jaeger:
  image: jaegertracing/all-in-one:1.51
  ports:
    - "16686:16686"  # UI
    - "6831:6831/udp"  # Agent

elasticsearch:
  image: docker.elastic.co/elasticsearch/elasticsearch:8.11.0
  ports:
    - "9200:9200"
  environment:
    - xpack.security.enabled=true
    - xpack.security.http.ssl.enabled=true
```

**Configure Prometheus scraping:**
```yaml
# config/prometheus/prometheus.yml
scrape_configs:
  - job_name: 'usp'
    static_configs:
      - targets: ['usp:9091']
    scheme: https
    tls_config:
      insecure_skip_verify: true
```

**Import Grafana dashboards:**
- usp-overview.json
- usp-authentication.json
- usp-security.json
- usp-secrets.json
- usp-pam.json

### Phase 3: Distributed Tracing (Week 2, Day 13-14) - 6 hours

**SEC-P1-006: OpenTelemetry (6h)**

```bash
# Install packages
dotnet add package OpenTelemetry.Extensions.Hosting
dotnet add package OpenTelemetry.Instrumentation.AspNetCore
dotnet add package OpenTelemetry.Exporter.Jaeger
```

```csharp
// Program.cs
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("USP", "1.0.0"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddEntityFrameworkCoreInstrumentation()
        .AddJaegerExporter(options =>
        {
            options.AgentHost = "jaeger";
            options.AgentPort = 6831;
        }));
```

**Create custom spans:**
```csharp
// VaultService.cs
private static readonly ActivitySource ActivitySource = new("USP.Vault");

public async Task UnsealAsync(string key)
{
    using var activity = ActivitySource.StartActivity("Vault.Unseal");
    activity?.SetTag("vault.key_number", 1);

    await _repository.SubmitUnsealKeyAsync(key);

    activity?.SetTag("vault.unsealed", _vault.IsUnsealed);
}
```

### Phase 4: Alerting & SLOs (Week 4+) - 13 hours

**SEC-P3-004: Prometheus Alerts (4h)**

```yaml
# config/prometheus/alerts/service-health.yaml
groups:
  - name: service_health
    rules:
      - alert: ServiceDown
        expr: up{job=~"usp|uccp|nccs"} == 0
        for: 1m
        labels:
          severity: critical
        annotations:
          summary: "Service {{ $labels.job }} is down"

      - alert: HighErrorRate
        expr: rate(http_requests_total{status=~"5.."}[5m]) > 0.05
        for: 5m
        labels:
          severity: warning
```

**SEC-P3-005: Alertmanager (3h)**

```yaml
# config/alertmanager/alertmanager.yml
route:
  receiver: 'default'
  routes:
    - match:
        severity: critical
      receiver: 'pagerduty-critical'
    - match:
        severity: warning
      receiver: 'slack-warnings'

receivers:
  - name: 'pagerduty-critical'
    pagerduty_configs:
      - service_key: '${PAGERDUTY_KEY}'

  - name: 'slack-warnings'
    slack_configs:
      - channel: '#alerts-warnings'
        api_url: '${SLACK_WEBHOOK}'
```

**SEC-P3-006: SLO Tracking (6h)**

```yaml
# config/slo/usp-slos.yaml
slos:
  - name: usp_availability
    objective: 99.9
    sli:
      query: |
        sum(rate(http_requests_total{job="usp"}[5m])) /
        sum(rate(http_requests_total{job="usp",status=~"5.."}[5m]))

  - name: usp_latency_p95
    objective: 95
    sli:
      threshold: 0.5
      query: |
        histogram_quantile(0.95,
          rate(http_request_duration_seconds_bucket{job="usp"}[5m])
        ) < 0.5
```

---

## Testing Strategy

### Metrics Testing
```bash
# Verify metrics endpoint
curl https://localhost:9091/metrics -k | grep -E "(login_attempts|secret_access)"

# Expected: Metrics incrementing on operations
```

### Tracing Testing
```bash
# Make API request
curl -X POST https://localhost:5001/api/v1/vault/seal/unseal -d '{"key":"..."}' -k

# View trace in Jaeger
open http://localhost:16686
# Expected: See full request trace with spans
```

### Alerting Testing
```bash
# Trigger test alert
curl -X POST http://alertmanager:9093/api/v1/alerts -d '[
  {"labels": {"alertname": "TestAlert", "severity": "warning"}}
]'

# Verify received in Slack
```

---

## Compliance Mapping

| Finding | SOC 2 | Compliance Requirement |
|---------|-------|------------------------|
| SEC-P1-004 | CC7.2 | System monitoring operational |
| SEC-P1-005 | CC7.2 | Security events monitored |
| SEC-P1-006 | CC7.2 | Distributed tracing for incidents |
| SEC-P1-007 | CC7.2 | Monitoring infrastructure deployed |
| SEC-P3-004 | CC7.2 | Automated alerting configured |
| SEC-P3-005 | CC7.2 | Incident notification operational |
| SEC-P3-006 | CC7.2 | Performance SLOs tracked |

---

## Success Criteria

✅ **Complete when:**
- Prometheus scraping metrics from all services
- Grafana dashboards displaying real-time data
- Jaeger capturing distributed traces
- Elasticsearch ingesting logs
- Alert rules firing correctly
- Alertmanager routing notifications
- SLOs tracked and error budgets calculated

---

**Status:** Not Started
**Last Updated:** 2025-12-27
**Category Owner:** SRE + DevOps Teams
