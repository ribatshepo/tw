# SEC-P2-007: TROUBLESHOOTING Missing

## 1. Metadata

| Field | Value |
|-------|-------|
| **Finding ID** | SEC-P2-007 |
| **Title** | No TROUBLESHOOTING.md Debugging Guide |
| **Priority** | P2 - MEDIUM |
| **Severity** | Medium |
| **Category** | Documentation |
| **Status** | Not Started |
| **Effort Estimate** | 6 hours |
| **Implementation Phase** | Phase 3 (Week 3, Day 7) |
| **Assigned To** | SRE + Support Team |
| **Created** | 2025-12-27 |

---

## 2. Cross-References

| Type | Reference |
|------|-----------|
| **Audit Report** | `/home/tshepo/projects/tw/COMPREHENSIVE_AUDIT_REPORT.md:477-486` |
| **Code Files** | None (file missing) |
| **Dependencies** | None |
| **Compliance Impact** | SOC 2 (CC7.2 - Incident Response) |

---

## 3. Executive Summary

### Problem

No TROUBLESHOOTING.md guide for debugging common issues.

### Impact

- **Long Resolution Times:** No documented solutions for common problems
- **Repeated Issues:** Same problems solved multiple times
- **Poor Operational Efficiency:** Engineers waste time debugging known issues

### Solution

Create comprehensive TROUBLESHOOTING.md with common issues, diagnostics, and solutions organized by service.

---

## 4. Implementation Guide

### Step 1: Create docs/TROUBLESHOOTING.md (5 hours)

```markdown
# TW Platform Troubleshooting Guide

This guide covers common issues and their solutions.

## Table of Contents

- [General Diagnostics](#general-diagnostics)
- [USP Issues](#usp-unified-security-platform)
- [UCCP Issues](#uccp-compute-platform)
- [Database Issues](#database-issues)
- [Network & TLS Issues](#network--tls-issues)
- [Performance Issues](#performance-issues)

## General Diagnostics

### Check Service Health

```bash
# All services
kubectl get pods -n tw-platform

# Specific service health
curl -k https://usp.example.com/health
curl https://uccp.example.com/health

# View logs
kubectl logs -n tw-platform -l app=usp --tail=100 --follow
```

### Check Resource Usage

```bash
# Pod resource usage
kubectl top pods -n tw-platform

# Node resource usage
kubectl top nodes
```

## USP (Unified Security Platform)

### Issue: Vault Sealed After Restart

**Symptoms:**
- Health endpoint returns `{"vault":{"sealed":true}}`
- API requests fail with "Vault is sealed"

**Diagnosis:**
```bash
curl -k https://localhost:5001/api/v1/vault/seal/status
# Response: {"sealed":true,"threshold":3,"sharesProvided":0}
```

**Solution:**
```bash
# Unseal with 3 keys (retrieve from secure storage)
curl -k -X POST https://localhost:5001/api/v1/vault/seal/unseal \
  -H "Content-Type: application/json" \
  -d '{"key":"<unseal-key-1>"}'

curl -k -X POST https://localhost:5001/api/v1/vault/seal/unseal \
  -H "Content-Type: application/json" \
  -d '{"key":"<unseal-key-2>"}'

curl -k -X POST https://localhost:5001/api/v1/vault/seal/unseal \
  -H "Content-Type: application/json" \
  -d '{"key":"<unseal-key-3>"}'
```

### Issue: 401 Unauthorized on Authenticated Requests

**Symptoms:**
- Valid JWT token rejected
- Error: "Invalid token" or "Token expired"

**Diagnosis:**
```bash
# Decode JWT token
echo "<token>" | cut -d'.' -f2 | base64 -d | jq .

# Check token expiration
# Look for "exp" claim (Unix timestamp)
```

**Solution:**
```bash
# Refresh token
curl -k -X POST https://localhost:5001/api/v1/auth/refresh \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer <old-token>" \
  -d '{"refreshToken":"<refresh-token>"}'
