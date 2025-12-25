-- ================================================================================================
-- GBMM Platform - User Roles Creation
-- ================================================================================================
-- Creates database users and roles for each service
-- Follows principle of least privilege
-- ================================================================================================

-- Create service-specific users
DO $$
BEGIN
    -- UCCP User
    IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = 'uccp_user') THEN
        CREATE ROLE uccp_user WITH LOGIN PASSWORD 'uccp_dev_password_change_me';
    END IF;

    -- NCCS User
    IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = 'nccs_user') THEN
        CREATE ROLE nccs_user WITH LOGIN PASSWORD 'nccs_dev_password_change_me';
    END IF;

    -- USP User
    IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = 'usp_user') THEN
        CREATE ROLE usp_user WITH LOGIN PASSWORD 'usp_dev_password_change_me';
    END IF;

    -- UDPS User
    IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = 'udps_user') THEN
        CREATE ROLE udps_user WITH LOGIN PASSWORD 'udps_dev_password_change_me';
    END IF;

    -- Stream User
    IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = 'stream_user') THEN
        CREATE ROLE stream_user WITH LOGIN PASSWORD 'stream_dev_password_change_me';
    END IF;
END
$$;

-- Grant database-level permissions
GRANT CONNECT ON DATABASE uccp_db TO uccp_user;
GRANT CONNECT ON DATABASE nccs_db TO nccs_user;
GRANT CONNECT ON DATABASE usp_db TO usp_user;
GRANT CONNECT ON DATABASE udps_db TO udps_user;
GRANT CONNECT ON DATABASE stream_db TO stream_user;

\echo 'User roles created successfully'
