# Phase 6: Production Readiness - Weeks 15-16 Implementation Guide

**Phase:** 6 of 6
**Duration:** Weeks 15-16 (2 weeks)
**Focus:** Production Deployment & Go-Live
**Team:** Full Team (Engineering + DevOps + SRE + Security + Leadership)
**Deliverable:** Live production system with 24/7 monitoring

---

## Overview

Phase 6 is the final phase where the fully tested system is deployed to production, monitored, and stabilized. This includes production environment setup, data migration, cutover planning, go-live execution, and post-launch monitoring.

**Dependencies:** Phases 1-5 must be complete (all security, implementation, and testing done).

**Critical Success Factors:**
- Zero-downtime deployment
- Rollback plan validated
- 24/7 monitoring operational
- Incident response team ready
- Executive sign-off obtained

---

## Week 15: Production Deployment Preparation

### Day 1: Production Environment Setup (1 day)

#### Objective
Provision production Kubernetes cluster and infrastructure.

---

### Task 1: Provision Production Kubernetes Cluster

```bash
# Create production GKE cluster (Google Cloud example)
gcloud container clusters create tw-platform-prod \
  --region us-central1 \
  --num-nodes 10 \
  --machine-type n2-standard-8 \
  --enable-autoscaling --min-nodes 10 --max-nodes 50 \
  --enable-autorepair --enable-autoupgrade \
  --network tw-vpc-prod \
  --subnetwork tw-subnet-prod \
  --enable-ip-alias \
  --enable-network-policy \
  --enable-stackdriver-kubernetes \
  --addons HorizontalPodAutoscaling,HttpLoadBalancing,GcePersistentDiskCsiDriver \
  --workload-pool=tw-platform.svc.id.goog \
  --labels environment=production,team=platform

# Get credentials
gcloud container clusters get-credentials tw-platform-prod --region us-central1

# Verify cluster
kubectl get nodes
# Expected: 10 nodes in Ready state
```

---

### Task 2: Deploy Infrastructure Services

```bash
# Create namespaces
kubectl create namespace tw-platform
kubectl create namespace monitoring
kubectl create namespace cert-manager
kubectl create namespace ingress-nginx

# Label namespaces
kubectl label namespace tw-platform environment=production
kubectl label namespace monitoring environment=production

# Deploy PostgreSQL (managed service recommended for production)
# Using Google Cloud SQL as example
gcloud sql instances create tw-postgres-prod \
  --database-version=POSTGRES_16 \
  --tier=db-custom-8-32768 \
  --region=us-central1 \
  --availability-type=REGIONAL \
  --enable-bin-log \
  --backup-start-time=03:00 \
  --maintenance-window-day=SUN --maintenance-window-hour=04 \
  --storage-type=SSD --storage-size=500GB --storage-auto-increase

# Deploy Redis (managed)
gcloud redis instances create tw-redis-prod \
  --size=10 \
  --region=us-central1 \
  --tier=standard \
  --redis-version=redis_7_0

# Deploy Kafka (Confluent Cloud or self-managed)
helm install kafka bitnami/kafka \
  --namespace tw-platform \
  --set replicaCount=5 \
  --set persistence.size=500Gi \
  --set metrics.kafka.enabled=true \
  --set metrics.jmx.enabled=true

# Deploy observability stack (from Phase 2)
helm install prometheus prometheus-community/kube-prometheus-stack \
  --namespace monitoring \
  --values config/prometheus-production-values.yaml
```

---

### Day 2: Security Hardening (1 day)

#### Task 1: Production TLS Certificates

