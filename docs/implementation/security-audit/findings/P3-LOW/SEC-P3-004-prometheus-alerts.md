# SEC-P3-004: Prometheus Alerts Missing

## 1. Metadata

| Field | Value |
|-------|-------|
| **Finding ID** | SEC-P3-004 |
| **Title** | Prometheus Alerting Rules Not Configured |
| **Priority** | P3 - LOW |
| **Severity** | Low |
| **Category** | Monitoring/Observability |
| **Status** | Not Started |
| **Effort Estimate** | 4 hours |
| **Implementation Phase** | Phase 4 (Week 4+, Nice to Have) |
| **Assigned To** | SRE + DevOps Engineer |
| **Created** | 2025-12-27 |

---

## 2. Cross-References

| Type | Reference |
|------|-----------|
| **Audit Report** | `/home/tshepo/projects/tw/COMPREHENSIVE_AUDIT_REPORT.md:836-839` |
| **Code Files** | `config/prometheus/alerts/` (missing) |
| **Dependencies** | SEC-P1-007 (Observability Stack) |
| **Related Findings** | SEC-P3-005 (Alertmanager), SEC-P3-006 (SLO Tracking) |
| **Compliance Impact** | SOC 2 (CC7.2 - Incident Detection) |

---

## 3. Executive Summary

### Problem

Prometheus deployed but no alerting rules configured. Cannot detect service failures, high error rates, or resource exhaustion.

### Impact

- **No Incident Detection:** Service outages not detected automatically
- **No Proactive Alerts:** Resource exhaustion surprises
- **Manual Monitoring Required:** SRE must manually check dashboards

### Solution

Create comprehensive alerting rules for service health, performance, errors, and resource usage.

---

## 4. Implementation Guide

### Step 1: Create Service Health Alerts (1 hour)

```yaml
# config/prometheus/alerts/service-health.yaml

groups:
  - name: service_health
    interval: 30s
    rules:
      # Service Down
      - alert: ServiceDown
        expr: up{job=~"usp|uccp|nccs|udps|stream"} == 0
        for: 1m
        labels:
          severity: critical
          category: availability
        annotations:
          summary: "Service {{ $labels.job }} is down"
          description: "{{ $labels.instance }} has been down for more than 1 minute"

      # High HTTP Error Rate
      - alert: HighHttpErrorRate
        expr: |
          rate(http_requests_total{status=~"5.."}[5m]) /
          rate(http_requests_total[5m]) > 0.05
        for: 5m
        labels:
          severity: warning
          category: errors
        annotations:
          summary: "High error rate on {{ $labels.job }}"
          description: "{{ $labels.instance }} has {{ $value | humanizePercentage }} error rate"

      # Service Unhealthy
      - alert: ServiceUnhealthy
        expr: health_check_status{endpoint="/health"} == 0
        for: 2m
        labels:
          severity: critical
          category: availability
        annotations:
          summary: "{{ $labels.job }} health check failing"
          description: "Health endpoint returning unhealthy status"
```

### Step 2: Create Performance Alerts (1 hour)

```yaml
# config/prometheus/alerts/performance.yaml

groups:
  - name: performance
    interval: 1m
    rules:
      # High Request Latency
      - alert: HighRequestLatency
        expr: |
          histogram_quantile(0.99,
            rate(http_request_duration_seconds_bucket[5m])
          ) > 1
        for: 5m
        labels:
          severity: warning
          category: performance
        annotations:
          summary: "High latency on {{ $labels.job }}"
          description: "P99 latency is {{ $value }}s (threshold: 1s)"

      # Database Query Slow
      - alert: SlowDatabaseQueries
        expr: |
          histogram_quantile(0.95,
            rate(db_query_duration_seconds_bucket[5m])
          ) > 0.5
        for: 5m
        labels:
          severity: warning
          category: performance
        annotations:
          summary: "Slow database queries on {{ $labels.job }}"
          description: "P95 query time is {{ $value }}s"
```

