# Penetration Testing Scenarios

**Document Version:** 1.0
**Last Updated:** 2025-12-27
**Test Scope:** TW Platform Security Assessment
**Test Type:** Manual + Automated Penetration Testing
**Execution Frequency:** Quarterly, Pre-Release
**Owner:** Security Team + External Pentest Vendor

---

## Table of Contents

1. [Overview](#overview)
2. [Test Environment](#test-environment)
3. [Authentication Attacks](#authentication-attacks)
4. [Authorization Bypass](#authorization-bypass)
5. [Injection Attacks](#injection-attacks)
6. [Secrets Extraction](#secrets-extraction)
7. [Network Attacks](#network-attacks)
8. [API Security Testing](#api-security-testing)
9. [Database Security](#database-security)
10. [Container/K8s Security](#containerk8s-security)
11. [Post-Test Activities](#post-test-activities)

---

## Overview

### Scope

**In-Scope:**
- All 5 services (USP, NCCS, UCCP, UDPS, Stream Compute)
- API endpoints (REST, gRPC)
- Authentication/Authorization mechanisms
- Database security (PostgreSQL)
- Network security
- Container security

**Out-of-Scope:**
- Physical security
- Social engineering
- Denial of Service attacks (without prior approval)
- Third-party dependencies (unless misconfiguration)

### Methodology

Following OWASP Testing Guide v4 and PTES (Penetration Testing Execution Standard):

1. **Reconnaissance** - Information gathering
2. **Scanning** - Vulnerability identification
3. **Exploitation** - Attempt to exploit vulnerabilities
4. **Post-Exploitation** - Assess impact
5. **Reporting** - Document findings with remediation

---

## Test Environment

### Staging Environment

```bash
# Endpoints
USP:    https://usp-staging.tw.local:5001
NCCS:   https://nccs-staging.tw.local:5001
UCCP:   http://uccp-staging.tw.local:8443
UDPS:   http://udps-staging.tw.local:8443
Stream: http://stream-staging.tw.local:8082

# Test Credentials (provided by security team)
Username: pentest_user
Password: <provided-separately>
API Key: <provided-separately>
```

### Tools

```bash
# Install pentest toolkit
brew install nmap nikto sqlmap john metasploit burpsuite

# Docker-based tools
docker pull owasp/zap2docker-stable
docker pull aquasec/trivy
```

---

## Authentication Attacks

### Scenario AUTH-001: Brute Force Protection

**Objective:** Verify account lockout after failed login attempts

**Steps:**
```bash
# Attempt multiple failed logins
for i in {1..10}; do
  curl -X POST https://usp-staging.tw.local:5001/api/v1/auth/login \
    -H "Content-Type: application/json" \
    -d '{"username":"admin","password":"wrong'$i'"}'
  sleep 1
done

# Expected: Account locked after 5 attempts (SEC-P0-005 compliance)
```

**Pass Criteria:**
- [ ] Account locked after 5 failed attempts
- [ ] Lockout duration ≥15 minutes
- [ ] CAPTCHA appears after 3 failed attempts
- [ ] Alert sent to security team

---

### Scenario AUTH-002: JWT Token Manipulation

**Objective:** Attempt to forge or manipulate JWT tokens

**Steps:**
```bash
# 1. Obtain valid JWT token
TOKEN=$(curl -X POST https://usp-staging.tw.local:5001/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"pentest_user","password":"<password>"}' | jq -r '.token')

# 2. Decode JWT
echo $TOKEN | cut -d '.' -f 2 | base64 -d | jq

# 3. Attempt to modify claims (change role to admin)
# Modify payload, resign with weak key
python3 jwt_forge.py --token $TOKEN --role admin --key weak_secret

# 4. Use forged token
curl -X GET https://usp-staging.tw.local:5001/api/v1/admin/users \
  -H "Authorization: Bearer $FORGED_TOKEN"

# Expected: 401 Unauthorized (signature validation fails)
```

**Pass Criteria:**
- [ ] Forged token rejected
- [ ] Invalid signature detected
- [ ] Tampered payload detected
- [ ] Event logged in audit trail

---

### Scenario AUTH-003: Session Fixation

**Objective:** Attempt session fixation attack

**Steps:**
```bash
# 1. Obtain session cookie
COOKIE=$(curl -c - https://usp-staging.tw.local:5001/api/v1/auth/login \
  -d '{"username":"pentest_user","password":"<password>"}' | grep -i session)

# 2. Logout and try to reuse cookie
curl -X POST https://usp-staging.tw.local:5001/api/v1/auth/logout \
  -H "Cookie: $COOKIE"

curl -X GET https://usp-staging.tw.local:5001/api/v1/secrets \
  -H "Cookie: $COOKIE"

# Expected: 401 Unauthorized (session invalidated on logout)
```

**Pass Criteria:**
- [ ] Session invalidated on logout
- [ ] New session ID generated on login
- [ ] Old session cannot be reused

---

### Scenario AUTH-004: MFA Bypass Attempt

**Objective:** Attempt to bypass multi-factor authentication

**Steps:**
```bash
# 1. Login with valid username/password
curl -X POST https://usp-staging.tw.local:5001/api/v1/auth/login \
  -d '{"username":"mfa_user","password":"<password>"}'

# Expected: MFA challenge required

# 2. Attempt to access protected resource without MFA code
curl -X GET https://usp-staging.tw.local:5001/api/v1/secrets \
  -H "Authorization: Bearer <partial_token>"

# Expected: 401 Unauthorized

# 3. Attempt MFA code brute force
for code in {000000..999999}; do
  curl -X POST https://usp-staging.tw.local:5001/api/v1/auth/mfa/verify \
    -d "{\"code\":\"$code\"}"
  sleep 0.1
done

# Expected: Rate limiting after 5 attempts
```

**Pass Criteria:**
- [ ] MFA cannot be bypassed
- [ ] Rate limiting on MFA verification (SEC-P1-008)
- [ ] TOTP codes expire after 30 seconds

---

## Authorization Bypass

### Scenario AUTHZ-001: Horizontal Privilege Escalation

**Objective:** Access another user's data

**Steps:**
```bash
# 1. Create two test users: userA and userB
# 2. Login as userA, create secret
TOKEN_A=$(curl -X POST https://usp-staging.tw.local:5001/api/v1/auth/login \
  -d '{"username":"userA","password":"<password>"}' | jq -r '.token')

SECRET_ID=$(curl -X POST https://usp-staging.tw.local:5001/api/v1/secrets \
  -H "Authorization: Bearer $TOKEN_A" \
  -d '{"path":"/userA/secret","data":{"key":"value"}}' | jq -r '.id')

# 3. Login as userB, attempt to access userA's secret
TOKEN_B=$(curl -X POST https://usp-staging.tw.local:5001/api/v1/auth/login \
  -d '{"username":"userB","password":"<password>"}' | jq -r '.token')

curl -X GET https://usp-staging.tw.local:5001/api/v1/secrets/$SECRET_ID \
  -H "Authorization: Bearer $TOKEN_B"

# Expected: 403 Forbidden (Row-Level Security enforcement - SEC-P1-009)
```

**Pass Criteria:**
- [ ] Cross-user access denied
- [ ] RLS policies enforced
- [ ] Audit log records unauthorized attempt

---

### Scenario AUTHZ-002: Vertical Privilege Escalation

**Objective:** Attempt to escalate privileges from regular user to admin

**Steps:**
```bash
# 1. Login as regular user
TOKEN=$(curl -X POST https://usp-staging.tw.local:5001/api/v1/auth/login \
  -d '{"username":"regular_user","password":"<password>"}' | jq -r '.token')

# 2. Attempt to access admin endpoint
curl -X GET https://usp-staging.tw.local:5001/api/v1/admin/users \
  -H "Authorization: Bearer $TOKEN"

# Expected: 403 Forbidden

# 3. Attempt to modify own role via API
curl -X PUT https://usp-staging.tw.local:5001/api/v1/users/me \
  -H "Authorization: Bearer $TOKEN" \
  -d '{"role":"admin"}'

# Expected: 403 Forbidden or role field ignored
```

**Pass Criteria:**
- [ ] Admin endpoints require admin role
- [ ] Users cannot modify own roles
- [ ] RequirePermission attributes enforced (SEC-P1-008)

---

## Injection Attacks

### Scenario INJ-001: SQL Injection

**Objective:** Attempt SQL injection in database queries

**Steps:**
```bash
# Test various injection points

# 1. Search endpoint
curl "https://usp-staging.tw.local:5001/api/v1/secrets?path=/test' OR '1'='1"

# 2. User lookup
curl "https://usp-staging.tw.local:5001/api/v1/users?username=admin'--"

# 3. Blind SQL injection (time-based)
curl "https://usp-staging.tw.local:5001/api/v1/secrets?id=1' AND SLEEP(5)--"

# Expected: No database errors, input properly sanitized
```

**Tools:**
```bash
# Automated SQL injection scan
sqlmap -u "https://usp-staging.tw.local:5001/api/v1/secrets?path=test" \
  --cookie="session=<token>" \
  --level=5 --risk=3

# Expected: No SQL injection vulnerabilities found
```

**Pass Criteria:**
- [ ] All inputs parameterized (no string concatenation)
- [ ] ORM (Entity Framework) used correctly
- [ ] No database errors exposed to user

---

### Scenario INJ-002: Command Injection

**Objective:** Attempt OS command injection

**Steps:**
```bash
# Test command injection in file upload, export, etc.

# 1. File export endpoint
curl -X POST https://udps-staging.tw.local:8443/api/v1/export \
  -d '{"filename":"test.csv; rm -rf /"}'

# 2. Report generation
curl -X POST https://nccs-staging.tw.local:5001/api/v1/reports/generate \
  -d '{"name":"report$(whoami).pdf"}'

# Expected: Input validation prevents command execution
```

**Pass Criteria:**
- [ ] No shell commands executed from user input
- [ ] Filename validation enforced
- [ ] Process isolation (containers)

---

## Secrets Extraction

### Scenario SEC-001: Vault Seal Bypass

**Objective:** Attempt to access secrets from sealed Vault

**Steps:**
```bash
# 1. Seal the vault
curl -k -X POST https://usp-staging.tw.local:5001/api/v1/vault/seal \
  -H "X-Vault-Token: $VAULT_TOKEN"

# 2. Attempt to retrieve secret
curl -k -X GET https://usp-staging.tw.local:5001/api/v1/secrets/database/password

# Expected: 503 Service Unavailable (Vault sealed)

# 3. Attempt direct database access (bypass vault)
psql -h postgres-staging -U postgres -d usp_dev -c \
  "SELECT encrypted_data FROM usp.secrets WHERE path='/database/password';"

# Expected: Encrypted data only (master key in sealed vault - SEC-P0-001)
```

**Pass Criteria:**
- [ ] Sealed vault blocks secret access
- [ ] Secrets encrypted at rest
- [ ] Master key protected by Shamir's Secret Sharing

---

### Scenario SEC-002: Memory Dump Analysis

**Objective:** Attempt to extract secrets from memory

**Steps:**
```bash
# 1. Dump process memory
kubectl exec -n tw-platform usp-<pod-id> -- \
  gcore $(pidof dotnet)

# 2. Search for sensitive patterns
strings core.123 | grep -i "password\|secret\|key"

# Expected: Secrets zeroed from memory after use
```

**Pass Criteria:**
- [ ] Secrets cleared from memory after use
- [ ] Memory encryption (if feasible)
- [ ] Minimal secret exposure time

---

## Network Attacks

### Scenario NET-001: Man-in-the-Middle (MITM)

**Objective:** Attempt to intercept inter-service communication

**Steps:**
```bash
# 1. Intercept traffic between NCCS and UCCP
sudo tcpdump -i eth0 -w traffic.pcap \
  'host nccs-staging and port 50000'

# 2. Analyze captured traffic
wireshark traffic.pcap

# Expected: All traffic encrypted with mTLS (SEC-P0-008)
```

**Pass Criteria:**
- [ ] All inter-service communication uses mTLS
- [ ] Certificate validation enforced
- [ ] No plaintext secrets in network traffic

---

### Scenario NET-002: Certificate Validation Bypass

**Objective:** Attempt to use invalid/expired certificates

**Steps:**
```bash
# 1. Generate self-signed certificate
openssl req -x509 -newkey rsa:2048 -keyout fake.key -out fake.crt -days 1 -nodes

# 2. Attempt to connect with fake certificate
curl --cert fake.crt --key fake.key \
  https://usp-staging.tw.local:5001/api/v1/health

# Expected: Connection refused (certificate validation)
```

**Pass Criteria:**
- [ ] Invalid certificates rejected
- [ ] Expired certificates rejected
- [ ] Certificate revocation checking (CRL/OCSP if implemented - SEC-P3-001)

---

## API Security Testing

### Scenario API-001: Rate Limiting Bypass

**Objective:** Attempt to bypass rate limiting

**Steps:**
```bash
# 1. Send requests above rate limit
for i in {1..1000}; do
  curl -X POST https://usp-staging.tw.local:5001/api/v1/auth/login \
    -d '{"username":"test","password":"test"}' &
done

# Expected: 429 Too Many Requests after threshold

# 2. Rotate IP addresses (if applicable)
# 3. Use different User-Agents

# Expected: Rate limiting still enforced
```

**Pass Criteria:**
- [ ] Rate limiting enforced per IP
- [ ] Rate limiting enforced per user
- [ ] Distributed rate limiting (Redis-based)

---

### Scenario API-002: Mass Assignment

**Objective:** Attempt to modify protected fields via API

**Steps:**
```bash
# Attempt to set isAdmin=true via user update
curl -X PUT https://usp-staging.tw.local:5001/api/v1/users/me \
  -H "Authorization: Bearer $TOKEN" \
  -d '{
    "username": "pentest_user",
    "email": "pentest@tw.local",
    "isAdmin": true,
    "role": "admin"
  }'

# Expected: Protected fields ignored or 403 Forbidden
```

**Pass Criteria:**
- [ ] Binding attributes restrict modifiable fields
- [ ] DTO validation enforces allowed properties
- [ ] Audit log records attempt

---

## Database Security

### Scenario DB-001: Direct Database Access

**Objective:** Attempt to connect to database directly

**Steps:**
```bash
# 1. Port scan for PostgreSQL
nmap -p 5432 postgres-staging.tw.local

# Expected: Port filtered (firewall/network policy)

# 2. Attempt connection
psql -h postgres-staging.tw.local -U postgres

# Expected: Connection refused or requires certificate
```

**Pass Criteria:**
- [ ] Database not publicly accessible
- [ ] Kubernetes NetworkPolicy blocks external access
- [ ] TLS required for connections (SEC-P0-008)

---

### Scenario DB-002: Privilege Escalation via SQL

**Objective:** Attempt to escalate database privileges

**Steps:**
```bash
# Login with application database user
psql -h postgres-staging.tw.local -U usp_user -d usp_dev

# Attempt privilege escalation
usp_dev=> ALTER USER usp_user WITH SUPERUSER;

# Expected: Permission denied

# Attempt to access system tables
usp_dev=> SELECT * FROM pg_authid;

# Expected: Permission denied
```

**Pass Criteria:**
- [ ] Application user has minimal privileges
- [ ] Cannot escalate to superuser
- [ ] Row-Level Security prevents cross-user access (SEC-P1-009)

---

## Container/K8s Security

### Scenario K8S-001: Container Escape

**Objective:** Attempt to break out of container

**Steps:**
```bash
# 1. Exec into container
kubectl exec -it -n tw-platform usp-<pod-id> -- /bin/bash

# 2. Attempt to access host filesystem
ls -la /host

# Expected: /host not mounted

# 3. Attempt privileged operations
docker ps

# Expected: docker not available

# 4. Check for capabilities
cat /proc/self/status | grep Cap

# Expected: Minimal capabilities (no CAP_SYS_ADMIN, CAP_NET_RAW, etc.)
```

**Pass Criteria:**
- [ ] Containers run as non-root (SEC-P2-012)
- [ ] No privileged containers
- [ ] securityContext enforced
- [ ] Read-only root filesystem where possible

---

### Scenario K8S-002: Secrets Exposure via K8s API

**Objective:** Attempt to access Kubernetes secrets

**Steps:**
```bash
# 1. From within container, attempt to access K8s API
TOKEN=$(cat /var/run/secrets/kubernetes.io/serviceaccount/token)
CA=/var/run/secrets/kubernetes.io/serviceaccount/ca.crt

curl --cacert $CA -H "Authorization: Bearer $TOKEN" \
  https://kubernetes.default.svc/api/v1/namespaces/tw-platform/secrets

# Expected: 403 Forbidden (RBAC prevents access - SEC-P2-012)
```

**Pass Criteria:**
- [ ] Service accounts have minimal RBAC permissions
- [ ] Secrets not accessible from application pods
- [ ] Pod Security Standards enforced

---

## Post-Test Activities

### Findings Report

**Template:**

| ID | Severity | Title | Description | Impact | Remediation | Status |
|----|----------|-------|-------------|--------|-------------|--------|
| PT-001 | HIGH | Session Fixation | Sessions not invalidated on logout | Session hijacking | Implement session invalidation | Open |
| PT-002 | MEDIUM | Verbose Error Messages | Stack traces exposed | Information disclosure | Generic error messages | Fixed |

**Severity Levels:**
- **CRITICAL:** Immediate exploitation, high impact
- **HIGH:** Exploitable with moderate effort, significant impact
- **MEDIUM:** Requires specific conditions, moderate impact
- **LOW:** Difficult to exploit, minimal impact
- **INFO:** No immediate security impact

### Remediation Tracking

- **Critical/High:** Fix within 7 days
- **Medium:** Fix within 30 days
- **Low:** Fix within 90 days
- **Info:** Address in next release

### Re-testing

After remediation:
1. Verify fixes with targeted re-tests
2. Regression testing to ensure no new issues
3. Update this document with results
4. Close findings in tracking system

---

**Next Pentest Date:** 2025-06-27 (Quarterly)
**Contact:** security@tw.com
**External Vendor:** TBD (if applicable)

---

**END OF PENETRATION TESTING SCENARIOS**
