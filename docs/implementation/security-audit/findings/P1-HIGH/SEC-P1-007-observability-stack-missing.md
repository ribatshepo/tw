# SEC-P1-007: Observability Stack Missing

## 1. Metadata

| Field | Value |
|-------|-------|
| **Finding ID** | SEC-P1-007 |
| **Title** | Prometheus, Grafana, Jaeger, Elasticsearch Not Deployed |
| **Priority** | P1 - HIGH |
| **Severity** | High |
| **Category** | Monitoring/Observability / Infrastructure |
| **Status** | Not Started |
| **Effort Estimate** | 12 hours |
| **Implementation Phase** | Phase 2 (Week 2, Day 10-12) |
| **Assigned To** | DevOps Engineer |
| **Created** | 2025-12-27 |

---

## 2. Cross-References

| Type | Reference |
|------|-----------|
| **Audit Report** | `/home/tshepo/projects/tw/COMPREHENSIVE_AUDIT_REPORT.md:909-921` |
| **Code Files** | `docker-compose.yml` (services commented out) |
| **Dependencies** | Blocked by SEC-P1-001 (HTTPS Metrics), SEC-P1-003 (Elasticsearch HTTPS) |
| **Blocks** | SEC-P1-004, SEC-P1-005, SEC-P1-006, SEC-P3-004, SEC-P3-005 |
| **Compliance Impact** | SOC 2 (CC7.2 - Monitoring) |

---

## 3. Executive Summary

### Problem

Observability stack (Prometheus, Grafana, Jaeger, Elasticsearch) commented out in `docker-compose.yml`. No monitoring infrastructure deployed.

### Impact

- **No Monitoring:** Cannot see system health, performance, errors
- **No Alerting:** Cannot detect incidents (service down, high error rate)
- **No Debugging:** Cannot investigate performance issues or trace requests

### Solution

Deploy full observability stack: Prometheus (metrics), Grafana (dashboards), Jaeger (tracing), Elasticsearch (logs).

---

## 4. Implementation Guide

### Step 1: Deploy Prometheus (3 hours)

```yaml
# docker-compose.yml
prometheus:
  image: prom/prometheus:v2.48.0
  ports:
    - "9090:9090"
  volumes:
    - ./config/prometheus/prometheus.yml:/etc/prometheus/prometheus.yml:ro
    - ./config/prometheus/alerts:/etc/prometheus/alerts:ro
    - prometheus_data:/prometheus
  command:
    - '--config.file=/etc/prometheus/prometheus.yml'
    - '--storage.tsdb.path=/prometheus'
    - '--storage.tsdb.retention.time=30d'
```

**Create `config/prometheus/prometheus.yml`:**

```yaml
global:
  scrape_interval: 15s
  evaluation_interval: 15s

scrape_configs:
  - job_name: 'usp'
    static_configs:
      - targets: ['usp:9091']
    scheme: https
    tls_config:
      insecure_skip_verify: true  # For dev; use proper certs in prod

  - job_name: 'uccp'
    static_configs:
      - targets: ['uccp:9100']

  # Add other services...
```

### Step 2: Deploy Grafana (2 hours)

```yaml
grafana:
  image: grafana/grafana:10.2.0
  ports:
    - "3000:3000"
  environment:
    - GF_SECURITY_ADMIN_PASSWORD=${GRAFANA_ADMIN_PASSWORD}
    - GF_INSTALL_PLUGINS=grafana-piechart-panel
  volumes:
    - ./config/grafana/dashboards:/etc/grafana/provisioning/dashboards:ro
    - ./config/grafana/datasources:/etc/grafana/provisioning/datasources:ro
    - grafana_data:/var/lib/grafana
```

### Step 3: Deploy Jaeger (2 hours)

```yaml
jaeger:
  image: jaegertracing/all-in-one:1.51
  ports:
    - "5775:5775/udp"  # Zipkin
    - "6831:6831/udp"  # Jaeger agent
    - "6832:6832/udp"  # Jaeger agent
    - "5778:5778"      # Jaeger UI
    - "16686:16686"    # Jaeger UI
    - "14268:14268"    # Jaeger collector
  environment:
    - COLLECTOR_ZIPKIN_HOST_PORT=:9411
```

### Step 4: Deploy Elasticsearch (3 hours)

```yaml
elasticsearch:
  image: docker.elastic.co/elasticsearch/elasticsearch:8.11.0
  ports:
    - "9200:9200"
  environment:
    - discovery.type=single-node
    - xpack.security.enabled=true
    - xpack.security.http.ssl.enabled=true
    - ELASTIC_PASSWORD=${ELASTICSEARCH_PASSWORD}
  volumes:
    - elasticsearch_data:/usr/share/elasticsearch/data
    - ./config/elasticsearch/certs:/usr/share/elasticsearch/config/certs:ro
```

### Step 5: Start Observability Stack (1 hour)

```bash
docker-compose up -d prometheus grafana jaeger elasticsearch

# Wait for services
sleep 60

# Verify Prometheus
curl http://localhost:9090/-/healthy
# Expected: Prometheus is Healthy.

# Verify Grafana
curl http://localhost:3000/api/health
# Expected: {"database":"ok","version":"..."}

# Verify Jaeger
curl http://localhost:16686/
# Expected: Jaeger UI HTML

# Verify Elasticsearch
curl -k -u elastic:$ELASTICSEARCH_PASSWORD https://localhost:9200
# Expected: Elasticsearch cluster info
```

### Step 6: Configure Dashboards (1 hour)

Grafana dashboards already created in `config/grafana/dashboards/`:
- `usp-overview.json`
- `usp-authentication.json`
- `usp-security.json`
- `usp-secrets.json`
- `usp-pam.json`

Access Grafana at http://localhost:3000 (admin / $GRAFANA_ADMIN_PASSWORD)

---

## 5. Testing

- [ ] Prometheus scraping metrics from all services
- [ ] Grafana dashboards displaying data
- [ ] Jaeger receiving traces
- [ ] Elasticsearch ingesting logs
- [ ] All services healthy

---

## 6. Compliance Evidence

**SOC 2 CC7.2:** Monitoring infrastructure operational

---

## 7. Sign-Off

- [ ] **DevOps:** Observability stack deployed
- [ ] **Engineering Lead:** All services monitored

---

**Finding Status:** Not Started
**Last Updated:** 2025-12-27

---

**End of SEC-P1-007**
