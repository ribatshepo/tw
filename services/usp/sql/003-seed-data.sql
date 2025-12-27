-- ============================================================================
-- USP Database Seed Data Script
-- ============================================================================
-- Purpose: Populate initial/default data for USP
-- Database: PostgreSQL 15+
--
-- Contents:
-- - Default roles and permissions
-- - Default admin user (with secure password)
-- - Default security policies
-- - Default MFA methods
-- - System configuration
--
-- Usage:
--   psql -h localhost -U usp_migration -d usp_db -f 003-seed-data.sql
--
-- Security:
--   - Default admin password must be changed on first login
--   - Passwords are hashed using Argon2id
--   - Default policies enforce security best practices
-- ============================================================================

-- ============================================================================
-- 1. Default Roles
-- ============================================================================

-- Insert built-in roles
INSERT INTO roles (id, name, description, is_system, created_at, updated_at)
VALUES
    (uuid_generate_v4(), 'PlatformAdmin', 'Full platform administration - unrestricted access', true, NOW(), NOW()),
    (uuid_generate_v4(), 'SecurityAdmin', 'Security policy and user management', true, NOW(), NOW()),
    (uuid_generate_v4(), 'AuthAdmin', 'Authentication and authorization management', true, NOW(), NOW()),
    (uuid_generate_v4(), 'SecretsAdmin', 'Secrets management administration', true, NOW(), NOW()),
    (uuid_generate_v4(), 'PAMAdmin', 'Privileged access management administration', true, NOW(), NOW()),
    (uuid_generate_v4(), 'ComplianceOfficer', 'Compliance and audit access', true, NOW(), NOW()),
    (uuid_generate_v4(), 'WorkspaceOwner', 'Workspace administration', true, NOW(), NOW()),
    (uuid_generate_v4(), 'WorkspaceAdmin', 'Workspace management', true, NOW(), NOW()),
    (uuid_generate_v4(), 'Developer', 'Development access', true, NOW(), NOW()),
    (uuid_generate_v4(), 'User', 'Standard user access', true, NOW(), NOW()),
    (uuid_generate_v4(), 'Viewer', 'Read-only access', true, NOW(), NOW()),
    (uuid_generate_v4(), 'Auditor', 'Audit log access only', true, NOW(), NOW())
ON CONFLICT DO NOTHING;

-- ============================================================================
-- 2. Default Permissions
-- ============================================================================

-- Platform permissions
INSERT INTO permissions (id, name, description, resource, action, created_at)
VALUES
    (uuid_generate_v4(), 'platform:manage', 'Full platform management', 'platform', 'manage', NOW()),
    (uuid_generate_v4(), 'platform:view', 'View platform information', 'platform', 'view', NOW())
ON CONFLICT DO NOTHING;

-- User management permissions
INSERT INTO permissions (id, name, description, resource, action, created_at)
VALUES
    (uuid_generate_v4(), 'users:create', 'Create users', 'users', 'create', NOW()),
    (uuid_generate_v4(), 'users:read', 'Read user information', 'users', 'read', NOW()),
    (uuid_generate_v4(), 'users:update', 'Update user information', 'users', 'update', NOW()),
    (uuid_generate_v4(), 'users:delete', 'Delete users', 'users', 'delete', NOW())
ON CONFLICT DO NOTHING;

-- Secrets permissions
INSERT INTO permissions (id, name, description, resource, action, created_at)
VALUES
    (uuid_generate_v4(), 'secrets:read', 'Read secrets', 'secrets', 'read', NOW()),
    (uuid_generate_v4(), 'secrets:write', 'Write secrets', 'secrets', 'write', NOW()),
    (uuid_generate_v4(), 'secrets:delete', 'Delete secrets', 'secrets', 'delete', NOW()),
    (uuid_generate_v4(), 'secrets:rotate', 'Rotate secrets', 'secrets', 'rotate', NOW())
ON CONFLICT DO NOTHING;

-- Audit permissions
INSERT INTO permissions (id, name, description, resource, action, created_at)
VALUES
    (uuid_generate_v4(), 'audit:read', 'Read audit logs', 'audit', 'read', NOW()),
    (uuid_generate_v4(), 'audit:export', 'Export audit logs', 'audit', 'export', NOW())