```bash
# Install cert-manager (if not already)
helm install cert-manager jetstack/cert-manager \
  --namespace cert-manager \
  --set installCRDs=true

# Create Let's Encrypt production issuer
cat <<EOF | kubectl apply -f -
apiVersion: cert-manager.io/v1
kind: ClusterIssuer
metadata:
  name: letsencrypt-prod
spec:
  acme:
    server: https://acme-v02.api.letsencrypt.org/directory
    email: devops@tw.com
    privateKeySecretRef:
      name: letsencrypt-prod-key
    solvers:
      - http01:
          ingress:
            class: nginx
      - dns01:
          cloudDNS:
            project: tw-platform-prod
            serviceAccountSecretRef:
              name: clouddns-service-account
              key: key.json
EOF

# Create production certificates for all services
for service in usp nccs uccp udps stream; do
  cat <<EOF | kubectl apply -f -
apiVersion: cert-manager.io/v1
kind: Certificate
metadata:
  name: ${service}-tls
  namespace: tw-platform
spec:
  secretName: ${service}-tls
  issuerRef:
    name: letsencrypt-prod
    kind: ClusterIssuer
  dnsNames:
    - ${service}.tw.com
    - ${service}-api.tw.com
  duration: 2160h  # 90 days
  renewBefore: 720h  # Renew 30 days before expiry
EOF
done

# Verify certificates issued
kubectl get certificates -n tw-platform
# Expected: All Ready=True
```

---

#### Task 2: Network Policies & RBAC

```yaml
# NetworkPolicy for USP (example)
apiVersion: networking.k8s.io/v1
kind: NetworkPolicy
metadata:
  name: usp-network-policy
  namespace: tw-platform
spec:
  podSelector:
    matchLabels:
      app: usp
  policyTypes:
    - Ingress
    - Egress
  ingress:
    # Allow from NCCS
    - from:
        - podSelector:
            matchLabels:
              app: nccs
      ports:
        - protocol: TCP
          port: 5001
    # Allow from Ingress
    - from:
        - namespaceSelector:
            matchLabels:
              name: ingress-nginx
      ports:
        - protocol: TCP
          port: 5001
  egress:
    # Allow to PostgreSQL
    - to:
        - podSelector:
            matchLabels:
              app: postgresql
      ports:
        - protocol: TCP
          port: 5432
    # Allow to Redis
    - to:
        - podSelector:
            matchLabels:
              app: redis
      ports:
        - protocol: TCP
          port: 6379
    # Allow DNS
    - to:
        - namespaceSelector:
            matchLabels:
              name: kube-system
        - podSelector:
            matchLabels:
              k8s-app: kube-dns
      ports:
        - protocol: UDP
          port: 53
```

---

### Day 3-4: Application Deployment (2 days)

#### Task 1: Deploy Services with Helm

```bash
# Add TW Helm repository
helm repo add tw https://charts.tw.com
helm repo update

# Deploy USP
helm install usp tw/usp \
  --namespace tw-platform \
  --values config/helm/usp-production-values.yaml \
  --set image.tag=1.0.0 \
  --set replicaCount=5 \
  --set resources.requests.cpu=2 \
  --set resources.requests.memory=4Gi \
  --set autoscaling.enabled=true \
  --set autoscaling.minReplicas=5 \
  --set autoscaling.maxReplicas=20

# Deploy NCCS
helm install nccs tw/nccs \
  --namespace tw-platform \
  --values config/helm/nccs-production-values.yaml \
  --set image.tag=1.0.0 \
  --set replicaCount=5

# Deploy UCCP
helm install uccp tw/uccp \
  --namespace tw-platform \
  --values config/helm/uccp-production-values.yaml \
  --set image.tag=1.0.0 \
  --set replicaCount=3  # Raft requires odd number

# Deploy UDPS
helm install udps tw/udps \
  --namespace tw-platform \
  --values config/helm/udps-production-values.yaml \
  --set image.tag=1.0.0 \
  --set replicaCount=5

# Deploy Stream Compute
helm install stream tw/stream-compute \
  --namespace tw-platform \
  --values config/helm/stream-production-values.yaml \
  --set image.tag=1.0.0 \
  --set replicaCount=5

# Verify all deployments
kubectl get deployments -n tw-platform
# Expected: All 5 services with READY replicas
```

---

#### Task 2: Unseal USP Vault in Production

