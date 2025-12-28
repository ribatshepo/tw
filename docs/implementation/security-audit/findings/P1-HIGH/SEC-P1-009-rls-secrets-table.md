# SEC-P1-009: Row-Level Security Not Enabled

## 1. Metadata

| Field | Value |
|-------|-------|
| **Finding ID** | SEC-P1-009 |
| **Title** | Secrets Table Lacks Row-Level Security Policies |
| **Priority** | P1 - HIGH |
| **Severity** | High |
| **Category** | Database Security |
| **Status** | Not Started |
| **Effort Estimate** | 4 hours |
| **Implementation Phase** | Phase 2 (Week 2, Day 12) |
| **Assigned To** | Database Engineer |
| **Created** | 2025-12-27 |

---

## 2. Cross-References

| Type | Reference |
|------|-----------|
| **Audit Report** | `/home/tshepo/projects/tw/COMPREHENSIVE_AUDIT_REPORT.md:313-317` |
| **Code Files** | `/home/tshepo/projects/tw/services/usp/migrations/sql/05-usp-schema.sql` |
| **Dependencies** | None |
| **Compliance Impact** | SOC 2 (CC6.1), HIPAA (164.312(a)(1)) |

---

## 3. Executive Summary

### Problem

Secrets table in `05-usp-schema.sql` has no Row-Level Security (RLS) policies. Users could potentially query each other's secrets at the database level.

### Impact

- **Multi-User Isolation Breach:** Users can access each other's encrypted secrets
- **Defense-in-Depth Missing:** Application-level access control, but no database-level protection
- **Compliance Risk:** HIPAA requires access controls at all layers

### Solution

Enable PostgreSQL Row-Level Security on secrets table with policies enforcing namespace/user isolation.

---

## 4. Implementation Guide

### Step 1: Create RLS Migration (1 hour)

```sql
-- migrations/sql/09-enable-rls-secrets.sql

BEGIN;

-- Enable RLS on secrets table
ALTER TABLE usp.secrets ENABLE ROW LEVEL SECURITY;

-- Policy 1: Users can only see secrets in their namespace
CREATE POLICY secrets_namespace_isolation ON usp.secrets
    FOR SELECT
    USING (
        namespace_id IN (
            SELECT namespace_id
            FROM usp.user_namespaces
            WHERE user_id = current_setting('app.current_user_id')::UUID
        )
    );

-- Policy 2: Users can only insert secrets in their namespace
CREATE POLICY secrets_insert_policy ON usp.secrets
    FOR INSERT
    WITH CHECK (
        namespace_id IN (
            SELECT namespace_id
            FROM usp.user_namespaces
            WHERE user_id = current_setting('app.current_user_id')::UUID
        )
    );

-- Policy 3: Users can only update their own secrets
CREATE POLICY secrets_update_policy ON usp.secrets
    FOR UPDATE
    USING (
        namespace_id IN (
            SELECT namespace_id
            FROM usp.user_namespaces
            WHERE user_id = current_setting('app.current_user_id')::UUID
        )
    );

-- Policy 4: Users can only delete their own secrets
CREATE POLICY secrets_delete_policy ON usp.secrets
    FOR DELETE
    USING (
        namespace_id IN (
            SELECT namespace_id
            FROM usp.user_namespaces
            WHERE user_id = current_setting('app.current_user_id')::UUID
        )
    );

COMMIT;
```

### Step 2: Update DbContext to Set User Context (1 hour)

```csharp
// USPDbContext.cs

public class USPDbContext : DbContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public USPDbContext(DbContextOptions<USPDbContext> options, IHttpContextAccessor httpContextAccessor)
        : base(options)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Set current user ID for RLS policies
        var userId = _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userId))
        {
            await Database.ExecuteSqlRawAsync(
                $"SET app.current_user_id = '{userId}'",
                cancellationToken
            );
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}
```

### Step 3: Apply Migration (30 minutes)

```bash
cd /home/tshepo/projects/tw/services/usp

# Apply RLS migration
psql -h localhost -U postgres -d usp_dev -f migrations/sql/09-enable-rls-secrets.sql

# Verify RLS enabled
psql -h localhost -U postgres -d usp_dev -c "SELECT tablename, rowsecurity FROM pg_tables WHERE tablename = 'secrets';"
# Expected: rowsecurity = true
```

### Step 4: Test RLS Policies (1.5 hours)

```sql
-- Test as user1
SET app.current_user_id = 'user1-uuid';
SELECT * FROM usp.secrets;  -- Should only see user1's secrets

-- Test as user2
SET app.current_user_id = 'user2-uuid';
SELECT * FROM usp.secrets;  -- Should only see user2's secrets

-- Test cross-user access (should be empty)
SET app.current_user_id = 'user1-uuid';
SELECT * FROM usp.secrets WHERE created_by = 'user2-uuid';  -- Expected: 0 rows
```

---

## 5. Testing

- [ ] RLS enabled on secrets table
- [ ] Users can only see secrets in their namespace
- [ ] Users cannot insert secrets in other namespaces
- [ ] Users cannot update other users' secrets
- [ ] Users cannot delete other users' secrets
- [ ] Application still works correctly with RLS

---

## 6. Compliance Evidence

**SOC 2 CC6.1:** Database-level access controls enforced
**HIPAA 164.312(a)(1):** Access controls at data layer

---

## 7. Sign-Off

- [ ] **Database Engineer:** RLS policies implemented
- [ ] **Backend Engineer:** Application tested with RLS
- [ ] **Security:** Multi-user isolation verified

---

**Finding Status:** Not Started
**Last Updated:** 2025-12-27

---

**End of SEC-P1-009**