```

### Issue: Database Connection Failed

**Symptoms:**
- USP fails to start
- Error: "Failed to connect to database"

**Diagnosis:**
```bash
# Test database connection
psql -h localhost -U usp_user -d usp_prod

# Check connection string
kubectl get configmap -n tw-platform usp-config -o yaml | grep DatabaseConnection
```

**Solution:**
```bash
# Verify database is running
kubectl get pods -n tw-infrastructure -l app=postgresql

# Check credentials
kubectl get secret -n tw-platform db-credentials -o jsonpath='{.data.postgres-password}' | base64 -d

# Restart database connection pool
kubectl rollout restart deployment/usp -n tw-platform
```

## UCCP (Compute Platform)

### Issue: Raft Cluster Split Brain

**Symptoms:**
- Multiple leaders elected
- Inconsistent state across nodes

**Diagnosis:**
```bash
# Check Raft status on each node
kubectl exec -n tw-platform uccp-0 -- curl localhost:8080/api/v1/raft/status
kubectl exec -n tw-platform uccp-1 -- curl localhost:8080/api/v1/raft/status
kubectl exec -n tw-platform uccp-2 -- curl localhost:8080/api/v1/raft/status
```

**Solution:**
```bash
# Stop all UCCP pods
kubectl scale statefulset uccp -n tw-platform --replicas=0

# Clear Raft data (WARNING: data loss)
kubectl delete pvc -n tw-platform -l app=uccp

# Restart cluster
kubectl scale statefulset uccp -n tw-platform --replicas=3

# Reinitialize Raft cluster
kubectl exec -n tw-platform uccp-0 -- ./uccp raft init
```

### Issue: Tasks Stuck in Pending State

**Symptoms:**
- Submitted tasks never execute
- Task status remains "pending"

**Diagnosis:**
```bash
# Check worker nodes registered
curl https://uccp.example.com/api/v1/workers

# Check task queue
curl https://uccp.example.com/api/v1/tasks?status=pending
```

**Solution:**
```bash
# Register worker nodes manually if missing
curl -X POST https://uccp.example.com/api/v1/workers/register \
  -d '{"nodeId":"worker-1","capacity":{"cpu":8,"memory":16}}'

# Or restart scheduler
kubectl rollout restart deployment/uccp-scheduler -n tw-platform
```

## Database Issues

### Issue: PostgreSQL Out of Connections

**Symptoms:**
- Error: "FATAL: remaining connection slots are reserved"
- New connections rejected

**Diagnosis:**
```bash
# Check active connections
psql -h localhost -U postgres -c "SELECT count(*) FROM pg_stat_activity;"

# Check connection limit
psql -h localhost -U postgres -c "SHOW max_connections;"
```

**Solution:**
```bash
# Increase max_connections in postgresql.conf
kubectl edit configmap -n tw-infrastructure postgres-config

# Add/update:
# max_connections = 200

# Restart PostgreSQL
kubectl rollout restart statefulset/postgres -n tw-infrastructure

# Or kill idle connections
psql -h localhost -U postgres -c "
SELECT pg_terminate_backend(pid)
FROM pg_stat_activity
WHERE state = 'idle'
AND state_change < now() - interval '10 minutes';
"
```

### Issue: Slow Database Queries

**Symptoms:**
- API requests timeout
- High database CPU usage

**Diagnosis:**
```bash
# Find slow queries
psql -h localhost -U postgres -c "
SELECT pid, now() - query_start as duration, query
FROM pg_stat_activity
WHERE state = 'active'
ORDER BY duration DESC
LIMIT 10;
"

# Check missing indexes
psql -h localhost -U postgres -d usp_prod -c "
SELECT schemaname, tablename, attname, n_distinct, correlation
FROM pg_stats
WHERE schemaname = 'usp'
AND correlation < 0.5;
"
```

**Solution:**
```bash
# Add missing indexes
psql -h localhost -U postgres -d usp_prod -c "
CREATE INDEX CONCURRENTLY idx_secrets_namespace_id ON usp.secrets(namespace_id);
CREATE INDEX CONCURRENTLY idx_secrets_created_at ON usp.secrets(created_at);
"

