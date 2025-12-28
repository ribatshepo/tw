# SEC-P3-005: Alertmanager Not Configured

## 1. Metadata

| Field | Value |
|-------|-------|
| **Finding ID** | SEC-P3-005 |
| **Title** | Alertmanager Not Configured for Alert Routing and Notifications |
| **Priority** | P3 - LOW |
| **Severity** | Low |
| **Category** | Monitoring/Observability |
| **Status** | Not Started |
| **Effort Estimate** | 3 hours |
| **Implementation Phase** | Phase 4 (Week 4+, Nice to Have) |
| **Assigned To** | SRE + DevOps Engineer |
| **Created** | 2025-12-27 |

---

## 2. Cross-References

| Type | Reference |
|------|-----------|
| **Audit Report** | `/home/tshepo/projects/tw/COMPREHENSIVE_AUDIT_REPORT.md:836-839` |
| **Code Files** | `config/alertmanager/` (missing) |
| **Dependencies** | SEC-P3-004 (Prometheus Alerts) |
| **Compliance Impact** | SOC 2 (CC7.2 - Incident Response) |

---

## 3. Executive Summary

### Problem

Prometheus alerts configured but Alertmanager not deployed/configured for routing alerts to Slack, PagerDuty, email, etc.

### Impact

- **Alerts Not Delivered:** Alerts fire but no one notified
- **No Incident Response:** SRE unaware of critical issues
- **Manual Alert Checking:** Must manually check Prometheus UI

### Solution

Deploy and configure Alertmanager with routing rules for email, Slack, and PagerDuty based on alert severity.

---

## 4. Implementation Guide

### Step 1: Deploy Alertmanager (1 hour)

```yaml
# deploy/kubernetes/monitoring/alertmanager.yaml

apiVersion: apps/v1
kind: Deployment
metadata:
  name: alertmanager
  namespace: tw-monitoring
spec:
  replicas: 1
  selector:
    matchLabels:
      app: alertmanager
  template:
    metadata:
      labels:
        app: alertmanager
    spec:
      containers:
        - name: alertmanager
          image: prom/alertmanager:v0.26.0
          args:
            - --config.file=/etc/alertmanager/alertmanager.yml
            - --storage.path=/alertmanager
          ports:
            - containerPort: 9093
              name: web
          volumeMounts:
            - name: config
              mountPath: /etc/alertmanager
            - name: storage
              mountPath: /alertmanager
      volumes:
        - name: config
          configMap:
            name: alertmanager-config
        - name: storage
          emptyDir: {}
---
apiVersion: v1
kind: Service
metadata:
  name: alertmanager
  namespace: tw-monitoring
spec:
  selector:
    app: alertmanager
  ports:
    - port: 9093
      targetPort: 9093
```

### Step 2: Configure Alert Routing (1.5 hours)

```yaml
# config/alertmanager/alertmanager.yml

global:
  resolve_timeout: 5m
  slack_api_url: 'https://hooks.slack.com/services/YOUR/SLACK/WEBHOOK'

# Define notification templates
templates:
  - '/etc/alertmanager/templates/*.tmpl'

# Alert routing tree
route:
  receiver: 'default'
  group_by: ['alertname', 'cluster', 'service']
  group_wait: 10s
  group_interval: 10s
  repeat_interval: 12h

  routes:
    # Critical alerts -> PagerDuty + Slack
    - match:
        severity: critical
      receiver: 'pagerduty-critical'
      continue: true

    - match:
        severity: critical
      receiver: 'slack-critical'

    # Warning alerts -> Slack only
    - match:
        severity: warning
      receiver: 'slack-warnings'

    # Security alerts -> Security team Slack + Email
    - match:
        category: security
      receiver: 'security-team'

# Inhibition rules (suppress alerts)
inhibit_rules:
  # Suppress warning if critical alert already firing
  - source_match:
      severity: 'critical'
    target_match:
      severity: 'warning'
    equal: ['alertname', 'instance']

# Notification receivers
receivers:
  - name: 'default'
    email_configs:
      - to: 'ops@example.com'
        from: 'alertmanager@example.com'
        smarthost: 'smtp.gmail.com:587'
        auth_username: 'alertmanager@example.com'
        auth_password: '${SMTP_PASSWORD}'

  - name: 'pagerduty-critical'
    pagerduty_configs:
      - service_key: '${PAGERDUTY_SERVICE_KEY}'
        description: '{{ .CommonAnnotations.summary }}'
        details:
          firing: '{{ .Alerts.Firing | len }}'
          resolved: '{{ .Alerts.Resolved | len }}'

  - name: 'slack-critical'
    slack_configs:
      - channel: '#alerts-critical'
        title: '🚨 CRITICAL: {{ .CommonAnnotations.summary }}'
        text: |
          {{ range .Alerts }}
          *Alert:* {{ .Annotations.summary }}
          *Description:* {{ .Annotations.description }}
          *Severity:* {{ .Labels.severity }}
          {{ end }}
        color: 'danger'

  - name: 'slack-warnings'
    slack_configs:
      - channel: '#alerts-warnings'
        title: '⚠️ Warning: {{ .CommonAnnotations.summary }}'
        text: |
          {{ range .Alerts }}
          *Alert:* {{ .Annotations.summary }}
          *Description:* {{ .Annotations.description }}
          {{ end }}
        color: 'warning'

  - name: 'security-team'
    slack_configs:
      - channel: '#security-alerts'
        title: '🔒 Security Alert: {{ .CommonAnnotations.summary }}'
    email_configs:
      - to: 'security@example.com'
```

### Step 3: Configure Prometheus to Use Alertmanager (30 minutes)

```yaml
# config/prometheus/prometheus.yml

alerting:
  alertmanagers:
    - static_configs:
        - targets:
            - alertmanager.tw-monitoring:9093
```

```bash
# Reload Prometheus
kubectl exec -n tw-monitoring prometheus-0 -- \
  curl -X POST http://localhost:9090/-/reload
```

### Step 4: Test Alerting (30 minutes)

```bash
# Trigger a test alert
curl -X POST http://alertmanager:9093/api/v1/alerts -d '[
  {
    "labels": {
      "alertname": "TestAlert",
      "severity": "warning"
    },
    "annotations": {
      "summary": "Test alert from manual trigger",
      "description": "This is a test alert to verify Alertmanager configuration"
    }
  }
]'

# Check alert received in Slack
# Expected: Message in #alerts-warnings channel

# Verify in Alertmanager UI
open http://alertmanager.example.com
```

---

## 5. Testing

- [ ] Alertmanager deployed
- [ ] Prometheus sending alerts to Alertmanager
- [ ] Critical alerts sent to PagerDuty
- [ ] Critical alerts sent to Slack #alerts-critical
- [ ] Warning alerts sent to Slack #alerts-warnings
- [ ] Security alerts sent to security team
- [ ] Email notifications working

---

## 6. Compliance Evidence

**SOC 2 CC7.2:** Automated incident notification system operational

---

## 7. Sign-Off

- [ ] **SRE:** Alertmanager configured and tested
- [ ] **DevOps:** All notification channels verified

---

**Finding Status:** Not Started
**Last Updated:** 2025-12-27

---

**End of SEC-P3-005**
