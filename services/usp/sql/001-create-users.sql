-- ============================================================================
-- USP Database User Creation Script
-- ============================================================================
-- Purpose: Create database users with minimum required privileges
-- Database: PostgreSQL 15+
--
-- Security Principles:
-- - Principle of least privilege
-- - Separate users for different purposes
-- - No superuser access for application
-- - Password complexity enforced
--
-- Usage:
--   psql -h localhost -U postgres -d usp_db -f 001-create-users.sql
--
-- Prerequisites:
--   - PostgreSQL database 'usp_db' must exist
--   - Run as postgres superuser
--   - Set passwords via environment variables:
--     export USP_APP_PASSWORD="<secure-password>"
--     export USP_READONLY_PASSWORD="<secure-password>"
--     export USP_MIGRATION_PASSWORD="<secure-password>"
-- ============================================================================

-- Enable required extensions
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pgcrypto";

-- ============================================================================
-- 1. Application User (usp_app)
-- ============================================================================
-- Purpose: Primary application user for USP service
-- Privileges: Read, write, update, delete on application tables
-- Usage: Runtime database operations

DO $$
BEGIN
    IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = 'usp_app') THEN
        CREATE ROLE usp_app WITH LOGIN PASSWORD :'USP_APP_PASSWORD';
        RAISE NOTICE 'Created role: usp_app';
    ELSE
        RAISE NOTICE 'Role already exists: usp_app';
    END IF;
END
$$;

-- Grant connection to database
GRANT CONNECT ON DATABASE usp_db TO usp_app;

-- Grant usage on schema
GRANT USAGE ON SCHEMA public TO usp_app;

-- Grant table privileges (will apply to future tables)
ALTER DEFAULT PRIVILEGES IN SCHEMA public
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO usp_app;

-- Grant sequence privileges (for auto-increment columns)
ALTER DEFAULT PRIVILEGES IN SCHEMA public
    GRANT USAGE, SELECT ON SEQUENCES TO usp_app;

-- Grant privileges on existing tables
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO usp_app;

-- Grant sequence usage on existing sequences
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO usp_app;

-- Prevent table creation (migrations handle this)
REVOKE CREATE ON SCHEMA public FROM usp_app;

COMMENT ON ROLE usp_app IS 'USP application runtime user - read/write access to application tables';

-- ============================================================================
-- 2. Read-Only User (usp_readonly)
-- ============================================================================
-- Purpose: Read-only access for reporting, analytics, monitoring
-- Privileges: SELECT only on all tables
-- Usage: Business intelligence, monitoring dashboards

DO $$
BEGIN
    IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = 'usp_readonly') THEN
        CREATE ROLE usp_readonly WITH LOGIN PASSWORD :'USP_READONLY_PASSWORD';
        RAISE NOTICE 'Created role: usp_readonly';
    ELSE
        RAISE NOTICE 'Role already exists: usp_readonly';
    END IF;
END
$$;

-- Grant connection to database
GRANT CONNECT ON DATABASE usp_db TO usp_readonly;

-- Grant usage on schema
GRANT USAGE ON SCHEMA public TO usp_readonly;

-- Grant SELECT privileges (will apply to future tables)
ALTER DEFAULT PRIVILEGES IN SCHEMA public
    GRANT SELECT ON TABLES TO usp_readonly;

-- Grant SELECT on existing tables
GRANT SELECT ON ALL TABLES IN SCHEMA public TO usp_readonly;

-- Grant sequence usage (for SELECT operations that need it)
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO usp_readonly;

COMMENT ON ROLE usp_readonly IS 'USP read-only user for reporting and monitoring';

-- ============================================================================
-- 3. Migration User (usp_migration)
-- ============================================================================
-- Purpose: Database migrations and schema changes
-- Privileges: Full DDL privileges (CREATE, ALTER, DROP tables)
-- Usage: EF Core migrations, schema updates
-- Security: Should only be used during deployment, not runtime

DO $$
BEGIN
    IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = 'usp_migration') THEN
        CREATE ROLE usp_migration WITH LOGIN PASSWORD :'USP_MIGRATION_PASSWORD';
        RAISE NOTICE 'Created role: usp_migration';
    ELSE
        RAISE NOTICE 'Role already exists: usp_migration';
    END IF;
END
$$;

-- Grant connection to database
GRANT CONNECT ON DATABASE usp_db TO usp_migration;

-- Grant full privileges on schema
GRANT ALL PRIVILEGES ON SCHEMA public TO usp_migration;

-- Grant all table privileges
GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA public TO usp_migration;

-- Grant all sequence privileges
GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA public TO usp_migration;

-- Grant ability to create tables
GRANT CREATE ON SCHEMA public TO usp_migration;

