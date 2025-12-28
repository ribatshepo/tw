# Infrastructure & Database - Category Consolidation

**Category:** Infrastructure & Database Security
**Total Findings:** 2
**Total Effort:** 5 hours
**Implementation Phase:** Phase 1 (P0: Day 4) + Phase 2 (P1: Week 2, Day 9)

---

## Overview

This document consolidates all findings related to database security, SQL scripts, and infrastructure hardening.

## Findings Summary

| Finding ID | Title | Priority | Effort | Focus |
|-----------|-------|----------|--------|-------|
| SEC-P0-003 | Hardcoded SQL Passwords in Migration Scripts | P0 - CRITICAL | 2h | Secrets |
| SEC-P1-010 | Schema Scripts Lack Transaction Wrapping | P1 - HIGH | 2h | Reliability |
| SEC-P1-011 | SQL Parameterized Passwords | P1 - HIGH | 3h | Automation |

**Total Critical Path Effort:** 7 hours (P0 + P1)

**Note:** SEC-P0-003 and SEC-P1-011 are primarily documented in the Secrets Management category but included here for database context.

---

## Critical Path Analysis

### Production Blocker (P0) - Week 1, Day 2

**SEC-P0-003: Hardcoded SQL Passwords (2h)**
- **Impact:** Database credentials in plaintext in git
- **Risk:** Credential exposure, unauthorized database access
- **Fix:** Use environment variable substitution
- **Category:** Also in Secrets Management

### Pre-Production (P1) - Week 2, Day 9

**SEC-P1-010: SQL Transaction Wrapping (2h)**
- **Impact:** Failed migrations leave database inconsistent
- **Risk:** Manual cleanup required, data corruption
- **Fix:** Wrap all DDL in BEGIN/COMMIT transactions

**SEC-P1-011: SQL Parameterized Passwords (3h)**
- **Impact:** No mechanism for injecting secrets at runtime
- **Risk:** Hardcoded passwords, manual script editing
- **Fix:** Implement psql variable substitution
- **Category:** Also in Secrets Management

---

## Database Security Architecture

### Current State (Insecure)

```
┌────────────────────────────────────┐
│  Git Repository                    │
├────────────────────────────────────┤
│  02-create-roles.sql               │
│                                    │
│  CREATE USER usp_user              │
│    WITH PASSWORD                   │
│    'usp_dev_password_change_me';   │ ❌ Hardcoded
│                                    │
│  CREATE USER uccp_user             │
│    WITH PASSWORD                   │
│    'uccp_dev_password_change_me';  │ ❌ Hardcoded
└────────────────────────────────────┘

┌────────────────────────────────────┐
│  Schema Migration Scripts          │
├────────────────────────────────────┤
│  CREATE TABLE users (...);         │
│  CREATE INDEX idx_users (...);     │
│  CREATE TABLE roles (...);         │
│  -- [Script fails here]            │ ❌ No transaction
│  CREATE TABLE permissions (...);   │    (not executed)
└────────────────────────────────────┘
```

### Target State (Secure)

```
┌────────────────────────────────────┐
│  Git Repository                    │
├────────────────────────────────────┤
│  02-create-roles.sql               │
│                                    │
│  CREATE USER usp_user              │
│    WITH PASSWORD :'USP_DB_PASSWORD'; ✅ Variable
│                                    │
│  CREATE USER uccp_user             │
│    WITH PASSWORD :'UCCP_DB_PASSWORD'; ✅ Variable
└────────────────────────────────────┘
       ▲
       │ Variables from Vault
       │
┌──────┴─────────────────────────────┐
│  USP Vault                         │
│  /database/usp_user    → password  │
│  /database/uccp_user   → password  │
└────────────────────────────────────┘

┌────────────────────────────────────┐
│  Schema Migration Scripts          │
├────────────────────────────────────┤
│  BEGIN;  ✅ Transaction wrapper    │
│    CREATE TABLE users (...);       │
│    CREATE INDEX idx_users (...);   │
│    CREATE TABLE roles (...);       │
│    -- [Script fails here]          │
│    -- All changes rolled back      │ ✅ Atomic
│  COMMIT;                           │
└────────────────────────────────────┘
```

