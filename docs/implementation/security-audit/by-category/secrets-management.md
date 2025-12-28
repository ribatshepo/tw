# Secrets Management - Category Consolidation

**Category:** Secrets Management
**Total Findings:** 5
**Total Effort:** 19 hours
**Implementation Phase:** Phase 1 (Week 1, Days 1-3)

---

## Overview

This document consolidates all findings related to secrets management, including hardcoded secrets, encryption keys, and secure credential handling.

## Findings Summary

| Finding ID | Title | Priority | Effort | Status |
|-----------|-------|----------|--------|--------|
| SEC-P0-001 | Hardcoded Secrets in .env Files | P0 - CRITICAL | 4h | Not Started |
| SEC-P0-002 | Hardcoded Secrets in appsettings.Development.json | P0 - CRITICAL | 3h | Not Started |
| SEC-P0-003 | Hardcoded SQL Passwords in Migration Scripts | P0 - CRITICAL | 2h | Not Started |
| SEC-P1-011 | SQL Scripts Should Use Environment Variable Substitution | P1 - HIGH | 3h | Not Started |
| SEC-P2-010 | Certificate Password Hardcoded in Script | P2 - MEDIUM | 1h | Not Started |

**Total Critical Path Effort:** 13 hours (P0 + P1)

---

## Critical Path Analysis

### Must Complete Before Production (P0)

1. **SEC-P0-001** - Move .env secrets to USP Vault
2. **SEC-P0-002** - Move appsettings secrets to USP Vault
3. **SEC-P0-003** - Parameterize SQL passwords using environment variables

**Blocking Impact:** Production deployment BLOCKED until all P0 findings resolved.

### Before Production Launch (P1)

4. **SEC-P1-011** - Implement psql variable substitution for database passwords

**Blocking Impact:** Deployment automation incomplete without this.

### Post-Production Enhancement (P2)

5. **SEC-P2-010** - Generate random certificate passwords instead of hardcoded

**Blocking Impact:** None, but improves security posture.

---

## Common Themes

### 1. Hardcoded Credentials
- **Root Cause:** Development convenience, lack of secure secret storage
- **Pattern:** Secrets committed to git in plaintext files
- **Files Affected:** `.env`, `appsettings.Development.json`, `02-create-roles.sql`, `generate-dev-certs.sh`

### 2. Missing Secret Rotation
- **Current State:** Secrets never rotated, same values since project start
- **Required:** Automated rotation via USP Vault API

### 3. No Separation of Environments
- **Current State:** Same secrets used for dev/staging/production
- **Required:** Environment-specific secrets with proper access controls

---

## Dependency Graph

```
SEC-P0-001 (Move .env to Vault)
    ↓
SEC-P0-002 (Move appsettings to Vault)
    ↓
SEC-P0-003 (Parameterize SQL passwords)
    ↓
SEC-P1-011 (SQL environment variable substitution)
    ↓
SEC-P2-010 (Random cert passwords)
```

**Sequential Implementation Required:** Must complete in this order due to dependencies.

---

## Implementation Strategy

### Phase 1: Migrate Existing Secrets (9 hours)

**Week 1, Day 1-2**

1. Create secrets in USP Vault (SEC-P0-001, SEC-P0-002)
   - Database credentials
   - JWT secrets
   - Redis passwords
   - Email service credentials
   - Encryption keys

2. Update application code to fetch from Vault
   - Modify `Program.cs` to call Vault API on startup
   - Cache secrets in memory with TTL
   - Implement graceful degradation if Vault unavailable

3. Remove hardcoded secrets from source control
   - Delete from .env, appsettings files
   - Update .gitignore to prevent re-commit
   - Purge from git history (BFG Repo-Cleaner)

### Phase 2: Parameterize SQL Scripts (5 hours)

**Week 1, Day 3**

1. Update SQL migration scripts (SEC-P0-003, SEC-P1-011)
   - Change hardcoded passwords to `:VAR_NAME` syntax
   - Create `load-db-credentials.sh` to fetch from Vault
   - Update bootstrap scripts to pass variables to psql

2. Test migrations on clean database
   - Verify environment variable substitution works
   - Confirm no hardcoded passwords remain

### Phase 3: Certificate Password Enhancement (1 hour)

**Week 1, Day 3 (if time permits)**

1. Update certificate generation script (SEC-P2-010)
   - Use `openssl rand -base64 32` for passwords
   - Save to .env file (git-ignored)
   - Mask output in terminal logs

---

## Testing Strategy

### Unit Tests
- Vault client properly handles authentication
- Secret caching mechanism works correctly
- Graceful degradation when Vault sealed