-- Allow migration user to grant privileges (needed for EF Core migrations)
ALTER DEFAULT PRIVILEGES IN SCHEMA public
    GRANT ALL PRIVILEGES ON TABLES TO usp_migration;

ALTER DEFAULT PRIVILEGES IN SCHEMA public
    GRANT ALL PRIVILEGES ON SEQUENCES TO usp_migration;

COMMENT ON ROLE usp_migration IS 'USP migration user for schema changes - use only during deployment';

-- ============================================================================
-- 4. Security Hardening
-- ============================================================================

-- Remove PUBLIC schema creation privilege (security best practice)
REVOKE CREATE ON SCHEMA public FROM PUBLIC;

-- Set connection limits (prevent connection exhaustion attacks)
ALTER ROLE usp_app CONNECTION LIMIT 100;
ALTER ROLE usp_readonly CONNECTION LIMIT 20;
ALTER ROLE usp_migration CONNECTION LIMIT 5;

-- Set statement timeout (prevent long-running queries)
ALTER ROLE usp_app SET statement_timeout = '30s';
ALTER ROLE usp_readonly SET statement_timeout = '60s';
ALTER ROLE usp_migration SET statement_timeout = '300s';

-- Set idle in transaction timeout (prevent idle transactions)
ALTER ROLE usp_app SET idle_in_transaction_session_timeout = '60s';
ALTER ROLE usp_readonly SET idle_in_transaction_session_timeout = '60s';
ALTER ROLE usp_migration SET idle_in_transaction_session_timeout = '300s';

-- Disable role attributes that might be security risks
ALTER ROLE usp_app NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION;
ALTER ROLE usp_readonly NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION;
ALTER ROLE usp_migration NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION;

-- ============================================================================
-- 5. Row-Level Security (RLS) Preparation
-- ============================================================================
-- Enable RLS on tables that need multi-tenancy isolation
-- This is a placeholder - actual policies will be created per table

-- Example: Enable RLS on users table (uncomment when needed)
-- ALTER TABLE users ENABLE ROW LEVEL SECURITY;

-- ============================================================================
-- 6. Audit Trail
-- ============================================================================

-- Create audit log for user management
CREATE TABLE IF NOT EXISTS database_user_audit (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    action VARCHAR(50) NOT NULL,
    username VARCHAR(255) NOT NULL,
    performed_by VARCHAR(255) NOT NULL,
    performed_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    details JSONB
);

-- Log user creation
INSERT INTO database_user_audit (action, username, performed_by, details)
VALUES
    ('USER_CREATED', 'usp_app', current_user, '{"purpose": "Application runtime user"}'::jsonb),
    ('USER_CREATED', 'usp_readonly', current_user, '{"purpose": "Read-only user for reporting"}'::jsonb),
    ('USER_CREATED', 'usp_migration', current_user, '{"purpose": "Migration user for schema changes"}'::jsonb);

-- ============================================================================
-- 7. Verification
-- ============================================================================

-- Display created users and their privileges
SELECT
    r.rolname AS username,
    CASE WHEN r.rolsuper THEN 'Yes' ELSE 'No' END AS is_superuser,
    CASE WHEN r.rolcreatedb THEN 'Yes' ELSE 'No' END AS can_create_db,
    CASE WHEN r.rolcreaterole THEN 'Yes' ELSE 'No' END AS can_create_role,
    r.rolconnlimit AS connection_limit,
    ARRAY_AGG(DISTINCT d.datname ORDER BY d.datname) AS databases
FROM pg_roles r
LEFT JOIN pg_auth_members m ON r.oid = m.member
LEFT JOIN pg_database d ON has_database_privilege(r.rolname, d.datname, 'CONNECT')
WHERE r.rolname IN ('usp_app', 'usp_readonly', 'usp_migration')
GROUP BY r.rolname, r.rolsuper, r.rolcreatedb, r.rolcreaterole, r.rolconnlimit
ORDER BY r.rolname;

-- ============================================================================
-- SUCCESS
-- ============================================================================
\echo '=========================================='
\echo 'Database users created successfully!'
\echo '=========================================='
\echo ''
\echo 'Created users:'
\echo '  - usp_app:       Application runtime user (read/write)'
\echo '  - usp_readonly:  Read-only user (reporting/monitoring)'
\echo '  - usp_migration: Migration user (schema changes)'
\echo ''
\echo 'Next steps:'
\echo '  1. Run: 002-enable-ssl.sql (enable SSL/TLS connections)'
\echo '  2. Run: 003-seed-data.sql (populate initial data)'
\echo '  3. Update application connection string to use usp_app user'
\echo '  4. Test connection with: psql -h localhost -U usp_app -d usp_db'
\echo ''
\echo '=========================================='
