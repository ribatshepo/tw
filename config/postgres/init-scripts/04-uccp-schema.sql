-- ================================================================================================
-- UCCP (Unified Compute & Coordination Platform) - Database Schema
-- ================================================================================================
-- Schema for UCCP service including:
-- - Service registry
-- - Task scheduling
-- - Distributed locking
-- - ML models and feature store
-- - Raft cluster state
-- ================================================================================================

\c uccp_db;

-- Set search path
SET search_path TO public;

-- ==============================================================================================
-- SERVICE REGISTRY
-- ==============================================================================================

CREATE TABLE IF NOT EXISTS services (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    name VARCHAR(255) NOT NULL,
    version VARCHAR(50) NOT NULL,
    host VARCHAR(255) NOT NULL,
    port INTEGER NOT NULL,
    protocol VARCHAR(50) NOT NULL DEFAULT 'grpc',
    health_endpoint VARCHAR(500),
    metadata JSONB,
    status VARCHAR(50) NOT NULL DEFAULT 'active',
    registered_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    last_heartbeat TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    CONSTRAINT unique_service UNIQUE (name, host, port)
);

CREATE INDEX IF NOT EXISTS idx_services_name ON services(name);
CREATE INDEX IF NOT EXISTS idx_services_status ON services(status);
CREATE INDEX IF NOT EXISTS idx_services_heartbeat ON services(last_heartbeat);

-- ==============================================================================================
-- TASK SCHEDULING
-- ==============================================================================================

CREATE TABLE IF NOT EXISTS tasks (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    namespace VARCHAR(255) NOT NULL,
    name VARCHAR(255) NOT NULL,
    type VARCHAR(100) NOT NULL,
    priority INTEGER NOT NULL DEFAULT 0,
    status VARCHAR(50) NOT NULL DEFAULT 'pending',
    payload JSONB NOT NULL,
    result JSONB,
    error TEXT,
    assigned_node VARCHAR(255),
    gpu_required BOOLEAN DEFAULT FALSE,
    cpu_cores INTEGER,
    memory_mb INTEGER,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    started_at TIMESTAMP WITH TIME ZONE,
    completed_at TIMESTAMP WITH TIME ZONE,
    CONSTRAINT check_status CHECK (status IN ('pending', 'running', 'completed', 'failed', 'cancelled'))
);

CREATE INDEX IF NOT EXISTS idx_tasks_namespace ON tasks(namespace);
CREATE INDEX IF NOT EXISTS idx_tasks_status ON tasks(status);
CREATE INDEX IF NOT EXISTS idx_tasks_priority ON tasks(priority DESC);
CREATE INDEX IF NOT EXISTS idx_tasks_created_at ON tasks(created_at);

-- ==============================================================================================
-- DISTRIBUTED LOCKING
-- ==============================================================================================

CREATE TABLE IF NOT EXISTS locks (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    name VARCHAR(255) NOT NULL UNIQUE,
    owner_id VARCHAR(255) NOT NULL,
    acquired_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    expires_at TIMESTAMP WITH TIME ZONE NOT NULL,
    lease_duration_seconds INTEGER NOT NULL,
    metadata JSONB
);

CREATE INDEX IF NOT EXISTS idx_locks_name ON locks(name);
CREATE INDEX IF NOT EXISTS idx_locks_expires_at ON locks(expires_at);

-- ==============================================================================================
-- ML MODELS
-- ==============================================================================================

CREATE TABLE IF NOT EXISTS ml_models (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    namespace VARCHAR(255) NOT NULL,
    name VARCHAR(255) NOT NULL,
    version VARCHAR(50) NOT NULL,
    framework VARCHAR(50) NOT NULL,
    model_type VARCHAR(100) NOT NULL,
    artifact_uri VARCHAR(1000) NOT NULL,
    metrics JSONB,
    parameters JSONB,
    status VARCHAR(50) NOT NULL DEFAULT 'registered',
    created_by VARCHAR(255),
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    CONSTRAINT unique_model_version UNIQUE (namespace, name, version),
    CONSTRAINT check_framework CHECK (framework IN ('tensorflow', 'pytorch', 'jax', 'xgboost', 'scikit-learn'))
);

CREATE INDEX IF NOT EXISTS idx_ml_models_namespace ON ml_models(namespace);
CREATE INDEX IF NOT EXISTS idx_ml_models_name ON ml_models(name);
CREATE INDEX IF NOT EXISTS idx_ml_models_status ON ml_models(status);

-- ==============================================================================================
-- FEATURE STORE
-- ==============================================================================================

CREATE TABLE IF NOT EXISTS feature_groups (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    namespace VARCHAR(255) NOT NULL,
    name VARCHAR(255) NOT NULL,
    description TEXT,
    features JSONB NOT NULL,
    primary_keys VARCHAR(255)[],
    event_time_column VARCHAR(255),
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    CONSTRAINT unique_feature_group UNIQUE (namespace, name)
);

CREATE INDEX IF NOT EXISTS idx_feature_groups_namespace ON feature_groups(namespace);
CREATE INDEX IF NOT EXISTS idx_feature_groups_name ON feature_groups(name);

-- ==============================================================================================
-- RAFT CLUSTER STATE
-- ==============================================================================================

CREATE TABLE IF NOT EXISTS raft_nodes (
    id VARCHAR(255) PRIMARY KEY,
    address VARCHAR(500) NOT NULL,
    role VARCHAR(50) NOT NULL DEFAULT 'follower',
    term BIGINT NOT NULL DEFAULT 0,
    voted_for VARCHAR(255),
    last_heartbeat TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    metadata JSONB,
    CONSTRAINT check_role CHECK (role IN ('leader', 'follower', 'candidate'))
);

CREATE INDEX IF NOT EXISTS idx_raft_nodes_role ON raft_nodes(role);
CREATE INDEX IF NOT EXISTS idx_raft_nodes_term ON raft_nodes(term);

-- ==============================================================================================
-- PERMISSIONS
-- ==============================================================================================

-- Grant schema permissions to uccp_user
GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA public TO uccp_user;
GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA public TO uccp_user;

\echo 'UCCP schema created successfully'