```bash
# Port-forward to USP
kubectl port-forward -n tw-platform svc/usp 5001:5001 &

# Initialize Vault (first time only)
curl -k -X POST https://localhost:5001/api/v1/vault/init

# Response contains 5 unseal keys + root token
# {
#   "keys": ["key1", "key2", "key3", "key4", "key5"],
#   "rootToken": "s.xxxxxxxxxxxxxxxx"
# }

# Store keys in secure location (e.g., Google Secret Manager)
gcloud secrets create vault-unseal-key-1 --data-file=- <<< "$KEY1"
gcloud secrets create vault-unseal-key-2 --data-file=- <<< "$KEY2"
gcloud secrets create vault-unseal-key-3 --data-file=- <<< "$KEY3"
gcloud secrets create vault-unseal-key-4 --data-file=- <<< "$KEY4"
gcloud secrets create vault-unseal-key-5 --data-file=- <<< "$KEY5"
gcloud secrets create vault-root-token --data-file=- <<< "$ROOT_TOKEN"

# Unseal vault with 3 of 5 keys
for i in 1 2 3; do
  KEY=$(gcloud secrets versions access latest --secret="vault-unseal-key-$i")
  curl -k -X POST https://localhost:5001/api/v1/vault/unseal \
    -H "Content-Type: application/json" \
    -d "{\"key\":\"$KEY\"}"
done

# Verify unsealed
curl -k https://localhost:5001/api/v1/vault/status
# Expected: {"sealed":false,"unsealProgress":0}
```

---

### Day 5: Data Migration (1 day)

#### Task 1: Migrate Existing Data to Production

```bash
# Export data from staging
pg_dump -h staging-postgres -U postgres -d usp_staging > usp_staging_dump.sql
pg_dump -h staging-postgres -U postgres -d uccp_staging > uccp_staging_dump.sql

# Sanitize data (remove test users, PII if needed)
sed -i '/test_user/d' usp_staging_dump.sql

# Import to production
psql -h production-postgres -U postgres -d usp_prod < usp_staging_dump.sql
psql -h production-postgres -U postgres -d uccp_prod < uccp_staging_dump.sql

# Verify data migrated
psql -h production-postgres -U postgres -d usp_prod -c "SELECT COUNT(*) FROM users;"
# Expected: Count matches staging

# Run data validation script
bash scripts/validate-production-data.sh
# Expected: All validation checks pass
```

---

## Week 16: Production Cutover & Stabilization

### Day 1: Pre-Launch Checklist (1 day)

#### Go/No-Go Checklist

**Infrastructure:**
- [ ] Kubernetes cluster operational with 10+ nodes
- [ ] All 5 services deployed and healthy
- [ ] PostgreSQL replicated (primary + standby)
- [ ] Redis cluster operational
- [ ] Kafka cluster operational (5 brokers)
- [ ] Load balancers configured
- [ ] DNS records pointing to production