### Integration Tests
- Application starts successfully with secrets from Vault
- Database connections work with Vault-sourced credentials
- Services can authenticate to each other with Vault credentials

### Security Tests
- No hardcoded secrets detectable in source code
- Secrets not logged or exposed in error messages
- Git history purged of historical secrets

---

## Compliance Mapping

| Finding | SOC 2 | HIPAA | PCI-DSS | GDPR |
|---------|-------|-------|---------|------|
| SEC-P0-001 | CC6.1 | 164.312(a)(2)(iv) | Req 8.2.1 | Art 32 |
| SEC-P0-002 | CC6.1 | 164.312(a)(2)(iv) | Req 8.2.1 | Art 32 |
| SEC-P0-003 | CC6.1 | 164.312(a)(2)(iv) | Req 8.2.1 | Art 32 |
| SEC-P1-011 | CC6.1 | - | Req 8.2.1 | - |
| SEC-P2-010 | CC6.1 | - | Req 8.2.1 | - |

**Compliance Status:** ❌ **NON-COMPLIANT** until all P0 findings resolved.

---

## Risk Assessment

### Pre-Implementation Risks

| Risk | Likelihood | Impact | Severity |
|------|-----------|--------|----------|
| Secrets leaked in git history | High | Critical | 🔴 CRITICAL |
| Credentials compromised | Medium | Critical | 🔴 CRITICAL |
| Same credentials across environments | High | High | 🟠 HIGH |
| No secret rotation | High | Medium | 🟡 MEDIUM |

### Post-Implementation Risks

| Risk | Likelihood | Impact | Severity |
|------|-----------|--------|----------|
| Vault sealed on restart | Medium | High | 🟠 HIGH |
| Secret cache stale | Low | Medium | 🟡 MEDIUM |
| Vault API latency | Low | Low | 🟢 LOW |

**Mitigation:**
- Implement Vault auto-unseal with cloud KMS
- Set appropriate cache TTL (5-15 minutes)
- Use local cache with background refresh

---

## Verification Checklist

### Pre-Deployment Verification

- [ ] All P0 findings implemented and tested
- [ ] No secrets in git repository (current or historical)
- [ ] All services successfully fetch secrets from Vault
- [ ] Database migrations work with parameterized passwords
- [ ] Certificate generation uses random passwords

### Production Readiness

- [ ] Vault unsealed and operational
- [ ] All production secrets stored in Vault
- [ ] Access controls configured (RBAC/ABAC)
- [ ] Audit logging enabled for secret access
- [ ] Secret rotation schedule documented

### Compliance Evidence

- [ ] SOC 2 CC6.1 evidence: Screenshots of Vault configuration
- [ ] PCI-DSS Req 8.2.1 evidence: No hardcoded passwords in code scans
- [ ] HIPAA 164.312(a)(2)(iv) evidence: Secret encryption at rest/in transit

---

## Rollback Plan

If Vault integration fails in production:

1. **Emergency Fallback:** Use Kubernetes Secrets as temporary storage
2. **Restore Service:** Configure services to read from K8s secrets
3. **Incident Response:** Document failure root cause
4. **Remediation:** Fix Vault issue, re-test, re-deploy

**Rollback Time:** < 15 minutes

---

## Related Documentation

- [USP Vault Architecture](/docs/specs/security.md#vault-architecture)
- [KEK Setup Guide](/services/usp/docs/KEK-SETUP-GUIDE.md)
- [Secret Rotation Workflow](/docs/operations/SECRET_ROTATION.md) (to be created)
- [Vault Operations Runbook](/docs/runbooks/vault-operations.md) (to be created)

---

## Implementation Timeline

| Week | Day | Finding | Hours | Deliverable |
|------|-----|---------|-------|-------------|
| 1 | 1 | SEC-P0-001 | 4h | .env secrets migrated to Vault |
| 1 | 2 | SEC-P0-002 | 3h | appsettings secrets migrated to Vault |
| 1 | 2 | SEC-P0-003 | 2h | SQL passwords parameterized |
| 1 | 3 | SEC-P1-011 | 3h | SQL env var substitution implemented |
| 1 | 3 | SEC-P2-010 | 1h | Random cert passwords |

**Total:** 13 hours (1.6 days)

---

## Success Criteria

✅ **Complete when:**
- All 5 findings marked as "Completed"
- No hardcoded secrets remain in codebase
- All services successfully authenticate using Vault secrets
- Compliance evidence collected for SOC 2/HIPAA/PCI-DSS
- Production deployment unblocked

---

**Status:** Not Started
**Last Updated:** 2025-12-27
**Category Owner:** Security Engineering Team
