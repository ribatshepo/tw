# SEC-P1-012: Certificate Automation Missing

## 1. Metadata

| Field | Value |
|-------|-------|
| **Finding ID** | SEC-P1-012 |
| **Title** | Automatic Certificate Rotation and Expiration Monitoring Not Implemented |
| **Priority** | P1 - HIGH |
| **Severity** | High |
| **Category** | TLS/HTTPS Security / Infrastructure |
| **Status** | Not Started |
| **Effort Estimate** | 8 hours |
| **Implementation Phase** | Phase 2 (Week 2, Day 13-14) |
| **Assigned To** | DevOps Engineer + Security Engineer |
| **Created** | 2025-12-27 |

---

## 2. Cross-References

| Type | Reference |
|------|-----------|
| **Audit Report** | `/home/tshepo/projects/tw/COMPREHENSIVE_AUDIT_REPORT.md:186-192` |
| **Code Files** | None (infrastructure/operational improvement) |
| **Dependencies** | None |
| **Related Findings** | SEC-P3-001 (CRL/OCSP), SEC-P3-002 (Certificate Expiration Monitoring) |
| **Compliance Impact** | SOC 2 (CC6.6), HIPAA (164.312(e)(1)) |

---

## 3. Executive Summary

### Problem

Certificate management is manual. No automated certificate rotation, no expiration monitoring, no alerts when certificates are about to expire.

### Impact

- **Service Outages:** Certificates expire, causing TLS handshake failures
- **Manual Intervention Required:** DevOps must manually renew certificates
- **No Proactive Alerts:** Cannot detect expiring certificates before outage

### Solution

Implement cert-manager for Kubernetes-based automatic certificate rotation, plus Prometheus monitoring for certificate expiration dates.

---

## 4. Implementation Guide

### Step 1: Install cert-manager (2 hours)

```bash
# Install cert-manager for automatic certificate management
kubectl apply -f https://github.com/cert-manager/cert-manager/releases/download/v1.13.0/cert-manager.yaml

# Verify cert-manager is running
kubectl get pods -n cert-manager
# Expected: cert-manager, cert-manager-cainjector, cert-manager-webhook all Running
```

**Create ClusterIssuer for internal CA:**

```yaml
# config/cert-manager/cluster-issuer.yaml

apiVersion: cert-manager.io/v1
kind: ClusterIssuer
metadata:
  name: internal-ca-issuer
spec:
  ca:
    secretName: internal-ca-key-pair  # Your internal CA certificate
```

### Step 2: Configure Automatic Certificate Issuance (2 hours)

```yaml
# deploy/kubernetes/usp/certificate.yaml

apiVersion: cert-manager.io/v1
kind: Certificate
metadata:
  name: usp-tls-cert
  namespace: usp
spec:
  secretName: usp-tls-secret
  issuerRef:
    name: internal-ca-issuer
    kind: ClusterIssuer
  dnsNames:
    - usp.local
    - usp.svc.cluster.local
    - localhost
  duration: 8760h  # 1 year
  renewBefore: 720h  # Renew 30 days before expiration
  privateKey:
    algorithm: RSA
    size: 4096
```

```yaml
# Similar certificates for all services
# - uccp-tls-cert
# - nccs-tls-cert
# - udps-tls-cert
# - stream-tls-cert
# - postgres-tls-cert
# - elasticsearch-tls-cert
```

### Step 3: Configure Certificate Expiration Monitoring (2 hours)

```yaml
# config/prometheus/alerts/certificate-expiration.yaml

groups:
  - name: certificate_expiration
    interval: 1h
    rules:
      # Alert when certificate expires in < 30 days
      - alert: CertificateExpiringSoon
        expr: |
          (x509_cert_not_after - time()) / 86400 < 30
        for: 1h
        labels:
          severity: warning
        annotations:
          summary: "Certificate {{ $labels.subject }} expiring soon"
          description: "Certificate expires in {{ $value | humanizeDuration }}"

      # Alert when certificate expires in < 7 days
      - alert: CertificateExpiringCritical
        expr: |
          (x509_cert_not_after - time()) / 86400 < 7
        for: 1h
        labels:
          severity: critical
        annotations:
          summary: "Certificate {{ $labels.subject }} expiring CRITICAL"
          description: "Certificate expires in {{ $value | humanizeDuration }}"
```

**Create certificate exporter for Prometheus:**

```yaml
# deploy/kubernetes/monitoring/certificate-exporter.yaml

apiVersion: apps/v1
kind: Deployment
metadata:
  name: certificate-exporter
  namespace: monitoring
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
        - name: certificate-exporter
          image: enix/x509-certificate-exporter:latest
          args:
            - --watch-kubeconf
            - --trim-path-components=3
          ports:
            - containerPort: 9793
              name: metrics
```

### Step 4: Configure Grafana Dashboard (1 hour)

```json
// config/grafana/dashboards/certificate-expiration.json

{
  "dashboard": {
    "title": "Certificate Expiration",
    "panels": [
      {
        "title": "Days Until Certificate Expiration",
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
        "title": "Certificates Expiring < 30 Days",
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

### Step 5: Test Automatic Rotation (1 hour)

```bash
# Create test certificate with short lifetime
kubectl apply -f - <<EOF
apiVersion: cert-manager.io/v1
kind: Certificate
metadata:
  name: test-short-cert
  namespace: default
spec:
  secretName: test-short-cert-secret
  issuerRef:
    name: internal-ca-issuer
    kind: ClusterIssuer
  dnsNames:
    - test.local
  duration: 2h  # Very short for testing
  renewBefore: 1h  # Renew after 1 hour
EOF

# Wait 1 hour and verify renewal
kubectl get certificate test-short-cert -o yaml
# Expected: "Renewed" event in status.conditions

# Check Prometheus for expiration metrics
curl http://prometheus:9090/api/v1/query?query='x509_cert_not_after{subject=~".*test.*"}'
# Expected: Expiration timestamp updated after renewal
```

---

## 5. Testing

- [ ] cert-manager installed and running
- [ ] Certificates issued automatically
- [ ] Certificates renew 30 days before expiration
- [ ] Prometheus scrapes certificate expiration metrics
- [ ] Grafana dashboard shows days until expiration
- [ ] Alerts fire when certificates expire soon

---

## 6. Compliance Evidence

**SOC 2 CC6.6:** Automated certificate lifecycle management
**HIPAA 164.312(e)(1):** TLS certificates managed with automated rotation

---

## 7. Sign-Off

- [ ] **DevOps:** cert-manager operational
- [ ] **Security:** Certificate rotation verified
- [ ] **SRE:** Monitoring and alerts configured

---

**Finding Status:** Not Started
**Last Updated:** 2025-12-27

---

**End of SEC-P1-012**