# Analyze tables
psql -h localhost -U postgres -d usp_prod -c "ANALYZE;"
```

## Network & TLS Issues

### Issue: TLS Certificate Expired

**Symptoms:**
- Browser shows "Certificate Expired"
- API requests fail with SSL error

**Diagnosis:**
```bash
# Check certificate expiration
openssl s_client -connect usp.example.com:443 -servername usp.example.com </dev/null 2>/dev/null | \
  openssl x509 -noout -dates
```

**Solution:**
```bash
# Renew certificate with cert-manager
kubectl delete certificate -n tw-platform usp-tls

# cert-manager will auto-renew
# Or manually trigger renewal
kubectl annotate certificate -n tw-platform usp-tls \
  cert-manager.io/issue-temporary-certificate="true"
```

### Issue: Service Cannot Reach Another Service

**Symptoms:**
- Error: "Connection refused" or "Connection timeout"
- Inter-service communication fails

**Diagnosis:**
```bash
# Test network connectivity
kubectl exec -n tw-platform deploy/usp -- curl -k https://uccp:8080/health

# Check service DNS
kubectl exec -n tw-platform deploy/usp -- nslookup uccp.tw-platform.svc.cluster.local

# Check NetworkPolicies
kubectl get networkpolicy -n tw-platform
```

**Solution:**
```bash
# Verify service exists
kubectl get svc -n tw-platform

# Check pod labels match service selector
kubectl get pod -n tw-platform --show-labels
kubectl get svc uccp -n tw-platform -o yaml | grep selector

# Update NetworkPolicy if blocking
kubectl edit networkpolicy -n tw-platform allow-inter-service
```

## Performance Issues

### Issue: High Memory Usage

**Symptoms:**
- Pods OOMKilled (Out of Memory)
- Slow response times

**Diagnosis:**
```bash
# Check memory usage
kubectl top pods -n tw-platform

# View OOMKill events
kubectl get events -n tw-platform | grep OOMKilled
```

**Solution:**
```bash
# Increase memory limits
kubectl edit deployment usp -n tw-platform

# Update:
# resources:
#   limits:
#     memory: 2Gi
#   requests:
#     memory: 1Gi

# Or enable horizontal pod autoscaling
kubectl autoscale deployment usp -n tw-platform \
  --cpu-percent=70 \
  --min=3 \
  --max=10
```

### Issue: High CPU Usage

**Symptoms:**
- Pods CPU throttled
- Slow response times

**Diagnosis:**
```bash
# Check CPU usage
kubectl top pods -n tw-platform

# Profile application (if instrumented)
curl https://usp.example.com/debug/pprof/profile?seconds=30 > cpu.prof
```

**Solution:**
```bash
# Increase CPU limits
kubectl edit deployment usp -n tw-platform

# Scale horizontally
kubectl scale deployment usp -n tw-platform --replicas=5
```

## Getting Help

If issues persist:
1. **Check Logs:** `kubectl logs -n tw-platform -l app=<service> --tail=1000`
2. **Check Metrics:** Visit Grafana dashboards
3. **Check Traces:** Visit Jaeger for distributed traces
4. **Contact Support:** Open GitHub issue with logs and symptoms
```

---

## 5. Testing

- [ ] TROUBLESHOOTING.md created
- [ ] All common issues documented
- [ ] Solutions verified on staging environment
- [ ] Diagnostic commands tested
- [ ] Links to monitoring tools included

---

## 6. Compliance Evidence

**SOC 2 CC7.2:** Incident response procedures documented

---

## 7. Sign-Off

- [ ] **SRE:** Troubleshooting guide complete
- [ ] **Support:** Common issues documented

---

**Finding Status:** Not Started
**Last Updated:** 2025-12-27

---

**End of SEC-P2-007**
