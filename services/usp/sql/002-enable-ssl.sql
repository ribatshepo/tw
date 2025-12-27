-- ============================================================================
-- PostgreSQL SSL/TLS Configuration Script
-- ============================================================================
-- Purpose: Enable and enforce SSL/TLS connections to PostgreSQL
-- Database: PostgreSQL 15+
--
-- Security Features:
-- - Enable SSL/TLS for all connections
-- - Enforce SSL for USP application users
-- - Configure certificate-based authentication (optional)
-- - Set minimum TLS version to 1.2
--
-- Prerequisites:
--   - PostgreSQL server must have SSL certificates installed:
--     /var/lib/postgresql/server.crt
--     /var/lib/postgresql/server.key
--     /var/lib/postgresql/root.crt (optional, for client cert validation)
--   - Certificate files must be owned by postgres user
--   - Private key must have permissions 600
--
-- Usage:
--   psql -h localhost -U postgres -d postgres -f 002-enable-ssl.sql
--
-- Certificate Generation (for development):
--   See: scripts/generate-postgres-ssl-certs.sh
--
-- Production Certificates:
--   Use certificates from your organization's Certificate Authority
-- ============================================================================

-- ============================================================================
-- 1. Enable SSL in PostgreSQL Configuration
-- ============================================================================

-- NOTE: These commands modify postgresql.conf and require PostgreSQL reload/restart
-- For production, these settings should be in postgresql.conf directly

-- Enable SSL
ALTER SYSTEM SET ssl = on;

-- Specify SSL certificate file
ALTER SYSTEM SET ssl_cert_file = '/var/lib/postgresql/15/main/server.crt';

-- Specify SSL private key file
ALTER SYSTEM SET ssl_key_file = '/var/lib/postgresql/15/main/server.key';

-- Optional: Specify CA certificate for client certificate validation
-- ALTER SYSTEM SET ssl_ca_file = '/var/lib/postgresql/15/main/root.crt';

-- Set minimum TLS version (TLS 1.2 or higher)
ALTER SYSTEM SET ssl_min_protocol_version = 'TLSv1.2';

-- Preferred SSL ciphers (strong ciphers only)
ALTER SYSTEM SET ssl_ciphers = 'HIGH:MEDIUM:+3DES:!aNULL:!PSK:!SRP:!MD5:!RC4';

-- Prefer server cipher order
ALTER SYSTEM SET ssl_prefer_server_ciphers = on;

-- Optional: Enable SSL compression (usually disabled for security)
ALTER SYSTEM SET ssl_compression = off;

-- ============================================================================
-- 2. Reload PostgreSQL Configuration
-- ============================================================================

-- Reload configuration (applies settings without restart)
SELECT pg_reload_conf();

\echo 'PostgreSQL configuration reloaded. SSL settings applied.'
\echo ''

-- ============================================================================
-- 3. Enforce SSL for Application Users
-- ============================================================================

-- Modify pg_hba.conf to require SSL for USP users
-- NOTE: This requires direct modification of pg_hba.conf file
-- Add these lines to pg_hba.conf (in order, before any 'host' entries):

\echo '=========================================='
\echo 'IMPORTANT: Manual pg_hba.conf Update Required'
\echo '=========================================='
\echo ''
\echo 'Add these lines to /etc/postgresql/15/main/pg_hba.conf:'
\echo ''
\echo '# USP Application Users - Require SSL'
\echo 'hostssl  usp_db    usp_app         0.0.0.0/0          scram-sha-256'
\echo 'hostssl  usp_db    usp_readonly    0.0.0.0/0          scram-sha-256'
\echo 'hostssl  usp_db    usp_migration   127.0.0.1/32       scram-sha-256'
\echo ''
\echo '# Deny non-SSL connections for USP users'
\echo 'host     usp_db    usp_app         0.0.0.0/0          reject'
\echo 'host     usp_db    usp_readonly    0.0.0.0/0          reject'
\echo 'host     usp_db    usp_migration   0.0.0.0/0          reject'
\echo ''
\echo 'After updating pg_hba.conf, reload PostgreSQL:'
\echo '  sudo systemctl reload postgresql'
\echo '  OR'
\echo '  sudo pg_ctlcluster 15 main reload'
\echo ''
\echo '=========================================='
\echo ''

-- ============================================================================
-- 4. Client Certificate Authentication (Optional)
-- ============================================================================

-- For maximum security, enable client certificate authentication
-- This requires clients to present valid SSL certificates

