# SEC-P1-004: Metrics Endpoint Mapping Broken

## 1. Metadata

| Field | Value |
|-------|-------|
| **Finding ID** | SEC-P1-004 |
| **Title** | Prometheus Metrics Endpoint Disabled Due to MapMetrics Issue |
| **Priority** | P1 - HIGH |
| **Severity** | High |
| **Category** | Monitoring/Observability |
| **Status** | Not Started |
| **Effort Estimate** | 2 hours |
| **Implementation Phase** | Phase 2 (Week 2, Day 10) |
| **Assigned To** | Backend Engineer |
| **Created** | 2025-12-27 |

---

## 2. Cross-References

| Type | Reference |
|------|-----------|
| **Audit Report** | `/home/tshepo/projects/tw/COMPREHENSIVE_AUDIT_REPORT.md:816-822` |
| **Code Files** | `/home/tshepo/projects/tw/services/usp/src/USP.API/Program.cs:230-232` |
| **Dependencies** | Blocked by SEC-P0-006 (TODO Comments) |
| **Blocks** | SEC-P1-005 (Metric Recording), SEC-P3-004 (Prometheus Alerts) |
| **Compliance Impact** | SOC 2 (CC7.2 - Monitoring) |

---

## 3. Executive Summary

### Problem

Metrics endpoint commented out in `Program.cs:230-232`:
```csharp
// TODO: Fix MapMetrics extension method issue
// app.MapMetrics("/metrics");
```

### Impact

- **No Metrics:** Prometheus cannot scrape metrics
- **No Monitoring:** No visibility into system performance
- **No Alerts:** Cannot detect issues (high error rate, resource exhaustion)

### Solution

Install `prometheus-net.AspNetCore` package and enable `app.MapMetrics("/metrics")`. **(Addressed in SEC-P0-006)**

---

## 4. Implementation Guide

### Step 1: Install Package (15 minutes)

```bash
cd /home/tshepo/projects/tw/services/usp/src/USP.API

dotnet add package prometheus-net.AspNetCore
```

### Step 2: Enable Metrics Endpoint (30 minutes)

```csharp
// Program.cs
using Prometheus;

var app = builder.Build();

// ✅ FIXED: Enable HTTP metrics tracking
app.UseHttpMetrics();

// ✅ FIXED: Expose Prometheus metrics endpoint
app.MapMetrics("/metrics");  // Remove TODO comment

app.Run();
```

### Step 3: Test Metrics (30 minutes)

```bash
curl https://localhost:5001/metrics -k

# Expected output:
# # HELP http_requests_received_total Total HTTP requests
# # TYPE http_requests_received_total counter
# http_requests_received_total{code="200",method="GET",controller="Health",action="Get"} 42
```

### Step 4: Configure Prometheus Scraping (30 minutes)

```yaml
# prometheus.yml
scrape_configs:
  - job_name: 'usp'
    scrape_interval: 15s
    static_configs:
      - targets: ['usp:5001']
```

---

## 5. Testing

- [ ] Metrics endpoint returns data at `/metrics`
- [ ] HTTP request counters incrementing
- [ ] Prometheus scrapes successfully
- [ ] Grafana displays metrics

---

## 6. Compliance Evidence

**SOC 2 CC7.2:** System monitoring operational

---

## 7. Sign-Off

- [ ] **Backend Engineer:** Metrics endpoint working
- [ ] **DevOps:** Prometheus scraping successfully

---

**Finding Status:** Not Started (Resolved by SEC-P0-006)
**Last Updated:** 2025-12-27

---

**End of SEC-P1-004**
