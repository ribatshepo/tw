-- ================================================================================================
-- UDPS (Unified Data Platform Service) - Database Schema
-- ================================================================================================
-- Schema for UDPS service including:
-- - Data catalog (tables, partitions, schemas)
-- - Data lineage tracking
-- - Query cache
-- - Data governance
-- ================================================================================================

\c udps_db;

-- Set search path
SET search_path TO public;

-- Create catalog schema for data catalog
CREATE SCHEMA IF NOT EXISTS catalog;

-- ==============================================================================================
-- DATA CATALOG - TABLES
-- ==============================================================================================

CREATE TABLE IF NOT EXISTS catalog.tables (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    namespace VARCHAR(255) NOT NULL,
    table_name VARCHAR(255) NOT NULL,
    schema_definition JSONB NOT NULL,
    storage_format VARCHAR(50) NOT NULL DEFAULT 'parquet',
    compression_codec VARCHAR(50) DEFAULT 'snappy',
    partition_columns VARCHAR(255)[],
    sort_columns VARCHAR(255)[],
    location VARCHAR(1000) NOT NULL,
    table_type VARCHAR(50) NOT NULL DEFAULT 'managed',
    properties JSONB,
    owner VARCHAR(255),
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    CONSTRAINT unique_table UNIQUE (namespace, table_name),
    CONSTRAINT check_storage_format CHECK (storage_format IN ('parquet', 'orc', 'avro', 'csv', 'json')),
    CONSTRAINT check_compression CHECK (compression_codec IN ('snappy', 'gzip', 'lz4', 'zstd', 'none')),
    CONSTRAINT check_table_type CHECK (table_type IN ('managed', 'external', 'view'))
);

CREATE INDEX IF NOT EXISTS idx_catalog_tables_namespace ON catalog.tables(namespace);
CREATE INDEX IF NOT EXISTS idx_catalog_tables_table_name ON catalog.tables(table_name);
CREATE INDEX IF NOT EXISTS idx_catalog_tables_owner ON catalog.tables(owner);

-- ==============================================================================================
-- DATA CATALOG - PARTITIONS
-- ==============================================================================================

