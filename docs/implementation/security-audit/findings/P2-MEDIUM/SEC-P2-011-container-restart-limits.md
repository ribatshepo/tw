# SEC-P2-011: Container Restart Limits Missing

## 1. Metadata

| Field | Value |
|-------|-------|
| **Finding ID** | SEC-P2-011 |
| **Title** | Docker Compose Has No Container Restart Limits (Infinite Retry) |
| **Priority** | P2 - MEDIUM |
| **Severity** | Low |
| **Category** | Configuration / Infrastructure |
| **Status** | Not Started |
| **Effort Estimate** | 30 minutes |
| **Implementation Phase** | Phase 3 (Week 3, Day 7) |
| **Assigned To** | DevOps Engineer |
| **Created** | 2025-12-27 |

---

## 2. Cross-References

| Type | Reference |
|------|-----------|
| **Audit Report** | `/home/tshepo/projects/tw/COMPREHENSIVE_AUDIT_REPORT.md:363-364` |
| **Code Files** | `docker-compose.infra.yml` |
| **Dependencies** | None |
| **Compliance Impact** | SOC 2 (CC7.2 - Monitoring) |

---

## 3. Executive Summary

### Problem

Docker Compose services configured with `restart: always` but no restart attempt limits. Failed containers retry infinitely.

### Impact

- **Resource Exhaustion:** Failing containers consume CPU/memory indefinitely
- **Log Spam:** Crash loops fill disk with error logs
- **No Alerting:** Infinite retries mask underlying issues

### Solution

Configure restart policies with `on-failure` and `max-attempts` limit, plus add health checks.

---

## 4. Implementation Guide

### Step 1: Update Restart Policies (20 minutes)

```yaml
# docker-compose.infra.yml

services:
  postgres:
    image: postgres:16-alpine
    # ✅ CHANGE: restart: always
    # TO:
    restart: on-failure:5  # Restart up to 5 times on failure
    deploy:
      restart_policy:
        condition: on-failure
        max_attempts: 5
        window: 120s  # 2 minutes
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U postgres"]
      interval: 10s
      timeout: 5s
      retries: 5

  redis:
    image: redis:7-alpine
    restart: on-failure:5
    deploy:
      restart_policy:
        condition: on-failure
        max_attempts: 5
        window: 120s
    healthcheck:
      test: ["CMD", "redis-cli", "ping"]
      interval: 10s
      timeout: 5s
      retries: 5

  kafka:
    image: bitnami/kafka:3.6
    restart: on-failure:5
    deploy:
      restart_policy:
        condition: on-failure
        max_attempts: 5
        window: 120s
    healthcheck:
      test: ["CMD-SHELL", "kafka-broker-api-versions.sh --bootstrap-server localhost:9092"]
      interval: 30s
      timeout: 10s
      retries: 5

  elasticsearch:
    image: docker.elastic.co/elasticsearch/elasticsearch:8.11.0
    restart: on-failure:5
    deploy:
      restart_policy:
        condition: on-failure
        max_attempts: 5
        window: 120s
    healthcheck:
      test: ["CMD-SHELL", "curl -k -u elastic:$ELASTICSEARCH_PASSWORD https://localhost:9200/_cluster/health || exit 1"]
      interval: 30s
      timeout: 10s
      retries: 5
```

### Step 2: Add Monitoring for Restart Events (10 minutes)

```yaml
# config/prometheus/alerts/container-restarts.yaml

groups:
  - name: container_restarts
    interval: 1m
    rules:
      - alert: ContainerRestarting
        expr: rate(container_restarts_total[5m]) > 0
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: "Container {{ $labels.container }} restarting"
          description: "Container has restarted {{ $value }} times in last 5 minutes"

      - alert: ContainerRestartLimitReached
        expr: container_restart_count >= 5
        for: 1m
        labels:
          severity: critical
        annotations:
          summary: "Container {{ $labels.container }} reached restart limit"
          description: "Container has failed and stopped after 5 restart attempts"
```

---

## 5. Testing

- [ ] All services use `restart: on-failure:5`
- [ ] Services stop after 5 failed restart attempts
- [ ] Health checks working for all services
- [ ] Prometheus alerts fire on restart events
- [ ] Logs show restart limit enforcement

---

## 6. Compliance Evidence

**SOC 2 CC7.2:** System monitoring includes container restart tracking

---

## 7. Sign-Off

- [ ] **DevOps:** Restart policies configured
- [ ] **SRE:** Monitoring alerts verified

---

**Finding Status:** Not Started
**Last Updated:** 2025-12-27

---

**End of SEC-P2-011**
