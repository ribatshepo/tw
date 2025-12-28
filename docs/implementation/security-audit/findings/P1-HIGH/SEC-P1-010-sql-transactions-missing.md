# SEC-P1-010: Schema Scripts Lack Transaction Wrapping

## 1. Metadata

| Field | Value |
|-------|-------|
| **Finding ID** | SEC-P1-010 |
| **Title** | Schema DDL Scripts Missing BEGIN/COMMIT Transaction Wrapping |
| **Priority** | P1 - HIGH |
| **Severity** | Medium |
| **Category** | Database / Infrastructure |
| **Status** | Not Started |
| **Effort Estimate** | 2 hours |
| **Implementation Phase** | Phase 2 (Week 2, Day 9) |
| **Assigned To** | Database Engineer |
| **Created** | 2025-12-27 |

---

## 2. Cross-References

| Type | Reference |
|------|-----------|
| **Audit Report** | `/home/tshepo/projects/tw/COMPREHENSIVE_AUDIT_REPORT.md:307-311` |
| **Code Files** | `04-uccp-schema.sql`, `05-usp-schema.sql`, `06-nccs-schema.sql`, `07-udps-schema.sql`, `08-stream-schema.sql` |
| **Dependencies** | None |
| **Compliance Impact** | SOC 2 (CC7.2 - Change Management) |

---

## 3. Executive Summary

### Problem

Schema DDL scripts (04-08) lack `BEGIN; ... COMMIT;` transaction wrapping. If a script fails partway through, database left in inconsistent state.

### Impact

- **Partial Schema Creation:** Failed migrations leave incomplete tables/indexes
- **Manual Cleanup Required:** Database must be manually fixed after failures
- **Rollback Impossible:** Cannot automatically rollback failed schema changes

### Solution

Wrap all schema DDL in PostgreSQL transactions with `BEGIN; ... COMMIT;`.

---

## 4. Implementation Guide

### Step 1: Wrap 04-uccp-schema.sql (20 minutes)

```sql
-- 04-uccp-schema.sql

BEGIN;  -- ✅ ADD transaction wrapper

-- Grant schema access
GRANT USAGE ON SCHEMA uccp TO uccp_user;
GRANT CREATE ON SCHEMA uccp TO uccp_user;

-- 1. Task Definitions Table
CREATE TABLE uccp.task_definitions (
    task_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    -- ... rest of table definition
);

-- ... all other tables and indexes

COMMIT;  -- ✅ ADD transaction commit
```

### Step 2: Wrap 05-usp-schema.sql (20 minutes)

```sql
-- 05-usp-schema.sql

BEGIN;  -- ✅ ADD transaction wrapper

-- Grant schema access
GRANT USAGE ON SCHEMA usp TO usp_user;
GRANT CREATE ON SCHEMA usp TO usp_user;

-- 1. Users Table
CREATE TABLE usp.users (
    user_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    -- ... rest of table definition
);

-- ... all other tables and indexes

COMMIT;  -- ✅ ADD transaction commit
```

### Step 3: Wrap 06-nccs-schema.sql (20 minutes)

```sql
-- 06-nccs-schema.sql

BEGIN;  -- ✅ ADD transaction wrapper

-- Grant schema access
GRANT USAGE ON SCHEMA nccs TO nccs_user;
GRANT CREATE ON SCHEMA nccs TO nccs_user;

-- 1. API Requests Log Table
CREATE TABLE nccs.api_requests (
    request_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    -- ... rest of table definition
);

-- ... all other tables and indexes

COMMIT;  -- ✅ ADD transaction commit
```

### Step 4: Wrap 07-udps-schema.sql (20 minutes)

```sql
-- 07-udps-schema.sql

BEGIN;  -- ✅ ADD transaction wrapper

-- Grant schema access
GRANT USAGE ON SCHEMA udps TO udps_user;
GRANT CREATE ON SCHEMA udps TO udps_user;

-- 1. Datasets Table
CREATE TABLE udps.datasets (
    dataset_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    -- ... rest of table definition
);

-- ... all other tables and indexes

COMMIT;  -- ✅ ADD transaction commit
```

### Step 5: Wrap 08-stream-schema.sql (20 minutes)

```sql
-- 08-stream-schema.sql

BEGIN;  -- ✅ ADD transaction wrapper

-- Grant schema access
GRANT USAGE ON SCHEMA stream_compute TO stream_user;
GRANT CREATE ON SCHEMA stream_compute TO stream_user;

-- 1. Stream Jobs Table
CREATE TABLE stream_compute.stream_jobs (
    job_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    -- ... rest of table definition
);

-- ... all other tables and indexes

COMMIT;  -- ✅ ADD transaction commit
```

### Step 6: Test Rollback on Failure (20 minutes)

```bash
# Test that transaction rollback works

# Create test script with intentional error
cat > test-rollback.sql <<'EOF'
BEGIN;

CREATE TABLE test_schema.test_table_1 (id INT);
CREATE TABLE test_schema.test_table_2 (id INT);
CREATE TABLE test_schema.INVALID SYNTAX HERE;  -- This will fail

COMMIT;
EOF

# Run test script
psql -h localhost -U postgres -d usp_dev -f test-rollback.sql
# Expected: ERROR, then verify no tables created (rollback worked)

psql -h localhost -U postgres -d usp_dev -c "SELECT tablename FROM pg_tables WHERE schemaname = 'test_schema';"
# Expected: 0 rows (rollback succeeded)
```

---

## 5. Testing

- [ ] All 5 schema scripts wrapped in transactions
- [ ] Scripts execute successfully with BEGIN/COMMIT
- [ ] Failed scripts rollback automatically (no partial schema)
- [ ] Idempotent execution (can re-run after failure)

---

## 6. Compliance Evidence

**SOC 2 CC7.2:** Change management includes automatic rollback on failure

---

## 7. Sign-Off

- [ ] **Database Engineer:** All schema scripts transactional
- [ ] **DevOps:** Migration process updated

---

**Finding Status:** Not Started
**Last Updated:** 2025-12-27

---

**End of SEC-P1-010**
