# SEC-P1-011: SQL Parameterized Passwords

## 1. Metadata

| Field | Value |
|-------|-------|
| **Finding ID** | SEC-P1-011 |
| **Title** | SQL Scripts Should Use Environment Variable Substitution for Passwords |
| **Priority** | P1 - HIGH |
| **Severity** | High |
| **Category** | Secrets Management / Database |
| **Status** | Not Started |
| **Effort Estimate** | 3 hours |
| **Implementation Phase** | Phase 1 (Week 1, Day 3) - Related to SEC-P0-003 |
| **Assigned To** | Database Engineer + DevOps Engineer |
| **Created** | 2025-12-27 |

---

## 2. Cross-References

| Type | Reference |
|------|-----------|
| **Audit Report** | `/home/tshepo/projects/tw/COMPREHENSIVE_AUDIT_REPORT.md:295-305` |
| **Code Files** | `/home/tshepo/projects/tw/services/usp/migrations/sql/02-create-roles.sql` |
| **Related Findings** | SEC-P0-003 (Hardcoded SQL Passwords - Critical) |
| **Dependencies** | None |
| **Compliance Impact** | SOC 2 (CC6.1), PCI-DSS (Req 8.2.1) |

---

## 3. Executive Summary

### Problem

While SEC-P0-003 addresses the immediate critical issue of hardcoded passwords, this finding addresses the **mechanism** for parameterized password injection in SQL scripts using environment variables.

### Impact

- **Non-Parameterized Approach:** Passwords hardcoded, not templated
- **No Environment Variable Support:** Cannot inject secrets at runtime
- **Manual Password Management:** Requires script editing to change passwords

### Solution

Implement PostgreSQL environment variable substitution using `psql` variables (`:VAR_NAME`) for password parameters.

---

## 4. Implementation Guide

### Step 1: Update 02-create-roles.sql with Variables (1 hour)

```sql
-- 02-create-roles.sql

-- ✅ CHANGE: Use psql variables instead of hardcoded passwords

-- UCCP Role
CREATE ROLE uccp_role;
CREATE USER uccp_user WITH PASSWORD :'UCCP_DB_PASSWORD';  -- Variable substitution
GRANT uccp_role TO uccp_user;

-- NCCS Role
CREATE ROLE nccs_role;
CREATE USER nccs_user WITH PASSWORD :'NCCS_DB_PASSWORD';  -- Variable substitution
GRANT nccs_role TO nccs_user;

-- USP Role
CREATE ROLE usp_role;
CREATE USER usp_user WITH PASSWORD :'USP_DB_PASSWORD';  -- Variable substitution
GRANT usp_role TO usp_user;

-- UDPS Role
CREATE ROLE udps_role;
CREATE USER udps_user WITH PASSWORD :'UDPS_DB_PASSWORD';  -- Variable substitution
GRANT udps_role TO udps_user;

-- Stream Compute Role
CREATE ROLE stream_role;
CREATE USER stream_user WITH PASSWORD :'STREAM_DB_PASSWORD';  -- Variable substitution
GRANT stream_role TO stream_user;
```

### Step 2: Create Environment Variable Loader (1 hour)

```bash
#!/bin/bash
# scripts/db/load-db-credentials.sh

set -euo pipefail

# Load database passwords from USP Vault
export UCCP_DB_PASSWORD=$(curl -s -H "X-Vault-Token: $VAULT_TOKEN" \
  https://usp:5001/api/v1/secrets/database/uccp_user | jq -r '.password')

export NCCS_DB_PASSWORD=$(curl -s -H "X-Vault-Token: $VAULT_TOKEN" \
  https://usp:5001/api/v1/secrets/database/nccs_user | jq -r '.password')

export USP_DB_PASSWORD=$(curl -s -H "X-Vault-Token: $VAULT_TOKEN" \
  https://usp:5001/api/v1/secrets/database/usp_user | jq -r '.password')

export UDPS_DB_PASSWORD=$(curl -s -H "X-Vault-Token: $VAULT_TOKEN" \
  https://usp:5001/api/v1/secrets/database/udps_user | jq -r '.password')

export STREAM_DB_PASSWORD=$(curl -s -H "X-Vault-Token: $VAULT_TOKEN" \
  https://usp:5001/api/v1/secrets/database/stream_user | jq -r '.password')

echo "Database credentials loaded from USP Vault"
```

### Step 3: Update Migration Script to Use Variables (30 minutes)

```bash
#!/bin/bash
# scripts/db/apply-migrations.sh

set -euo pipefail

# Source credentials from Vault
source scripts/db/load-db-credentials.sh

# Apply migrations with environment variable substitution
psql -h localhost -U postgres -d postgres \
  -v UCCP_DB_PASSWORD="$UCCP_DB_PASSWORD" \
  -v NCCS_DB_PASSWORD="$NCCS_DB_PASSWORD" \
  -v USP_DB_PASSWORD="$USP_DB_PASSWORD" \
  -v UDPS_DB_PASSWORD="$UDPS_DB_PASSWORD" \
  -v STREAM_DB_PASSWORD="$STREAM_DB_PASSWORD" \
  -f services/usp/migrations/sql/02-create-roles.sql

echo "Roles created with parameterized passwords"
```

### Step 4: Test Parameterized Execution (30 minutes)

```bash
# Test that environment variables are correctly substituted

# Set test passwords
export UCCP_DB_PASSWORD="test_password_123"
export NCCS_DB_PASSWORD="test_password_456"
export USP_DB_PASSWORD="test_password_789"
export UDPS_DB_PASSWORD="test_password_abc"
export STREAM_DB_PASSWORD="test_password_xyz"

# Run migration script
bash scripts/db/apply-migrations.sh

# Verify user can login with environment password
psql -h localhost -U uccp_user -d postgres -c "SELECT 1;"
# Prompted for password: enter "test_password_123"
# Expected: Connection successful
```

---

## 5. Testing

- [ ] SQL scripts use `:VAR_NAME` syntax for passwords
- [ ] Environment variables loaded from USP Vault
- [ ] Migration script passes variables to psql
- [ ] Database users created with correct passwords
- [ ] No hardcoded passwords remain in SQL files

---

## 6. Compliance Evidence

**SOC 2 CC6.1:** Passwords not hardcoded, sourced from secure vault
**PCI-DSS Req 8.2.1:** System passwords not stored in plaintext

---

## 7. Sign-Off

- [ ] **Database Engineer:** Parameterized SQL scripts implemented
- [ ] **DevOps:** Credential loading from Vault verified
- [ ] **Security:** No hardcoded passwords in source code

---

**Finding Status:** Not Started
**Last Updated:** 2025-12-27

---

**End of SEC-P1-011**
