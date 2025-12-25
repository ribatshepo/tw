-- ================================================================================================
-- Stream Compute Service - Database Schema
-- ================================================================================================
-- Schema for Stream Compute service including:
-- - Stream configurations
-- - Checkpoints and state
-- - Stream processing jobs
-- - Event patterns (CEP)
-- ================================================================================================

\c stream_db;

-- Set search path
SET search_path TO public;

-- ==============================================================================================
-- STREAM CONFIGURATIONS
-- ==============================================================================================

CREATE TABLE IF NOT EXISTS stream_configs (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    name VARCHAR(255) NOT NULL UNIQUE,
    namespace VARCHAR(255) NOT NULL,
    source_type VARCHAR(100) NOT NULL,
    source_config JSONB NOT NULL,
    sink_type VARCHAR(100) NOT NULL,
    sink_config JSONB NOT NULL,
    parallelism INTEGER DEFAULT 1,
    buffer_size INTEGER DEFAULT 10000,
    batch_size INTEGER DEFAULT 1000,
    flush_interval_ms INTEGER DEFAULT 5000,
    status VARCHAR(50) NOT NULL DEFAULT 'created',
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    CONSTRAINT check_source_type CHECK (source_type IN ('kafka', 'rabbitmq', 'http', 'websocket', 'file')),
    CONSTRAINT check_sink_type CHECK (sink_type IN ('kafka', 'rabbitmq', 'database', 's3', 'elasticsearch')),
    CONSTRAINT check_status CHECK (status IN ('created', 'running', 'paused', 'failed', 'stopped'))
);

CREATE INDEX IF NOT EXISTS idx_stream_configs_name ON stream_configs(name);
CREATE INDEX IF NOT EXISTS idx_stream_configs_namespace ON stream_configs(namespace);
CREATE INDEX IF NOT EXISTS idx_stream_configs_status ON stream_configs(status);

-- ==============================================================================================
-- STREAM PROCESSING JOBS
-- ==============================================================================================

CREATE TABLE IF NOT EXISTS processing_jobs (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    stream_config_id UUID REFERENCES stream_configs(id) ON DELETE CASCADE,
    job_id VARCHAR(255) NOT NULL UNIQUE,
    job_name VARCHAR(255) NOT NULL,
    flink_job_id VARCHAR(255),
    processing_type VARCHAR(100) NOT NULL,
    configuration JSONB NOT NULL,
    status VARCHAR(50) NOT NULL DEFAULT 'created',
    parallelism INTEGER DEFAULT 1,
    started_at TIMESTAMP WITH TIME ZONE,
    stopped_at TIMESTAMP WITH TIME ZONE,
    error_message TEXT,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    CONSTRAINT check_processing_type CHECK (processing_type IN ('map', 'filter', 'aggregate', 'join', 'window', 'cep')),
    CONSTRAINT check_job_status CHECK (status IN ('created', 'running', 'completed', 'failed', 'cancelled'))
);

CREATE INDEX IF NOT EXISTS idx_processing_jobs_stream_config_id ON processing_jobs(stream_config_id);
CREATE INDEX IF NOT EXISTS idx_processing_jobs_job_id ON processing_jobs(job_id);
CREATE INDEX IF NOT EXISTS idx_processing_jobs_status ON processing_jobs(status);

-- ==============================================================================================
-- CHECKPOINTS
-- ==============================================================================================

CREATE TABLE IF NOT EXISTS checkpoints (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    stream_config_id UUID REFERENCES stream_configs(id) ON DELETE CASCADE,
    checkpoint_id BIGINT NOT NULL,
    checkpoint_location VARCHAR(1000) NOT NULL,
    state_size_bytes BIGINT,
    duration_ms INTEGER,
    status VARCHAR(50) NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    CONSTRAINT check_checkpoint_status CHECK (status IN ('completed', 'failed', 'expired'))
);

CREATE INDEX IF NOT EXISTS idx_checkpoints_stream_config_id ON checkpoints(stream_config_id);
CREATE INDEX IF NOT EXISTS idx_checkpoints_checkpoint_id ON checkpoints(checkpoint_id);
CREATE INDEX IF NOT EXISTS idx_checkpoints_created_at ON checkpoints(created_at);

-- ==============================================================================================
-- STREAM STATE
-- ==============================================================================================

CREATE TABLE IF NOT EXISTS stream_state (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    stream_config_id UUID REFERENCES stream_configs(id) ON DELETE CASCADE,
    state_key VARCHAR(500) NOT NULL,
    state_value JSONB NOT NULL,
    last_updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    CONSTRAINT unique_stream_state UNIQUE (stream_config_id, state_key)
);

CREATE INDEX IF NOT EXISTS idx_stream_state_stream_config_id ON stream_state(stream_config_id);
CREATE INDEX IF NOT EXISTS idx_stream_state_state_key ON stream_state(state_key);

-- ==============================================================================================
-- METRICS AND MONITORING
-- ==============================================================================================