CREATE TABLE IF NOT EXISTS catalog.partitions (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    table_id UUID REFERENCES catalog.tables(id) ON DELETE CASCADE,
    partition_values JSONB NOT NULL,
    location VARCHAR(1000) NOT NULL,
    file_count INTEGER DEFAULT 0,
    row_count BIGINT DEFAULT 0,
    size_bytes BIGINT DEFAULT 0,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_catalog_partitions_table_id ON catalog.partitions(table_id);
CREATE INDEX IF NOT EXISTS idx_catalog_partitions_values ON catalog.partitions USING GIN (partition_values);

-- ==============================================================================================
-- DATA CATALOG - COLUMNS
-- ==============================================================================================

CREATE TABLE IF NOT EXISTS catalog.columns (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    table_id UUID REFERENCES catalog.tables(id) ON DELETE CASCADE,
    column_name VARCHAR(255) NOT NULL,
    data_type VARCHAR(100) NOT NULL,
    nullable BOOLEAN DEFAULT TRUE,
    comment TEXT,
    statistics JSONB,
    ordinal_position INTEGER NOT NULL,
    CONSTRAINT unique_column UNIQUE (table_id, column_name)
);

CREATE INDEX IF NOT EXISTS idx_catalog_columns_table_id ON catalog.columns(table_id);
CREATE INDEX IF NOT EXISTS idx_catalog_columns_column_name ON catalog.columns(column_name);

-- ==============================================================================================
-- DATA LINEAGE
-- ==============================================================================================

CREATE TABLE IF NOT EXISTS catalog.lineage (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    source_table_id UUID REFERENCES catalog.tables(id) ON DELETE CASCADE,
    target_table_id UUID REFERENCES catalog.tables(id) ON DELETE CASCADE,
    transformation_type VARCHAR(100) NOT NULL,
    transformation_query TEXT,
    column_mappings JSONB,
    created_by VARCHAR(255),
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    CONSTRAINT check_transformation_type CHECK (transformation_type IN ('select', 'join', 'aggregate', 'filter', 'transform', 'union'))
);

CREATE INDEX IF NOT EXISTS idx_lineage_source_table_id ON catalog.lineage(source_table_id);
CREATE INDEX IF NOT EXISTS idx_lineage_target_table_id ON catalog.lineage(target_table_id);
CREATE INDEX IF NOT EXISTS idx_lineage_created_at ON catalog.lineage(created_at);

-- ==============================================================================================
-- QUERY CACHE
-- ==============================================================================================

CREATE TABLE IF NOT EXISTS query_cache (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    query_hash VARCHAR(64) NOT NULL UNIQUE,
    query_text TEXT NOT NULL,
    result_location VARCHAR(1000),
    result_row_count BIGINT,
    result_size_bytes BIGINT,
    execution_time_ms INTEGER,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    expires_at TIMESTAMP WITH TIME ZONE NOT NULL,
    hit_count INTEGER DEFAULT 0,
    last_accessed_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_query_cache_query_hash ON query_cache(query_hash);
CREATE INDEX IF NOT EXISTS idx_query_cache_expires_at ON query_cache(expires_at);

-- ==============================================================================================
-- QUERY HISTORY
-- ==============================================================================================

CREATE TABLE IF NOT EXISTS query_history (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    query_id VARCHAR(100) NOT NULL UNIQUE,
    query_text TEXT NOT NULL,
    user_id VARCHAR(255),
    namespace VARCHAR(255),
    status VARCHAR(50) NOT NULL,
    rows_scanned BIGINT,
    rows_returned BIGINT,
    bytes_scanned BIGINT,
    execution_time_ms INTEGER,
    error_message TEXT,
    started_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    completed_at TIMESTAMP WITH TIME ZONE,
    CONSTRAINT check_status CHECK (status IN ('running', 'completed', 'failed', 'cancelled'))
);

CREATE INDEX IF NOT EXISTS idx_query_history_query_id ON query_history(query_id);
CREATE INDEX IF NOT EXISTS idx_query_history_user_id ON query_history(user_id);
CREATE INDEX IF NOT EXISTS idx_query_history_status ON query_history(status);
CREATE INDEX IF NOT EXISTS idx_query_history_started_at ON query_history(started_at);

-- ==============================================================================================
-- DATA GOVERNANCE - ACCESS POLICIES
-- ==============================================================================================

CREATE TABLE IF NOT EXISTS catalog.access_policies (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    table_id UUID REFERENCES catalog.tables(id) ON DELETE CASCADE,
    policy_name VARCHAR(255) NOT NULL,
    policy_type VARCHAR(50) NOT NULL,
    principals VARCHAR(255)[],
    column_filters JSONB,
    row_filters TEXT,
    enabled BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    CONSTRAINT check_policy_type CHECK (policy_type IN ('column_masking', 'row_filtering', 'access_control'))
);

CREATE INDEX IF NOT EXISTS idx_access_policies_table_id ON catalog.access_policies(table_id);
CREATE INDEX IF NOT EXISTS idx_access_policies_enabled ON catalog.access_policies(enabled);

-- ==============================================================================================
-- DATA QUALITY RULES
-- ==============================================================================================

CREATE TABLE IF NOT EXISTS catalog.data_quality_rules (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    table_id UUID REFERENCES catalog.tables(id) ON DELETE CASCADE,
    rule_name VARCHAR(255) NOT NULL,
    rule_type VARCHAR(50) NOT NULL,
    column_name VARCHAR(255),
    rule_expression TEXT NOT NULL,
    severity VARCHAR(50) NOT NULL DEFAULT 'error',
    enabled BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    CONSTRAINT check_rule_type CHECK (rule_type IN ('not_null', 'unique', 'range', 'pattern', 'custom')),
    CONSTRAINT check_severity CHECK (severity IN ('error', 'warning', 'info'))
);

CREATE INDEX IF NOT EXISTS idx_data_quality_rules_table_id ON catalog.data_quality_rules(table_id);
CREATE INDEX IF NOT EXISTS idx_data_quality_rules_enabled ON catalog.data_quality_rules(enabled);

-- ==============================================================================================
-- PERMISSIONS
-- ==============================================================================================

-- Grant schema permissions to udps_user
GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA public TO udps_user;
GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA public TO udps_user;
GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA catalog TO udps_user;
GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA catalog TO udps_user;
GRANT USAGE ON SCHEMA catalog TO udps_user;

\echo 'UDPS schema created successfully'
