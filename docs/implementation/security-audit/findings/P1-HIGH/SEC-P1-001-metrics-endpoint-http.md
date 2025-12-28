# SEC-P1-001: Metrics Endpoint Exposed Over HTTP

## 1. Metadata

| Field | Value |
|-------|-------|
| **Finding ID** | SEC-P1-001 |
| **Title** | Metrics Endpoint Exposed Over HTTP Instead of HTTPS |
| **Priority** | P1 - HIGH |
| **Severity** | High |
| **Category** | TLS/HTTPS Security / Monitoring |
| **Status** | Not Started |
| **Effort Estimate** | 4 hours |
| **Implementation Phase** | Phase 2 (Week 2, Day 6-7) |
| **Assigned To** | DevOps Engineer |
| **Created** | 2025-12-27 |

---

## 2. Cross-References

| Type | Reference |
|------|-----------|
| **Audit Report** | `/home/tshepo/projects/tw/COMPREHENSIVE_AUDIT_REPORT.md:153-158` |
| **Code Files** | `/home/tshepo/projects/tw/services/usp/src/USP.API/appsettings.json:105`, `appsettings.Development.json:49` |
| **Dependencies** | Blocked by SEC-P0-008 (TrustServerCertificate) |
| **Blocks** | SEC-P3-001 (CRL/OCSP), SEC-P3-002 (Certificate Monitoring) |
| **Compliance Impact** | SOC 2 (CC7.2 - Monitoring), PCI-DSS (Req 10.2) |

---

## 3. Executive Summary

### Problem

Metrics endpoint configured as `"Metrics": {"Url": "http://+:9090"}` exposes operational metrics (CPU, memory, request rates, error counts) over unencrypted HTTP.

### Impact

- **Information Disclosure:** Attackers can see system performance, error rates, authentication failures
- **Reconnaissance:** Metrics reveal attack surface (endpoints, dependencies, vulnerabilities)
- **DoS Targeting:** Attackers know resource limits and can optimize DoS attacks

### Solution

Change to `https://+:9091` and configure TLS certificate for metrics endpoint.

---

## 4. Implementation Guide

### Step 1: Update Configuration (1 hour)

```json
{
  "Metrics": {
    "Url": "https://+:9091"  // ✅ HTTPS instead of HTTP
  }
}
```

### Step 2: Configure Kestrel Certificate (1 hour)

```csharp
// Program.cs
builder.WebHost.ConfigureKestrel(options =>
{
    options.Listen(IPAddress.Any, 9091, listenOptions =>
    {
        listenOptions.UseHttps(httpsOptions =>
        {
            httpsOptions.ServerCertificate = LoadMetricsCertificate(configuration);
        });
    });
});
```

### Step 3: Update Prometheus Scrape Config (30 minutes)

```yaml
scrape_configs:
  - job_name: 'usp'
    scheme: https  # ✅ HTTPS
    tls_config:
      ca_file: /etc/prometheus/certs/ca.crt
      cert_file: /etc/prometheus/certs/client.crt
      key_file: /etc/prometheus/certs/client.key
    static_configs:
      - targets: ['usp:9091']
```

### Step 4: Test (30 minutes)

```bash
# Should fail (no HTTPS)
curl http://localhost:9090/metrics
# Expected: Connection refused

# Should succeed (HTTPS)
curl https://localhost:9091/metrics -k
# Expected: Prometheus metrics
```

---

## 5. Testing

- [ ] Metrics unavailable over HTTP
- [ ] Metrics available over HTTPS
- [ ] Prometheus scrapes successfully
- [ ] Certificate validation working

---

## 6. Compliance Evidence

**SOC 2 CC7.2:** Monitoring data protected in transit
**PCI-DSS Req 10.2:** Audit logs (metrics) encrypted

---

## 7. Sign-Off

- [ ] **DevOps:** HTTPS metrics working
- [ ] **Security:** Certificate validation enforced

---

**Finding Status:** Not Started
**Last Updated:** 2025-12-27

---

**End of SEC-P1-001**
