# SEC-P2-006: DEPLOYMENT Guide Missing

## 1. Metadata

| Field | Value |
|-------|-------|
| **Finding ID** | SEC-P2-006 |
| **Title** | No DEPLOYMENT.md Guide for Production Deployment |
| **Priority** | P2 - MEDIUM |
| **Severity** | Medium |
| **Category** | Documentation |
| **Status** | Not Started |
| **Effort Estimate** | 8 hours |
| **Implementation Phase** | Phase 3 (Week 3, Day 6) |
| **Assigned To** | DevOps Engineer + SRE |
| **Created** | 2025-12-27 |

---

## 2. Cross-References

| Type | Reference |
|------|-----------|
| **Audit Report** | `/home/tshepo/projects/tw/COMPREHENSIVE_AUDIT_REPORT.md:477-486` |
| **Code Files** | None (file missing) |
| **Dependencies** | None |
| **Compliance Impact** | SOC 2 (CC8.1 - Change Management) |

---

## 3. Executive Summary

### Problem

No DEPLOYMENT.md guide exists. Kubernetes/Docker deployment procedures undefined.

### Impact

- **No Production Deployment Path:** Team doesn't know how to deploy
- **Inconsistent Deployments:** No standardized deployment procedure
- **Operational Risk:** Missing rollback procedures, health checks

### Solution

Create comprehensive DEPLOYMENT.md covering Docker, Kubernetes, CI/CD, monitoring, and rollback procedures.

---

## 4. Implementation Guide

### Step 1: Create docs/DEPLOYMENT.md (7 hours)

```markdown
# TW Platform Deployment Guide

This guide covers deploying the TW platform to production using Kubernetes.

## Prerequisites

- Kubernetes 1.28+ cluster
- kubectl configured
- Helm 3.12+
- Container registry access (Docker Hub, ECR, GCR, etc.)
- TLS certificates for production domains

## Architecture Overview

```
┌──────────────────────────────────────┐
│      Load Balancer (Ingress)        │
│         TLS Termination              │
└─────────────┬────────────────────────┘
              │
    ┌─────────┴──────────┐
    │                    │
┌───▼────┐         ┌─────▼──────┐
│  UCCP  │         │    USP     │
│ (Go)   │◄────────┤  (.NET)    │
└───┬────┘         └─────┬──────┘
    │                    │
    │ ┌──────────┐       │
    └─► Database │◄──────┘
      │ (Postgres)│
      └──────────┘
```

## Step 1: Prepare Infrastructure

### 1.1 Create Kubernetes Namespace

```bash
kubectl create namespace tw-platform
kubectl create namespace tw-infrastructure
kubectl create namespace tw-monitoring
```

### 1.2 Deploy Infrastructure Services

```bash
# PostgreSQL
helm install postgres bitnami/postgresql \
  --namespace tw-infrastructure \
  --set auth.password=<from-vault> \
  --set primary.persistence.size=100Gi

# Redis
helm install redis bitnami/redis \
  --namespace tw-infrastructure \
  --set auth.password=<from-vault>

# Kafka
helm install kafka bitnami/kafka \
  --namespace tw-infrastructure \
  --set replicaCount=3
```

## Step 2: Configure Secrets

### 2.1 Create Kubernetes Secrets

```bash
# Database credentials
kubectl create secret generic db-credentials \
  --namespace tw-platform \
  --from-literal=postgres-password=<from-vault> \
  --from-literal=redis-password=<from-vault>

# TLS certificates
kubectl create secret tls usp-tls \
  --namespace tw-platform \
  --cert=certs/usp.crt \
  --key=certs/usp.key

kubectl create secret tls uccp-tls \
  --namespace tw-platform \
  --cert=certs/uccp.crt \
  --key=certs/uccp.key
```

## Step 3: Deploy Services

### 3.1 Deploy USP (Security Platform)

```bash
cd deploy/helm/usp

# Update values.yaml with production config
vim values-production.yaml

# Deploy
helm install usp . \
  --namespace tw-platform \
  --values values-production.yaml \
  --set image.tag=v1.0.0

# Verify deployment
kubectl get pods -n tw-platform -l app=usp
kubectl logs -n tw-platform -l app=usp --tail=100
```

### 3.2 Deploy UCCP (Compute Platform)

```bash
cd deploy/helm/uccp

helm install uccp . \
  --namespace tw-platform \
  --values values-production.yaml \
  --set image.tag=v1.0.0
```

### 3.3 Deploy Other Services

```bash
# NCCS
helm install nccs deploy/helm/nccs \
  --namespace tw-platform \
  --values deploy/helm/nccs/values-production.yaml

# UDPS
helm install udps deploy/helm/udps \
  --namespace tw-platform \
  --values deploy/helm/udps/values-production.yaml

# Stream Compute
helm install stream deploy/helm/stream-compute \
  --namespace tw-platform \
  --values deploy/helm/stream-compute/values-production.yaml
```

## Step 4: Configure Ingress

```yaml
# deploy/kubernetes/ingress.yaml

apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: tw-platform-ingress
  namespace: tw-platform
  annotations:
    cert-manager.io/cluster-issuer: "letsencrypt-prod"
    nginx.ingress.kubernetes.io/ssl-redirect: "true"
spec:
  ingressClassName: nginx
  tls:
    - hosts:
        - usp.example.com
        - uccp.example.com
      secretName: tw-platform-tls
  rules:
    - host: usp.example.com
      http:
        paths:
          - path: /
            pathType: Prefix
            backend:
              service:
                name: usp
                port:
                  number: 5001
    - host: uccp.example.com
      http:
        paths:
          - path: /
            pathType: Prefix
            backend:
              service:
                name: uccp
                port:
                  number: 8080
```

```bash
kubectl apply -f deploy/kubernetes/ingress.yaml
```

## Step 5: Initialize USP Vault

```bash
# Port-forward to USP
kubectl port-forward -n tw-platform svc/usp 5001:5001

# Initialize vault and save unseal keys
curl -k -X POST https://localhost:5001/api/v1/vault/init \
  | tee vault-init-response.json

# Store unseal keys in secure location (AWS Secrets Manager, etc.)
# NEVER commit unseal keys to git

# Unseal vault with 3 keys
for key in $(jq -r '.unsealKeys[]' vault-init-response.json | head -3); do
  curl -k -X POST https://localhost:5001/api/v1/vault/seal/unseal \
    -H "Content-Type: application/json" \
    -d "{\"key\":\"$key\"}"
done
```

## Step 6: Deploy Monitoring

```bash
# Prometheus
helm install prometheus prometheus-community/kube-prometheus-stack \
  --namespace tw-monitoring \
  --values config/prometheus/values-production.yaml

# Grafana dashboards
kubectl apply -f config/grafana/dashboards/ -n tw-monitoring

# Jaeger
helm install jaeger jaegertracing/jaeger \
  --namespace tw-monitoring
```

## Step 7: Verify Deployment

```bash
# Check all pods running
kubectl get pods -n tw-platform
# Expected: All pods in Running state

# Check service health
kubectl exec -n tw-platform deploy/usp -- curl -k https://localhost:5001/health

# Check ingress
curl https://usp.example.com/health
# Expected: {"status":"Healthy"}

# Check metrics
curl https://usp.example.com/metrics
# Expected: Prometheus metrics
```

## Rolling Updates

### Update Service Version

```bash
# Update USP to v1.1.0
helm upgrade usp deploy/helm/usp \
  --namespace tw-platform \
  --set image.tag=v1.1.0 \
  --reuse-values

# Watch rollout
kubectl rollout status -n tw-platform deployment/usp

# Verify new version
kubectl get pods -n tw-platform -l app=usp -o jsonpath='{.items[0].spec.containers[0].image}'
```

## Rollback Procedures

### Rollback Helm Release

```bash
# View release history
helm history usp -n tw-platform

# Rollback to previous version
helm rollback usp -n tw-platform

# Rollback to specific revision
helm rollback usp 3 -n tw-platform
```

### Rollback Kubernetes Deployment

```bash
# Rollback deployment
kubectl rollout undo deployment/usp -n tw-platform

# Rollback to specific revision
kubectl rollout undo deployment/usp --to-revision=2 -n tw-platform
```

## Backup & Disaster Recovery

### Database Backups

```bash
# Daily PostgreSQL backup
kubectl exec -n tw-infrastructure postgres-0 -- \
  pg_dump -U postgres -d usp_prod | \
  gzip > backups/usp-$(date +%Y%m%d).sql.gz

# Upload to S3
aws s3 cp backups/usp-$(date +%Y%m%d).sql.gz \
  s3://tw-platform-backups/postgresql/
```

### Vault Backup

```bash
# Backup vault data
kubectl exec -n tw-platform deploy/usp -- \
  curl -k -H "X-Vault-Token: $ROOT_TOKEN" \
  https://localhost:5001/api/v1/vault/backup > vault-backup.json

# Encrypt backup
gpg --encrypt --recipient ops@example.com vault-backup.json
```

## Monitoring & Alerts

- **Grafana Dashboards:** https://grafana.example.com
- **Prometheus Alerts:** https://alertmanager.example.com
- **Jaeger Tracing:** https://jaeger.example.com

## Troubleshooting

See [TROUBLESHOOTING.md](TROUBLESHOOTING.md) for common deployment issues.

## CI/CD Integration

See `.github/workflows/deploy-production.yml` for automated deployment pipeline.
```

---

## 5. Testing

- [ ] DEPLOYMENT.md created
- [ ] All deployment steps tested on staging
- [ ] Rollback procedures verified
- [ ] Backup/restore tested
- [ ] Monitoring configured

---

## 6. Compliance Evidence

**SOC 2 CC8.1:** Documented change management procedures

---

## 7. Sign-Off

- [ ] **DevOps:** Deployment guide verified
- [ ] **SRE:** Operational procedures complete

---

**Finding Status:** Not Started
**Last Updated:** 2025-12-27

---

**End of SEC-P2-006**
