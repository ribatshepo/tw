-- USP Database Initialization Script
-- This script runs automatically when the PostgreSQL container starts for the first time

-- Enable required extensions
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pgcrypto";

-- Create additional schemas if needed
-- (The main schema is created by EF Core migrations)

-- Grant permissions to USP user
GRANT ALL PRIVILEGES ON DATABASE usp_db TO usp_user;

-- Log initialization
DO $$
BEGIN
    RAISE NOTICE 'USP database initialized successfully';
END $$;