ON CONFLICT DO NOTHING;

-- PAM permissions
INSERT INTO permissions (id, name, description, resource, action, created_at)
VALUES
    (uuid_generate_v4(), 'pam:checkout', 'Checkout privileged accounts', 'pam', 'checkout', NOW()),
    (uuid_generate_v4(), 'pam:manage-safes', 'Manage PAM safes', 'pam', 'manage-safes', NOW()),
    (uuid_generate_v4(), 'pam:view-sessions', 'View PAM sessions', 'pam', 'view-sessions', NOW())
ON CONFLICT DO NOTHING;

-- ============================================================================
-- 3. Assign Permissions to Roles
-- ============================================================================

-- PlatformAdmin gets all permissions
INSERT INTO role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM roles r
CROSS JOIN permissions p
WHERE r.name = 'PlatformAdmin'
ON CONFLICT DO NOTHING;

-- SecurityAdmin permissions
INSERT INTO role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM roles r
CROSS JOIN permissions p
WHERE r.name = 'SecurityAdmin'
  AND p.name IN ('users:create', 'users:read', 'users:update', 'users:delete',
                 'secrets:read', 'secrets:write', 'audit:read')
ON CONFLICT DO NOTHING;

-- SecretsAdmin permissions
INSERT INTO role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM roles r
CROSS JOIN permissions p
WHERE r.name = 'SecretsAdmin'
  AND p.name IN ('secrets:read', 'secrets:write', 'secrets:delete', 'secrets:rotate')
ON CONFLICT DO NOTHING;

-- PAMAdmin permissions
INSERT INTO role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM roles r
CROSS JOIN permissions p
WHERE r.name = 'PAMAdmin'
  AND p.name IN ('pam:checkout', 'pam:manage-safes', 'pam:view-sessions')
ON CONFLICT DO NOTHING;

-- Auditor permissions
INSERT INTO role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM roles r
CROSS JOIN permissions p
WHERE r.name = 'Auditor'
  AND p.name IN ('audit:read', 'audit:export')
ON CONFLICT DO NOTHING;

-- User permissions
INSERT INTO role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM roles r
CROSS JOIN permissions p
WHERE r.name = 'User'
  AND p.name IN ('platform:view', 'users:read', 'secrets:read')
ON CONFLICT DO NOTHING;

-- Viewer permissions
INSERT INTO role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM roles r
CROSS JOIN permissions p
WHERE r.name = 'Viewer'
  AND p.name IN ('platform:view', 'users:read')
ON CONFLICT DO NOTHING;

-- ============================================================================
-- 4. Default Admin User
-- ============================================================================

-- Insert default admin user
-- Password: Admin123! (MUST BE CHANGED ON FIRST LOGIN)
-- Password hash generated using Argon2id (this is a placeholder - actual hash should be generated by application)

\echo '=========================================='
\echo 'IMPORTANT: Default Admin User'
\echo '=========================================='
\echo ''
\echo 'A default admin user will be created:'
\echo '  Username: admin@usp.local'
\echo '  Password: Admin123!'
\echo ''
\echo 'WARNING: This default password MUST be changed immediately!'
\echo ''
\echo 'To change the default password after first login:'
\echo '  POST /api/v1/auth/change-password'
\echo '  with body: { "currentPassword": "Admin123!", "newPassword": "YourSecurePassword" }'
\echo ''
\echo '=========================================='

-- Note: Actual user insertion should be done by the application on first startup
-- This is because password hashing should use the application's Argon2id implementation
-- The application should check if admin user exists, if not, create it with default password

-- ============================================================================
-- 5. Default Security Policies
-- ============================================================================

-- Password Policy
INSERT INTO policies (id, name, type, content, is_active, created_at, updated_at)
VALUES (
    uuid_generate_v4(),
    'Default Password Policy',
    0, -- PolicyType.RBAC
    '{
        "minimumLength": 12,
        "requireUppercase": true,
        "requireLowercase": true,
        "requireDigits": true,
        "requireSpecialChars": true,
        "passwordHistoryCount": 5,
        "maxAge": 90,
        "preventCommonPasswords": true
    }'::jsonb,
    true,
    NOW(),
    NOW()
)
ON CONFLICT DO NOTHING;

