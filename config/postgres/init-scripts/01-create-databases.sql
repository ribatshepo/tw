-- ================================================================================================
-- GBMM Platform - Database Creation
-- ================================================================================================
-- Creates databases for all services
-- Executed automatically by PostgreSQL on first startup
-- ================================================================================================

-- Create databases for each service
CREATE DATABASE uccp_db;
CREATE DATABASE nccs_db;
CREATE DATABASE usp_db;
CREATE DATABASE udps_db;
CREATE DATABASE stream_db;

-- Grant initial permissions to postgres superuser
GRANT ALL PRIVILEGES ON DATABASE uccp_db TO postgres;
GRANT ALL PRIVILEGES ON DATABASE nccs_db TO postgres;
GRANT ALL PRIVILEGES ON DATABASE usp_db TO postgres;
GRANT ALL PRIVILEGES ON DATABASE udps_db TO postgres;
GRANT ALL PRIVILEGES ON DATABASE stream_db TO postgres;

\echo 'Databases created successfully'