---

## Implementation Strategy

### Phase 1: Remove Hardcoded Passwords (Week 1, Day 2) - 2 hours

**SEC-P0-003: Parameterize SQL Passwords (2h)**

Documented in [secrets-management.md](secrets-management.md#phase-2-parameterize-sql-scripts-5-hours)

Quick reference:
```sql
-- 02-create-roles.sql
-- BEFORE:
CREATE USER usp_user WITH PASSWORD 'usp_dev_password_change_me';

-- AFTER:
CREATE USER usp_user WITH PASSWORD :'USP_DB_PASSWORD';
```

### Phase 2: Transaction Wrapping (Week 2, Day 9) - 2 hours

**SEC-P1-010: Add BEGIN/COMMIT to All Schema Scripts (2h)**

```sql
-- 04-uccp-schema.sql
BEGIN;  -- ✅ Start transaction

-- Grant schema access
GRANT USAGE ON SCHEMA uccp TO uccp_user;
GRANT CREATE ON SCHEMA uccp TO uccp_user;

-- 1. Task Definitions Table
CREATE TABLE uccp.task_definitions (
    task_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(255) NOT NULL,
    description TEXT,
    created_at TIMESTAMP DEFAULT NOW()
);

-- 2. Tasks Table
CREATE TABLE uccp.tasks (
    task_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    definition_id UUID REFERENCES uccp.task_definitions(task_id),
    status VARCHAR(50) NOT NULL,
    created_at TIMESTAMP DEFAULT NOW()
);

-- 3. Indexes
CREATE INDEX idx_tasks_status ON uccp.tasks(status);
CREATE INDEX idx_tasks_created_at ON uccp.tasks(created_at);

COMMIT;  -- ✅ Commit all changes atomically
```

Apply to all 5 schema scripts:
- `04-uccp-schema.sql`
- `05-usp-schema.sql`
- `06-nccs-schema.sql`
- `07-udps-schema.sql`
- `08-stream-schema.sql`

### Phase 3: Environment Variable Substitution (Week 1, Day 3) - 3 hours

**SEC-P1-011: Implement psql Variable Substitution (3h)**

Documented in [secrets-management.md](secrets-management.md#phase-2-parameterize-sql-scripts-5-hours)

**1. Create Credential Loader Script (1h)**

```bash
#!/bin/bash
# scripts/db/load-db-credentials.sh

set -euo pipefail

echo "Loading database credentials from USP Vault..."

# Fetch credentials from Vault
export USP_DB_PASSWORD=$(curl -s -H "X-Vault-Token: $VAULT_TOKEN" \
  https://usp:5001/api/v1/secrets/database/usp_user | jq -r '.password')

export UCCP_DB_PASSWORD=$(curl -s -H "X-Vault-Token: $VAULT_TOKEN" \
  https://usp:5001/api/v1/secrets/database/uccp_user | jq -r '.password')

export NCCS_DB_PASSWORD=$(curl -s -H "X-Vault-Token: $VAULT_TOKEN" \
  https://usp:5001/api/v1/secrets/database/nccs_user | jq -r '.password')

export UDPS_DB_PASSWORD=$(curl -s -H "X-Vault-Token: $VAULT_TOKEN" \
  https://usp:5001/api/v1/secrets/database/udps_user | jq -r '.password')

export STREAM_DB_PASSWORD=$(curl -s -H "X-Vault-Token: $VAULT_TOKEN" \
  https://usp:5001/api/v1/secrets/database/stream_user | jq -r '.password')

echo "✅ Credentials loaded successfully"
```

**2. Update Migration Script (1h)**

```bash
#!/bin/bash
# scripts/db/apply-migrations.sh

set -euo pipefail

# Load credentials from Vault
source scripts/db/load-db-credentials.sh

# Apply migrations with variable substitution
psql -h localhost -U postgres -d postgres \
  -v USP_DB_PASSWORD="$USP_DB_PASSWORD" \
  -v UCCP_DB_PASSWORD="$UCCP_DB_PASSWORD" \
  -v NCCS_DB_PASSWORD="$NCCS_DB_PASSWORD" \
  -v UDPS_DB_PASSWORD="$UDPS_DB_PASSWORD" \
  -v STREAM_DB_PASSWORD="$STREAM_DB_PASSWORD" \
  -f services/usp/migrations/sql/02-create-roles.sql

echo "✅ Roles created with parameterized passwords"

# Apply schema migrations (with transactions)
for script in services/usp/migrations/sql/0[4-8]-*.sql; do
  echo "Applying $script..."
  psql -h localhost -U postgres -d postgres -f "$script"
done

echo "✅ All migrations applied successfully"
```

**3. Test Parameterized Execution (1h)**

```bash
# Test with test passwords
export USP_DB_PASSWORD="test_password_$(openssl rand -base64 12)"
export UCCP_DB_PASSWORD="test_password_$(openssl rand -base64 12)"

# Run migration
bash scripts/db/apply-migrations.sh

# Verify user can login
psql -h localhost -U usp_user -d postgres -c "SELECT 1;"
# Enter password when prompted
# Expected: Connection successful
```

---

## Database Hardening Checklist

### Security Configuration

```sql
-- postgresql.conf

# Connection security
ssl = on
ssl_cert_file = '/var/lib/postgresql/server.crt'
ssl_key_file = '/var/lib/postgresql/server.key'

# Password encryption
password_encryption = scram-sha-256

# Logging
log_connections = on
log_disconnections = on
log_duration = on
log_statement = 'ddl'  # Log all DDL

# Resource limits
max_connections = 200
shared_buffers = 256MB
work_mem = 16MB

# pg_hba.conf
# TYPE  DATABASE  USER       ADDRESS       METHOD
host    all       all        10.0.0.0/8    scram-sha-256
hostssl all       all        0.0.0.0/0     scram-sha-256 clientcert=1
```

### Row-Level Security

```sql
-- Enable RLS on sensitive tables
ALTER TABLE usp.secrets ENABLE ROW LEVEL SECURITY;
ALTER TABLE usp.users ENABLE ROW LEVEL SECURITY;

-- Create policies (from SEC-P1-009)
CREATE POLICY secrets_namespace_isolation ON usp.secrets
    FOR SELECT
    USING (namespace_id IN (
        SELECT namespace_id FROM usp.user_namespaces
        WHERE user_id = current_setting('app.current_user_id')::UUID
    ));
```

---

## Testing Strategy

### Transaction Rollback Testing

```bash
# Create test script with intentional error
cat > test-transaction-rollback.sql <<'EOF'
BEGIN;

CREATE TABLE test_schema.table1 (id INT);
CREATE TABLE test_schema.table2 (id INT);
CREATE TABLE test_schema.SYNTAX ERROR HERE;  -- Fails

COMMIT;
EOF

# Run script
psql -f test-transaction-rollback.sql
# Expected: ERROR

# Verify rollback worked (no tables created)
psql -c "SELECT tablename FROM pg_tables WHERE schemaname = 'test_schema';"
# Expected: 0 rows
```

### Password Substitution Testing

```bash
# Verify no hardcoded passwords in SQL
grep -rn "PASSWORD.*'" services/usp/migrations/sql/
# Expected: 0 results (or only comments)

# Verify variable syntax present
grep -rn "PASSWORD.*:" services/usp/migrations/sql/02-create-roles.sql
# Expected: All CREATE USER statements use :VAR_NAME
```

---

## Compliance Mapping

| Finding | SOC 2 | HIPAA | PCI-DSS |
|---------|-------|-------|---------|
| SEC-P0-003 | CC6.1 | 164.312(a)(2)(iv) | Req 8.2.1 |
| SEC-P1-010 | CC8.1 | - | - |
| SEC-P1-011 | CC6.1 | - | Req 8.2.1 |

---

## Success Criteria

✅ **Complete when:**
- No hardcoded passwords in SQL scripts
- All schema scripts wrapped in BEGIN/COMMIT
- psql variable substitution working
- Credentials loaded from USP Vault
- Transaction rollback verified
- Database connections use TLS
- Row-Level Security enabled on secrets table

---

**Status:** Not Started
**Last Updated:** 2025-12-27
**Category Owner:** Database + Infrastructure Teams
