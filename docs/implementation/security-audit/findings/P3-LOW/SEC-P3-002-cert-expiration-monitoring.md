# SEC-P3-002: Certificate Expiration Monitoring

## 1. Metadata

| Field | Value |
|-------|-------|
| **Finding ID** | SEC-P3-002 |
| **Title** | Certificate Expiration Monitoring and Alerts Not Configured |
| **Priority** | P3 - LOW |
| **Severity** | Low |
| **Category** | TLS/HTTPS Security / Monitoring |
| **Status** | Not Started |
| **Effort Estimate** | 3 hours |
| **Implementation Phase** | Phase 4 (Week 4+, Nice to Have) |
| **Assigned To** | SRE + DevOps Engineer |
| **Created** | 2025-12-27 |

---

## 2. Cross-References

| Type | Reference |
|------|-----------|
| **Audit Report** | `/home/tshepo/projects/tw/COMPREHENSIVE_AUDIT_REPORT.md:190-192` |
| **Code Files** | Monitoring configuration |
| **Dependencies** | SEC-P1-007 (Observability Stack), SEC-P1-012 (Certificate Automation) |
| **Compliance Impact** | SOC 2 (CC7.2 - Monitoring) |

---

## 3. Executive Summary

### Problem

No proactive monitoring of certificate expiration dates. No alerts when certificates are about to expire.

### Impact

- **Service Outages:** Expired certificates cause TLS handshake failures
- **No Warning:** No advance notice before certificate expiration
- **Manual Monitoring:** Requires manual calendar tracking

### Solution

Deploy certificate exporter for Prometheus with alerts for certificates expiring in <30 days and <7 days.

**Note:** This is partially addressed in SEC-P1-012 (Certificate Automation). This finding focuses on the monitoring aspect specifically.

---

## 4. Implementation Guide

### Step 1: Deploy Certificate Exporter (1 hour)

```yaml
# deploy/kubernetes/monitoring/certificate-exporter.yaml

apiVersion: apps/v1
kind: Deployment
metadata:
  name: certificate-exporter
  namespace: tw-monitoring
spec:
  replicas: 1
  selector:
    matchLabels:
      app: certificate-exporter
  template:
    metadata:
      labels:
        app: certificate-exporter
    spec:
      containers:
        - name: exporter
          image: enix/x509-certificate-exporter:3.11.0
          args:
            - --watch-kubeconf
            - --watch-file=/certs
            - --trim-path-components=3
          ports:
            - containerPort: 9793
              name: metrics
          volumeMounts:
            - name: certs
              mountPath: /certs
              readOnly: true
      volumes:
        - name: certs
          secret:
            secretName: tls-certificates
---
apiVersion: v1
kind: Service
metadata:
  name: certificate-exporter
  namespace: tw-monitoring
spec:
  selector:
    app: certificate-exporter
  ports:
    - port: 9793
      targetPort: 9793
      name: metrics
```

### Step 2: Configure Prometheus Scraping (30 minutes)

```yaml
# config/prometheus/prometheus.yml

scrape_configs:
  - job_name: 'certificate-exporter'
    static_configs:
      - targets: ['certificate-exporter.tw-monitoring:9793']
    metric_relabel_configs:
      - source_labels: [__name__]
        regex: 'x509_cert_not_after'
        action: keep
```

### Step 3: Create Alert Rules (1 hour)

```yaml
# config/prometheus/alerts/certificate-expiration.yaml

groups:
  - name: certificate_expiration
    interval: 1h
    rules:
      # Warning: Certificate expires in < 30 days
      - alert: CertificateExpiringSoon
        expr: |
          (x509_cert_not_after - time()) / 86400 < 30
        for: 1h
        labels:
          severity: warning
          category: security
        annotations:
          summary: "Certificate {{ $labels.subject }} expiring soon"
          description: |
            Certificate will expire in {{ $value | humanizeDuration }}.
            Subject: {{ $labels.subject }}
            Issuer: {{ $labels.issuer }}
            Serial: {{ $labels.serial_number }}
          runbook_url: "https://wiki.example.com/runbooks/certificate-renewal"

      # Critical: Certificate expires in < 7 days
      - alert: CertificateExpiringCritical
        expr: |
          (x509_cert_not_after - time()) / 86400 < 7
        for: 1h
        labels:
          severity: critical
          category: security
          pagerduty: "true"
        annotations:
          summary: "⚠️ CRITICAL: Certificate {{ $labels.subject }} expiring in < 7 days"
          description: |
            Certificate will expire in {{ $value | humanizeDuration }}.
            IMMEDIATE ACTION REQUIRED.
            Subject: {{ $labels.subject }}
            Issuer: {{ $labels.issuer }}

      # Error: Certificate already expired
      - alert: CertificateExpired
        expr: |
          x509_cert_not_after < time()
        for: 5m
        labels:
          severity: critical
          category: security
          pagerduty: "true"
        annotations:
          summary: "🚨 Certificate {{ $labels.subject }} EXPIRED"
          description: |
            Certificate has expired.
            Service may be unavailable.
            Subject: {{ $labels.subject }}
```

### Step 4: Create Grafana Dashboard (30 minutes)

```json
// config/grafana/dashboards/certificate-expiration.json

{
  "dashboard": {
    "title": "Certificate Expiration Monitoring",
    "panels": [
      {
        "title": "Days Until Expiration",
        "type": "graph",
        "targets": [
          {
            "expr": "(x509_cert_not_after - time()) / 86400",
            "legendFormat": "{{ subject }}"
          }
        ],
        "thresholds": [
          { "value": 7, "color": "red" },
          { "value": 30, "color": "orange" },
          { "value": 90, "color": "green" }
        ]
      },
      {
        "title": "Certificates Expiring Soon",
        "type": "stat",
        "targets": [
          {
            "expr": "count((x509_cert_not_after - time()) / 86400 < 30)"
          }
        ]
      }
    ]
  }
}
```

---

## 5. Testing

- [ ] Certificate exporter deployed
- [ ] Prometheus scraping certificate metrics
- [ ] Alerts fire 30 days before expiration
- [ ] Critical alerts fire 7 days before expiration
- [ ] Grafana dashboard shows expiration dates
- [ ] PagerDuty integration working (if configured)

---

## 6. Compliance Evidence

**SOC 2 CC7.2:** Proactive monitoring of certificate expiration

---

## 7. Sign-Off

- [ ] **SRE:** Certificate monitoring configured
- [ ] **DevOps:** Alerts verified

---

**Finding Status:** Not Started
**Last Updated:** 2025-12-27

---

**End of SEC-P3-002**
