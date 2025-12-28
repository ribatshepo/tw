# SEC-P1-003: Elasticsearch Default Uses HTTP

## 1. Metadata

| Field | Value |
|-------|-------|
| **Finding ID** | SEC-P1-003 |
| **Title** | Elasticsearch Default Configuration Uses HTTP Instead of HTTPS |
| **Priority** | P1 - HIGH |
| **Severity** | Medium |
| **Category** | TLS/HTTPS Security / Configuration |
| **Status** | Not Started |
| **Effort Estimate** | 4 hours |
| **Implementation Phase** | Phase 2 (Week 2, Day 8-9) |
| **Assigned To** | DevOps Engineer |
| **Created** | 2025-12-27 |

---

## 2. Cross-References

| Type | Reference |
|------|-----------|
| **Audit Report** | `/home/tshepo/projects/tw/COMPREHENSIVE_AUDIT_REPORT.md:173-177` |
| **Code Files** | `/home/tshepo/projects/tw/services/usp/src/USP.API/ObservabilityOptions.cs:5` |
| **Dependencies** | None |
| **Blocks** | SEC-P1-007 (Observability Stack) |
| **Compliance Impact** | SOC 2 (CC7.2), HIPAA (164.312(e)(1)) |

---

## 3. Executive Summary

### Problem

Default Elasticsearch URL hardcoded as `"http://elasticsearch:9200"` in `ObservabilityOptions.cs:5`. Encourages insecure HTTP usage in code samples and documentation.

### Impact

- **Bad Defaults:** Developers copy-paste HTTP configuration
- **Log Exposure:** Audit logs and application logs sent over HTTP
- **Information Disclosure:** Sensitive data in logs can be intercepted

### Solution

Change default to `https://elasticsearch:9200` and configure Elasticsearch with TLS.

---

## 4. Implementation Guide

### Step 1: Update Default Configuration (15 minutes)

```csharp
// ObservabilityOptions.cs

public class ObservabilityOptions
{
    // ✅ CHANGE: HTTP to HTTPS
    public string ElasticsearchUrl { get; set; } = "https://elasticsearch:9200";
    public string JaegerAgentHost { get; set; } = "localhost";
    public int JaegerAgentPort { get; set; } = 6831;
}
```

### Step 2: Configure Elasticsearch with TLS (2 hours)

**Generate Elasticsearch Certificate:**

```bash
cd /home/tshepo/projects/tw/config/elasticsearch

# Generate certificate
docker run --rm -v $(pwd)/certs:/certs \
  docker.elastic.co/elasticsearch/elasticsearch:8.11.0 \
  elasticsearch-certutil cert \
  --name elasticsearch \
  --dns elasticsearch,localhost \
  --ip 127.0.0.1 \
  --out /certs/elasticsearch.p12

# Extract PEM files
openssl pkcs12 -in certs/elasticsearch.p12 -out certs/elasticsearch.crt -nokeys
openssl pkcs12 -in certs/elasticsearch.p12 -out certs/elasticsearch.key -nocerts -nodes
```

**Update docker-compose.yml:**

```yaml
elasticsearch:
  image: docker.elastic.co/elasticsearch/elasticsearch:8.11.0
  environment:
    - xpack.security.enabled=true
    - xpack.security.http.ssl.enabled=true
    - xpack.security.http.ssl.key=/usr/share/elasticsearch/config/certs/elasticsearch.key
    - xpack.security.http.ssl.certificate=/usr/share/elasticsearch/config/certs/elasticsearch.crt
  volumes:
    - ./config/elasticsearch/certs:/usr/share/elasticsearch/config/certs:ro
```

### Step 3: Update Application Configuration (1 hour)

```json
{
  "Observability": {
    "ElasticsearchUrl": "https://elasticsearch:9200",
    "ElasticsearchUsername": "elastic",
    "ElasticsearchPassword": "from_environment_or_vault"
  }
}
```

### Step 4: Test Elasticsearch HTTPS (30 minutes)

```bash
# Should fail (HTTP disabled)
curl http://localhost:9200
# Expected: Connection refused

# Should succeed (HTTPS)
curl -k -u elastic:password https://localhost:9200
# Expected: Elasticsearch cluster info JSON
```

---

## 5. Testing

- [ ] Default changed to HTTPS
- [ ] Elasticsearch accepts HTTPS connections
- [ ] Logs ingested successfully via HTTPS
- [ ] HTTP connections rejected

---

## 6. Compliance Evidence

**SOC 2 CC7.2:** Monitoring data (logs) protected in transit
**HIPAA 164.312(e)(1):** Audit logs encrypted in transit

---

## 7. Sign-Off

- [ ] **DevOps:** Elasticsearch HTTPS configured
- [ ] **Security:** Default changed to HTTPS

---

**Finding Status:** Not Started
**Last Updated:** 2025-12-27

---

**End of SEC-P1-003**