-- Example pg_hba.conf entry for cert-based auth:
-- hostssl  usp_db    usp_app    0.0.0.0/0    cert clientcert=verify-full

\echo 'Optional: Client Certificate Authentication'
\echo '=========================================='
\echo ''
\echo 'For enhanced security, you can require client certificates:'
\echo ''
\echo '1. Generate client certificates for each service'
\echo '2. Update pg_hba.conf to use cert authentication:'
\echo '   hostssl  usp_db  usp_app  0.0.0.0/0  cert clientcert=verify-full'
\echo '3. Update USP connection string to include certificate:'
\echo '   Host=localhost;Port=5432;Database=usp_db;Username=usp_app;'
\echo '   SSL Mode=Require;SSL Cert=/path/to/client.crt;SSL Key=/path/to/client.key;'
\echo '   Root Certificate=/path/to/root.crt'
\echo ''

-- ============================================================================
-- 5. Create SSL Status View
-- ============================================================================

-- Create a view to monitor SSL connections
CREATE OR REPLACE VIEW ssl_connections AS
SELECT
    pid,
    usename,
    application_name,
    client_addr,
    client_port,
    ssl,
    ssl_version,
    ssl_cipher,
    backend_start,
    state
FROM pg_stat_ssl
JOIN pg_stat_activity USING (pid)
WHERE usename IN ('usp_app', 'usp_readonly', 'usp_migration')
ORDER BY backend_start DESC;

COMMENT ON VIEW ssl_connections IS 'Monitor SSL connections for USP database users';

-- Grant SELECT on the view to readonly user
GRANT SELECT ON ssl_connections TO usp_readonly;

-- ============================================================================
-- 6. Create Audit Log for SSL Policy Violations
-- ============================================================================

CREATE TABLE IF NOT EXISTS ssl_policy_violations (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    occurred_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    username VARCHAR(255) NOT NULL,
    client_addr INET,
    connection_type VARCHAR(50) NOT NULL, -- 'SSL' or 'NO_SSL'
    action VARCHAR(50) NOT NULL, -- 'ALLOWED' or 'REJECTED'
    details JSONB
);

COMMENT ON TABLE ssl_policy_violations IS 'Audit log for SSL policy violations';

-- Create index for faster queries
CREATE INDEX IF NOT EXISTS idx_ssl_violations_occurred_at
    ON ssl_policy_violations (occurred_at DESC);

CREATE INDEX IF NOT EXISTS idx_ssl_violations_username
    ON ssl_policy_violations (username);

-- ============================================================================
-- 7. Verification
-- ============================================================================

-- Display current SSL configuration
SELECT
    name,
    setting,
    short_desc
FROM pg_settings
WHERE name LIKE 'ssl%'
ORDER BY name;

\echo ''
\echo '=========================================='
\echo 'SSL Configuration Summary'
\echo '=========================================='
\echo ''
\echo 'Current SSL Settings:'

SELECT
    'ssl = ' || setting AS config_line
FROM pg_settings WHERE name = 'ssl'
UNION ALL
SELECT
    'ssl_cert_file = ' || setting
FROM pg_settings WHERE name = 'ssl_cert_file'
UNION ALL
SELECT
    'ssl_key_file = ' || setting
FROM pg_settings WHERE name = 'ssl_key_file'
UNION ALL
SELECT
    'ssl_min_protocol_version = ' || setting
FROM pg_settings WHERE name = 'ssl_min_protocol_version'
UNION ALL
SELECT
    'ssl_ciphers = ' || setting
FROM pg_settings WHERE name = 'ssl_ciphers';

\echo ''
\echo '=========================================='
\echo 'Next Steps:'
\echo '=========================================='
\echo ''
\echo '1. Update pg_hba.conf with SSL requirements (see above)'
\echo '2. Reload PostgreSQL: sudo systemctl reload postgresql'
\echo '3. Verify SSL certificates exist and have correct permissions:'
\echo '   ls -la /var/lib/postgresql/15/main/server.{crt,key}'
\echo '   (should be owned by postgres:postgres, key should be 600)'
\echo '4. Test SSL connection:'
\echo '   psql "host=localhost port=5432 dbname=usp_db user=usp_app sslmode=require"'
\echo '5. Monitor SSL connections:'
\echo '   SELECT * FROM ssl_connections;'
\echo ''
\echo '=========================================='
\echo 'SSL Configuration Complete!'
\echo '=========================================='