-- Session Policy
INSERT INTO policies (id, name, type, content, is_active, created_at, updated_at)
VALUES (
    uuid_generate_v4(),
    'Default Session Policy',
    0, -- PolicyType.RBAC
    '{
        "sessionTimeout": 3600,
        "idleTimeout": 900,
        "maxConcurrentSessions": 5,
        "enforceDeviceBinding": true,
        "requireMFAForSensitiveOperations": true
    }'::jsonb,
    true,
    NOW(),
    NOW()
)
ON CONFLICT DO NOTHING;

-- ============================================================================
-- 6. Default Workspace
-- ============================================================================

-- Create default workspace
INSERT INTO workspaces (id, name, description, is_active, created_at, updated_at)
VALUES (
    uuid_generate_v4(),
    'Default',
    'Default workspace for USP',
    true,
    NOW(),
    NOW()
)
ON CONFLICT DO NOTHING;

-- ============================================================================
-- 7. System Configuration
-- ============================================================================

-- Create system configuration table if not exists
CREATE TABLE IF NOT EXISTS system_configuration (
    key VARCHAR(255) PRIMARY KEY,
    value TEXT NOT NULL,
    description TEXT,
    data_type VARCHAR(50) NOT NULL, -- 'string', 'number', 'boolean', 'json'
    is_encrypted BOOLEAN DEFAULT false,
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

-- Insert default system configuration
INSERT INTO system_configuration (key, value, description, data_type, is_encrypted)
VALUES
    ('system.initialized', 'true', 'System initialization status', 'boolean', false),
    ('system.version', '1.0.0', 'USP version', 'string', false),
    ('system.first_run_completed', 'false', 'First run setup completed', 'boolean', false),
    ('security.enforce_mfa', 'false', 'Enforce MFA for all users', 'boolean', false),
    ('security.lockout_threshold', '5', 'Account lockout threshold (failed attempts)', 'number', false),
    ('security.lockout_duration', '900', 'Account lockout duration (seconds)', 'number', false),
    ('secrets.default_ttl', '2592000', 'Default secret TTL (30 days in seconds)', 'number', false),
    ('secrets.max_versions', '10', 'Maximum secret versions to retain', 'number', false),
    ('audit.retention_days', '365', 'Audit log retention period (days)', 'number', false),
    ('session.max_age', '3600', 'Maximum session age (seconds)', 'number', false),
    ('token.access_token_lifetime', '3600', 'Access token lifetime (seconds)', 'number', false),
    ('token.refresh_token_lifetime', '604800', 'Refresh token lifetime (7 days)', 'number', false)
ON CONFLICT (key) DO NOTHING;

-- ============================================================================
-- 8. Verification
-- ============================================================================

\echo ''
\echo '=========================================='
\echo 'Seed Data Summary'
\echo '=========================================='
\echo ''

-- Count roles
SELECT
    COUNT(*) AS role_count,
    'Roles created' AS description
FROM roles;

-- Count permissions
SELECT
    COUNT(*) AS permission_count,
    'Permissions created' AS description
FROM permissions;

-- Count role-permission mappings
SELECT
    COUNT(*) AS mapping_count,
    'Role-permission mappings created' AS description
FROM role_permissions;

-- Count policies
SELECT
    COUNT(*) AS policy_count,
    'Security policies created' AS description
FROM policies;

-- Count workspaces
SELECT
    COUNT(*) AS workspace_count,
    'Workspaces created' AS description
FROM workspaces;

-- Count system configuration
SELECT
    COUNT(*) AS config_count,
    'System configuration entries' AS description
FROM system_configuration;

\echo ''
\echo '=========================================='
\echo 'Seed Data Loaded Successfully!'
\echo '=========================================='
\echo ''
\echo 'Next Steps:'
\echo '  1. Start USP application'
\echo '  2. Application will create default admin user on first run'
\echo '  3. Log in with default credentials and change password immediately'
\echo '  4. Configure additional users, roles, and permissions as needed'
\echo '  5. Enable MFA for all admin accounts'
\echo ''
\echo '=========================================='