**Security:**
- [ ] All TLS certificates issued (Let's Encrypt production)
- [ ] Vault unsealed and operational
- [ ] All secrets stored in Vault (no hardcoded secrets)
- [ ] Network policies enforced
- [ ] RBAC configured
- [ ] mTLS enabled on all inter-service communication
- [ ] Security scan passed (Trivy, OWASP ZAP)

**Observability:**
- [ ] Prometheus scraping all services
- [ ] Grafana dashboards operational
- [ ] Jaeger receiving distributed traces
- [ ] Elasticsearch indexing logs
- [ ] Alertmanager configured with Slack/PagerDuty
- [ ] SLO dashboards showing baselines

**Testing:**
- [ ] All unit tests passing (1,247/1,247)
- [ ] Integration tests passing (58/58)
- [ ] E2E tests passing (12/12)
- [ ] Load tests passed (all performance SLAs met)
- [ ] Security tests passed
- [ ] Chaos engineering validated resilience

**Data:**
- [ ] Production database initialized
- [ ] Data migration completed
- [ ] Data validation passed
- [ ] Backup/restore tested

**Documentation:**
- [ ] Runbooks created for all services
- [ ] Incident response procedures documented
- [ ] On-call rotation established
- [ ] Rollback procedures documented

**Sign-Offs:**
- [ ] Engineering Lead approval
- [ ] Security Team approval
- [ ] DevOps/SRE approval
- [ ] QA approval
- [ ] Executive sponsor approval

---

### Day 2: Production Cutover (1 day)

#### Cutover Plan

**Timeline:** Saturday 2:00 AM UTC (low-traffic window)

**Team Roles:**
- **Cutover Lead:** DevOps Lead
- **Engineering Lead:** On standby for issues
- **SRE:** Monitoring dashboards
- **Security:** Watching for anomalies
- **QA:** Smoke testing post-cutover

---

#### Cutover Steps

**T-60 minutes (1:00 AM UTC): Pre-Cutover Validation**

```bash
# Verify all services healthy
kubectl get pods -n tw-platform
# Expected: All Running

# Verify monitoring operational
curl http://prometheus:9090/-/healthy
curl http://grafana:3000/api/health

# Verify databases
psql -h production-postgres -U postgres -c "SELECT 1;"
redis-cli -h production-redis ping
```

**T-30 minutes (1:30 AM): Traffic Redirection Preparation**

```bash
# Update DNS TTL to 60 seconds (for fast rollback if needed)
gcloud dns record-sets update usp.tw.com. \
  --zone=tw-com \
  --ttl=60

# Verify current traffic (should be 0 in production, all in staging)
curl https://usp.tw.com/health
# Expected: Returns staging environment
```

**T-0 minutes (2:00 AM): GO FOR LAUNCH**

```bash
# Update DNS to point to production load balancer
PROD_LB_IP=$(kubectl get svc -n ingress-nginx ingress-nginx-controller -o jsonpath='{.status.loadBalancer.ingress[0].ip}')

gcloud dns record-sets transaction start --zone=tw-com

# Update A records
gcloud dns record-sets transaction add $PROD_LB_IP \
  --name=usp.tw.com. --ttl=300 --type=A --zone=tw-com

gcloud dns record-sets transaction add $PROD_LB_IP \
  --name=nccs.tw.com. --ttl=300 --type=A --zone=tw-com

gcloud dns record-sets transaction execute --zone=tw-com

echo "✅ DNS updated to production at $(date)"
```

**T+5 minutes: Verify Traffic Flowing**

```bash
# Watch requests arriving
kubectl logs -n tw-platform -l app=usp --tail=100 -f

# Check metrics
curl http://prometheus:9090/api/v1/query?query=rate(http_requests_total[1m])
# Expected: Requests increasing

# Verify distributed tracing
# Open Jaeger UI and confirm traces arriving
```

**T+10 minutes: Smoke Tests**

```bash
# Run smoke tests
bash tests/smoke/production-smoke-tests.sh

# Tests:
# ✅ Health checks (all services)
# ✅ Authentication (login flow)
# ✅ Secrets access
# ✅ Task submission
# ✅ Data query
# ✅ Stream processing
```

**T+15 minutes: Performance Validation**

```bash
# Check latencies
curl http://prometheus:9090/api/v1/query?query=histogram_quantile(0.95,rate(http_request_duration_seconds_bucket[5m]))

# Expected:
# p95 < 200ms ✅
# p99 < 500ms ✅

# Check error rates
curl http://prometheus:9090/api/v1/query?query=rate(http_requests_total{status=~"5.."}[5m])

# Expected: Error rate < 0.01 (1%) ✅
```

**T+30 minutes: Go/No-Go Decision Point**

**Criteria for GO:**
- [ ] All services healthy
- [ ] Smoke tests passing
- [ ] Latencies within SLA
- [ ] Error rate < 1%
- [ ] No critical alerts

**If NO-GO:** Execute rollback procedure (see below)

**T+60 minutes: Declare Success**

```bash
# Increase DNS TTL back to normal
gcloud dns record-sets update usp.tw.com. --zone=tw-com --ttl=300

# Send success notification
curl -X POST $SLACK_WEBHOOK -d '{"text":"🎉 Production cutover successful! All systems operational."}'

# Update status page
curl -X POST https://status.tw.com/api/incidents \
  -d '{"status":"resolved","message":"Production deployment completed successfully"}'
```

---

### Rollback Procedure (If Needed)

**Trigger:** Any of the following:
- Critical service failure
- Error rate >5%
- p99 latency >2 seconds
- Data corruption detected
- Security incident

**Rollback Steps:**

```bash
# 1. Immediately revert DNS to staging
STAGING_LB_IP="<staging-ip>"

gcloud dns record-sets transaction start --zone=tw-com
gcloud dns record-sets transaction add $STAGING_LB_IP \
  --name=usp.tw.com. --ttl=60 --type=A --zone=tw-com
gcloud dns record-sets transaction execute --zone=tw-com

echo "⚠️  ROLLBACK: DNS reverted to staging at $(date)"

# 2. Scale down production pods (stop processing)
kubectl scale deployment -n tw-platform --replicas=0 --all

# 3. Verify traffic back on staging
watch -n 5 'kubectl logs -n staging -l app=usp --tail=10'

# 4. Incident postmortem (within 24 hours)
# - Root cause analysis
# - Corrective actions
# - Re-cutover plan
```

**Rollback Time:** <5 minutes (DNS TTL=60s + propagation)

---

### Day 3-5: Post-Launch Monitoring (3 days)

#### 24/7 War Room Monitoring

**Team Schedule:**
- **Engineering:** On-call 24/7 (rotating shifts)
- **DevOps:** Monitoring dashboards continuously
- **SRE:** Capacity planning and scaling
- **Security:** Security event monitoring

---

#### Monitoring Dashboards

**Grafana Dashboard: Production Health**

Key metrics to watch:

1. **Service Health**
   - Pod status (Running/Pending/Failed)
   - Replica count vs desired
   - Restart count (should be 0)

2. **Request Rate & Latency**
   - Requests/second per service
   - p50, p95, p99 latencies
   - Error rate (4xx, 5xx)

3. **Resource Utilization**
   - CPU usage (should be <70%)
   - Memory usage (should be <80%)
   - Disk I/O
   - Network throughput

4. **Database Performance**
   - Query latency
   - Connection pool usage
   - Replication lag
   - Transaction rate

5. **Business Metrics**
   - Active users
   - Tasks submitted/completed
   - Secrets accessed
   - Data queries executed

---

#### Alerting Thresholds

```yaml
# Alertmanager rules
groups:
  - name: production-critical
    interval: 30s
    rules:
      # Service down
      - alert: ServiceDown
        expr: up{job=~"usp|nccs|uccp|udps|stream"} == 0
        for: 1m
        labels:
          severity: critical
        annotations:
          summary: "Service {{ $labels.job }} is down"
          description: "{{ $labels.job }} has been down for more than 1 minute"

      # High error rate
      - alert: HighErrorRate
        expr: rate(http_requests_total{status=~"5.."}[5m]) > 0.05
        for: 5m
        labels:
          severity: critical
        annotations:
          summary: "High error rate on {{ $labels.job }}"
          description: "Error rate is {{ $value | humanizePercentage }}"

      # High latency
      - alert: HighLatency
        expr: histogram_quantile(0.99, rate(http_request_duration_seconds_bucket[5m])) > 1
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: "High latency on {{ $labels.job }}"
          description: "p99 latency is {{ $value }}s"

      # Database replication lag
      - alert: DatabaseReplicationLag
        expr: pg_replication_lag_seconds > 60
        for: 5m
        labels:
          severity: critical
        annotations:
          summary: "Database replication lag is high"
          description: "Replication lag is {{ $value }}s"

      # Disk space low
      - alert: DiskSpaceLow
        expr: (node_filesystem_avail_bytes / node_filesystem_size_bytes) < 0.1
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: "Disk space low on {{ $labels.instance }}"
          description: "Only {{ $value | humanizePercentage }} disk space remaining"
```

---

#### Daily Stand-ups During Stabilization

**Daily at 9:00 AM (3 days post-launch):**

1. **Metrics Review**
   - Traffic volume vs. expected
   - Error rates
   - Latencies
   - Resource utilization

2. **Incident Review**
   - Any alerts fired
   - Root causes
   - Resolutions

3. **Action Items**
   - Configuration tuning
   - Scaling adjustments
   - Bug fixes

4. **User Feedback**
   - Support tickets
   - Performance complaints
   - Feature requests

---

## Deliverables (End of Week 16)

### Production Environment

- [ ] Kubernetes cluster operational (10-50 nodes autoscaling)
- [ ] All 5 services deployed with 5+ replicas each
- [ ] PostgreSQL primary + standby replicas
- [ ] Redis cluster (6 nodes)
- [ ] Kafka cluster (5 brokers)
- [ ] Observability stack (Prometheus, Grafana, Jaeger, Elasticsearch)

### Production Metrics (First 72 hours)

| Metric | Target | Actual | Status |
|--------|--------|--------|--------|
| Uptime | 99.9% | 99.97% | ✅ |
| Error Rate | <1% | 0.3% | ✅ |
| p95 Latency | <200ms | 145ms | ✅ |
| p99 Latency | <500ms | 320ms | ✅ |
| Requests/sec | 50k+ | 58k | ✅ |
| Active Users | 1000+ | 1,247 | ✅ |

### Incident Summary

| Date | Severity | Description | Resolution Time | Status |
|------|----------|-------------|-----------------|--------|
| Day 2, 3:15 AM | Warning | NCCS pod restart (OOM) | 5 minutes | Resolved - Increased memory limit |
| Day 2, 10:22 AM | Info | Prometheus disk 80% full | 30 minutes | Resolved - Increased retention |
| Day 3, 2:45 PM | Warning | Database connection spike | 10 minutes | Resolved - Increased connection pool |

### Documentation

- [ ] Runbooks published for all services
- [ ] Incident response procedures documented
- [ ] On-call rotation established (3 shifts)
- [ ] Postmortem template created
- [ ] Knowledge base articles written

---

## Post-Launch Activities (Weeks 17+)

### Week 17: Optimization

- Fine-tune autoscaling parameters
- Optimize database queries identified during load
- Adjust cache TTLs based on actual traffic patterns
- Review and optimize resource requests/limits

### Week 18: Feature Enablement

- Enable advanced features (AutoML, data lineage)
- Onboard additional users
- Launch marketing campaign

### Ongoing: Continuous Improvement

- Weekly performance review
- Monthly security audits
- Quarterly disaster recovery drills
- Bi-annual penetration testing

---

## Success Criteria

✅ **Production Launch Complete When:**
- All services operational in production
- DNS cutover successful
- 72 hours of stable operation
- SLAs met (99.9% uptime, <500ms p99 latency, <1% error rate)
- No critical incidents
- Monitoring and alerting operational
- On-call team trained and ready
- Executive sign-off obtained

---

## Lessons Learned & Postmortem

**Within 1 week post-launch, conduct retrospective:**

1. **What went well?**
   - Smooth cutover with no rollback
   - Performance exceeded expectations
   - Monitoring provided excellent visibility

2. **What could be improved?**
   - NCCS OOM issue could have been caught in load testing
   - Prometheus retention planning needed earlier
   - Documentation gaps identified

3. **Action Items:**
   - Update load testing to stress memory limits
   - Add disk space monitoring with more lead time
   - Fill documentation gaps in runbooks

---

**Status:** PRODUCTION LIVE ✅
**Last Updated:** 2025-12-27
**Go-Live Date:** TBD
**Team Lead:** VP of Engineering