### Step 3: Create Resource Usage Alerts (1 hour)

```yaml
# config/prometheus/alerts/resources.yaml

groups:
  - name: resource_usage
    interval: 1m
    rules:
      # High CPU Usage
      - alert: HighCpuUsage
        expr: |
          100 - (avg by (instance) (rate(node_cpu_seconds_total{mode="idle"}[5m])) * 100) > 80
        for: 5m
        labels:
          severity: warning
          category: resources
        annotations:
          summary: "High CPU usage on {{ $labels.instance }}"
          description: "CPU usage is {{ $value | humanize }}%"

      # High Memory Usage
      - alert: HighMemoryUsage
        expr: |
          (1 - (node_memory_MemAvailable_bytes / node_memory_MemTotal_bytes)) * 100 > 85
        for: 5m
        labels:
          severity: warning
          category: resources
        annotations:
          summary: "High memory usage on {{ $labels.instance }}"
          description: "Memory usage is {{ $value | humanize }}%"

      # Disk Space Low
      - alert: DiskSpaceLow
        expr: |
          (1 - (node_filesystem_avail_bytes / node_filesystem_size_bytes)) * 100 > 85
        for: 5m
        labels:
          severity: warning
          category: resources
        annotations:
          summary: "Disk space low on {{ $labels.instance }}"
          description: "Disk usage is {{ $value | humanize }}% on {{ $labels.mountpoint }}"
```

### Step 4: Create Security Alerts (1 hour)

```yaml
# config/prometheus/alerts/security.yaml

groups:
  - name: security
    interval: 1m
    rules:
      # High Failed Login Rate
      - alert: HighFailedLoginRate
        expr: |
          rate(login_attempts_total{success="false"}[5m]) > 10
        for: 2m
        labels:
          severity: warning
          category: security
        annotations:
          summary: "High failed login rate detected"
          description: "{{ $value }} failed logins per second for user {{ $labels.username }}"

      # Account Lockout Spike
      - alert: AccountLockoutSpike
        expr: |
          rate(account_lockouts_total[5m]) > 5
        for: 2m
        labels:
          severity: warning
          category: security
        annotations:
          summary: "Account lockout spike detected"
          description: "{{ $value }} account lockouts per second - possible brute force attack"

      # Unauthorized Access Attempts
      - alert: UnauthorizedAccessAttempts
        expr: |
          rate(http_requests_total{status="401"}[5m]) > 20
        for: 2m
        labels:
          severity: warning
          category: security
        annotations:
          summary: "High rate of unauthorized access attempts"
          description: "{{ $value }} 401 responses per second on {{ $labels.endpoint }}"
```

### Step 5: Load Alert Rules in Prometheus (30 minutes)

```yaml
# config/prometheus/prometheus.yml

rule_files:
  - /etc/prometheus/alerts/service-health.yaml
  - /etc/prometheus/alerts/performance.yaml
  - /etc/prometheus/alerts/resources.yaml
  - /etc/prometheus/alerts/security.yaml
  - /etc/prometheus/alerts/certificate-expiration.yaml
```

```bash
# Reload Prometheus configuration
kubectl exec -n tw-monitoring prometheus-0 -- \
  curl -X POST http://localhost:9090/-/reload

# Verify alerts loaded
curl http://prometheus:9090/api/v1/rules | jq '.data.groups[].rules[] | .name'
```

---

## 5. Testing

- [ ] All alert rules loaded in Prometheus
- [ ] Alerts appear in Prometheus UI
- [ ] Test alerts fire correctly (simulate conditions)
- [ ] Alert labels and annotations correct
- [ ] Alerts integrate with Alertmanager (SEC-P3-005)

---

## 6. Compliance Evidence

**SOC 2 CC7.2:** Automated incident detection via alerting

---

## 7. Sign-Off

- [ ] **SRE:** All alert rules configured
- [ ] **DevOps:** Alerts tested and verified

---

**Finding Status:** Not Started
**Last Updated:** 2025-12-27

---

**End of SEC-P3-004**
