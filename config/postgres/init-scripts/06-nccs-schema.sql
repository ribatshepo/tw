-- ================================================================================================
-- NCCS (.NET Compute Client Service) - Database Schema
-- ================================================================================================
-- Schema for NCCS service including:
-- - Client configurations
-- - API keys
-- - Request tracking
-- - SignalR connections
-- ================================================================================================

\c nccs_db;

-- Set search path
SET search_path TO public;

-- ==============================================================================================
-- CLIENT CONFIGURATIONS
-- ==============================================================================================

CREATE TABLE IF NOT EXISTS client_configs (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    client_id VARCHAR(255) NOT NULL UNIQUE,
    client_name VARCHAR(255) NOT NULL,
    namespace VARCHAR(255) NOT NULL,
    uccp_endpoint VARCHAR(500) NOT NULL,
    cache_enabled BOOLEAN DEFAULT TRUE,
    cache_ttl_seconds INTEGER DEFAULT 300,
    retry_policy JSONB,
    timeout_seconds INTEGER DEFAULT 30,
    metadata JSONB,
    status VARCHAR(50) NOT NULL DEFAULT 'active',
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    CONSTRAINT check_status CHECK (status IN ('active', 'inactive', 'suspended'))
);

CREATE INDEX IF NOT EXISTS idx_client_configs_client_id ON client_configs(client_id);
CREATE INDEX IF NOT EXISTS idx_client_configs_namespace ON client_configs(namespace);
CREATE INDEX IF NOT EXISTS idx_client_configs_status ON client_configs(status);

-- ==============================================================================================
-- API KEYS
-- ==============================================================================================

CREATE TABLE IF NOT EXISTS api_keys (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    client_id VARCHAR(255) NOT NULL REFERENCES client_configs(client_id) ON DELETE CASCADE,
    key_hash VARCHAR(255) NOT NULL UNIQUE,
    key_prefix VARCHAR(20) NOT NULL,
    description TEXT,
    scopes VARCHAR(100)[],
    rate_limit_per_minute INTEGER DEFAULT 1000,
    last_used_at TIMESTAMP WITH TIME ZONE,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    expires_at TIMESTAMP WITH TIME ZONE,
    revoked BOOLEAN DEFAULT FALSE,
    revoked_at TIMESTAMP WITH TIME ZONE
);

CREATE INDEX IF NOT EXISTS idx_api_keys_client_id ON api_keys(client_id);
CREATE INDEX IF NOT EXISTS idx_api_keys_key_hash ON api_keys(key_hash);
CREATE INDEX IF NOT EXISTS idx_api_keys_revoked ON api_keys(revoked);

-- ==============================================================================================
-- REQUEST TRACKING
-- ==============================================================================================

CREATE TABLE IF NOT EXISTS request_log (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    client_id VARCHAR(255) REFERENCES client_configs(client_id),
    request_id VARCHAR(100) NOT NULL,
    method VARCHAR(50) NOT NULL,
    endpoint VARCHAR(500) NOT NULL,
    status_code INTEGER,
    duration_ms INTEGER,
    request_size_bytes BIGINT,
    response_size_bytes BIGINT,
    ip_address INET,
    user_agent TEXT,
    error_message TEXT,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_request_log_client_id ON request_log(client_id);
CREATE INDEX IF NOT EXISTS idx_request_log_request_id ON request_log(request_id);
CREATE INDEX IF NOT EXISTS idx_request_log_created_at ON request_log(created_at);
CREATE INDEX IF NOT EXISTS idx_request_log_status_code ON request_log(status_code);

-- ==============================================================================================
-- SIGNALR CONNECTIONS
-- ==============================================================================================

CREATE TABLE IF NOT EXISTS signalr_connections (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    connection_id VARCHAR(255) NOT NULL UNIQUE,
    client_id VARCHAR(255) REFERENCES client_configs(client_id),
    user_id UUID,
    hub_name VARCHAR(255) NOT NULL,
    connected_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    last_activity TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    disconnected_at TIMESTAMP WITH TIME ZONE,
    metadata JSONB
);

CREATE INDEX IF NOT EXISTS idx_signalr_connections_connection_id ON signalr_connections(connection_id);
CREATE INDEX IF NOT EXISTS idx_signalr_connections_client_id ON signalr_connections(client_id);
CREATE INDEX IF NOT EXISTS idx_signalr_connections_hub_name ON signalr_connections(hub_name);
CREATE INDEX IF NOT EXISTS idx_signalr_connections_connected_at ON signalr_connections(connected_at);

-- ==============================================================================================
-- CACHE METADATA
-- ==============================================================================================

CREATE TABLE IF NOT EXISTS cache_entries (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    cache_key VARCHAR(500) NOT NULL UNIQUE,
    client_id VARCHAR(255) REFERENCES client_configs(client_id),
    data_size_bytes INTEGER,
    hit_count INTEGER DEFAULT 0,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    expires_at TIMESTAMP WITH TIME ZONE NOT NULL,
    last_accessed_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_cache_entries_cache_key ON cache_entries(cache_key);
CREATE INDEX IF NOT EXISTS idx_cache_entries_expires_at ON cache_entries(expires_at);
CREATE INDEX IF NOT EXISTS idx_cache_entries_client_id ON cache_entries(client_id);

-- ==============================================================================================
-- PERMISSIONS
-- ==============================================================================================

-- Grant schema permissions to nccs_user
GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA public TO nccs_user;
GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA public TO nccs_user;

\echo 'NCCS schema created successfully'