CREATE TABLE IF NOT EXISTS stream_metrics (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    stream_config_id UUID REFERENCES stream_configs(id) ON DELETE CASCADE,
    metric_name VARCHAR(255) NOT NULL,
    metric_value DOUBLE PRECISION NOT NULL,
    tags JSONB,
    timestamp TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_stream_metrics_stream_config_id ON stream_metrics(stream_config_id);
CREATE INDEX IF NOT EXISTS idx_stream_metrics_metric_name ON stream_metrics(metric_name);
CREATE INDEX IF NOT EXISTS idx_stream_metrics_timestamp ON stream_metrics(timestamp);

-- ==============================================================================================
-- COMPLEX EVENT PROCESSING (CEP) PATTERNS
-- ==============================================================================================

CREATE TABLE IF NOT EXISTS cep_patterns (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    name VARCHAR(255) NOT NULL UNIQUE,
    namespace VARCHAR(255) NOT NULL,
    pattern_definition JSONB NOT NULL,
    time_window_seconds INTEGER NOT NULL,
    enabled BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_cep_patterns_name ON cep_patterns(name);
CREATE INDEX IF NOT EXISTS idx_cep_patterns_namespace ON cep_patterns(namespace);
CREATE INDEX IF NOT EXISTS idx_cep_patterns_enabled ON cep_patterns(enabled);

-- ==============================================================================================
-- CEP PATTERN MATCHES
-- ==============================================================================================

CREATE TABLE IF NOT EXISTS cep_pattern_matches (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    pattern_id UUID REFERENCES cep_patterns(id) ON DELETE CASCADE,
    matched_events JSONB NOT NULL,
    match_timestamp TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    metadata JSONB
);

CREATE INDEX IF NOT EXISTS idx_cep_pattern_matches_pattern_id ON cep_pattern_matches(pattern_id);
CREATE INDEX IF NOT EXISTS idx_cep_pattern_matches_timestamp ON cep_pattern_matches(match_timestamp);

-- ==============================================================================================
-- WINDOW AGGREGATIONS
-- ==============================================================================================

CREATE TABLE IF NOT EXISTS window_aggregations (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    stream_config_id UUID REFERENCES stream_configs(id) ON DELETE CASCADE,
    window_type VARCHAR(50) NOT NULL,
    window_size_seconds INTEGER NOT NULL,
    aggregation_function VARCHAR(100) NOT NULL,
    group_by_keys VARCHAR(255)[],
    result JSONB NOT NULL,
    window_start TIMESTAMP WITH TIME ZONE NOT NULL,
    window_end TIMESTAMP WITH TIME ZONE NOT NULL,
    event_count BIGINT DEFAULT 0,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    CONSTRAINT check_window_type CHECK (window_type IN ('tumbling', 'sliding', 'session')),
    CONSTRAINT check_aggregation CHECK (aggregation_function IN ('count', 'sum', 'avg', 'min', 'max', 'percentile'))
);

CREATE INDEX IF NOT EXISTS idx_window_aggregations_stream_config_id ON window_aggregations(stream_config_id);
CREATE INDEX IF NOT EXISTS idx_window_aggregations_window_start ON window_aggregations(window_start);
CREATE INDEX IF NOT EXISTS idx_window_aggregations_window_end ON window_aggregations(window_end);

-- ==============================================================================================
-- ANOMALY DETECTION
-- ==============================================================================================

CREATE TABLE IF NOT EXISTS anomalies (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    stream_config_id UUID REFERENCES stream_configs(id) ON DELETE CASCADE,
    anomaly_type VARCHAR(100) NOT NULL,
    severity VARCHAR(50) NOT NULL,
    description TEXT,
    detected_value DOUBLE PRECISION,
    expected_value DOUBLE PRECISION,
    threshold DOUBLE PRECISION,
    event_data JSONB,
    detected_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    acknowledged BOOLEAN DEFAULT FALSE,
    acknowledged_at TIMESTAMP WITH TIME ZONE,
    CONSTRAINT check_anomaly_type CHECK (anomaly_type IN ('outlier', 'drift', 'spike', 'pattern_break')),
    CONSTRAINT check_severity CHECK (severity IN ('low', 'medium', 'high', 'critical'))
);

CREATE INDEX IF NOT EXISTS idx_anomalies_stream_config_id ON anomalies(stream_config_id);
CREATE INDEX IF NOT EXISTS idx_anomalies_severity ON anomalies(severity);
CREATE INDEX IF NOT EXISTS idx_anomalies_detected_at ON anomalies(detected_at);
CREATE INDEX IF NOT EXISTS idx_anomalies_acknowledged ON anomalies(acknowledged);

-- ==============================================================================================
-- PERMISSIONS
-- ==============================================================================================

-- Grant schema permissions to stream_user
GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA public TO stream_user;
GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA public TO stream_user;

\echo 'Stream Compute schema created successfully'
