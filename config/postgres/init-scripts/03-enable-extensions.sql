-- ================================================================================================
-- GBMM Platform - PostgreSQL Extensions
-- ================================================================================================
-- Enables required PostgreSQL extensions for all databases
-- ================================================================================================

-- Enable extensions for UCCP database
\c uccp_db;
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pgcrypto";
CREATE EXTENSION IF NOT EXISTS "pg_stat_statements";
\echo 'Extensions enabled for uccp_db'

-- Enable extensions for NCCS database
\c nccs_db;
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pgcrypto";
CREATE EXTENSION IF NOT EXISTS "pg_stat_statements";
\echo 'Extensions enabled for nccs_db'

-- Enable extensions for USP database
\c usp_db;
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pgcrypto";
CREATE EXTENSION IF NOT EXISTS "pg_stat_statements";
\echo 'Extensions enabled for usp_db'

-- Enable extensions for UDPS database
\c udps_db;
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pgcrypto";
CREATE EXTENSION IF NOT EXISTS "pg_stat_statements";
\echo 'Extensions enabled for udps_db'

-- Enable extensions for Stream database
\c stream_db;
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pgcrypto";
CREATE EXTENSION IF NOT EXISTS "pg_stat_statements";
\echo 'Extensions enabled for stream_db'

-- Return to default database
\c postgres;
