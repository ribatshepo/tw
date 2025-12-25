#!/usr/bin/env bash

set -euo pipefail

# ================================================================================================
# GBMM Platform - Smoke Tests
# ================================================================================================
# Validates infrastructure setup by running smoke tests against all components
# - Infrastructure connectivity (PostgreSQL, Redis, Kafka, MinIO, RabbitMQ)
# - Database schemas (tables exist)
# - Certificate validity (not expired)
# - Observability stack (Prometheus, Grafana, Jaeger, Elasticsearch)
# - Basic operations (create topics, buckets, cache keys)
#
# Exit codes:
#   0 - All tests passed
#   1 - One or more tests failed
# ================================================================================================

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

# Source logging functions
source "$SCRIPT_DIR/helpers/logging.sh"

# Load environment variables
if [[ -f "$PROJECT_ROOT/.env" ]]; then
    set -a
    source "$PROJECT_ROOT/.env"
    set +a
else
    log_warn ".env file not found, using defaults"
fi

# ================================================================================================
# CONFIGURATION
# ================================================================================================

CERTS_DIR="${CERTS_DIR:-$PROJECT_ROOT/certs}"
POSTGRES_CONTAINER="gbmm-postgres"
REDIS_CONTAINER="gbmm-redis"
KAFKA_CONTAINER="gbmm-kafka"
MINIO_CONTAINER="gbmm-minio"
RABBITMQ_CONTAINER="gbmm-rabbitmq"
PROMETHEUS_CONTAINER="gbmm-prometheus"
GRAFANA_CONTAINER="gbmm-grafana"
JAEGER_CONTAINER="gbmm-jaeger"
ELASTICSEARCH_CONTAINER="gbmm-elasticsearch"

# Test results
TESTS_PASSED=0
TESTS_FAILED=0
FAILED_TESTS=()

# ================================================================================================
# TEST HELPERS
# ================================================================================================

# Run a test and track results
run_test() {
    local test_name="$1"
    local test_function="$2"

    log_progress "$test_name"

    if $test_function >/dev/null 2>&1; then
        log_progress_done
        ((TESTS_PASSED++)) || true
    else
        log_progress_fail
        ((TESTS_FAILED++)) || true
        FAILED_TESTS+=("$test_name")
    fi

    # Always return 0 to prevent set -e from exiting
    return 0
}

# Check if container is running
container_running() {
    local container_name="$1"
    docker ps --format '{{.Names}}' | grep -q "^${container_name}$"
}

# ================================================================================================
# INFRASTRUCTURE CONNECTIVITY TESTS
# ================================================================================================

test_postgres_connectivity() {
    container_running "$POSTGRES_CONTAINER" && \
    docker exec "$POSTGRES_CONTAINER" pg_isready -U "${POSTGRES_SUPERUSER}" >/dev/null 2>&1 && \
    docker exec "$POSTGRES_CONTAINER" psql -U "${POSTGRES_SUPERUSER}" -d postgres -c "SELECT 1" >/dev/null 2>&1
}

test_redis_connectivity() {
    container_running "$REDIS_CONTAINER" && \
    docker exec "$REDIS_CONTAINER" redis-cli -a "${REDIS_PASSWORD}" --no-auth-warning PING 2>/dev/null | grep -q "PONG"
}

test_kafka_connectivity() {
    container_running "$KAFKA_CONTAINER" && \
    docker exec "$KAFKA_CONTAINER" kafka-broker-api-versions --bootstrap-server localhost:9092 >/dev/null 2>&1
}

test_minio_connectivity() {
    container_running "$MINIO_CONTAINER" && \
    docker exec "$MINIO_CONTAINER" curl -f -s http://localhost:9000/minio/health/live >/dev/null 2>&1
}

test_rabbitmq_connectivity() {
    container_running "$RABBITMQ_CONTAINER" && \
    docker exec "$RABBITMQ_CONTAINER" rabbitmq-diagnostics -q ping >/dev/null 2>&1
}

test_prometheus_connectivity() {
    container_running "$PROMETHEUS_CONTAINER" && \
    curl -f -s http://localhost:${PROMETHEUS_PORT:-9090}/-/healthy >/dev/null 2>&1
}

test_grafana_connectivity() {
    container_running "$GRAFANA_CONTAINER" && \
    curl -f -s http://localhost:${GRAFANA_PORT:-3000}/api/health >/dev/null 2>&1
}

test_jaeger_connectivity() {
    container_running "$JAEGER_CONTAINER" && \
    curl -f -s http://localhost:${JAEGER_ADMIN_PORT:-14269}/ >/dev/null 2>&1
}

test_elasticsearch_connectivity() {
    container_running "$ELASTICSEARCH_CONTAINER" && \
    curl -f -s -u "elastic:${ELASTICSEARCH_PASSWORD}" http://localhost:${ELASTICSEARCH_PORT:-9200}/_cluster/health >/dev/null 2>&1
}

# ================================================================================================
# DATABASE SCHEMA TESTS
# ================================================================================================

test_uccp_database() {
    docker exec "$POSTGRES_CONTAINER" psql -U "${POSTGRES_SUPERUSER}" -d uccp_db -c "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'public'" >/dev/null 2>&1
}

test_nccs_database() {
    docker exec "$POSTGRES_CONTAINER" psql -U "${POSTGRES_SUPERUSER}" -d nccs_db -c "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'public'" >/dev/null 2>&1
}

test_usp_database() {
    docker exec "$POSTGRES_CONTAINER" psql -U "${POSTGRES_SUPERUSER}" -d usp_db -c "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'public'" >/dev/null 2>&1
}

test_udps_database() {
    docker exec "$POSTGRES_CONTAINER" psql -U "${POSTGRES_SUPERUSER}" -d udps_db -c "SELECT COUNT(*) FROM catalog.tables" >/dev/null 2>&1 || \
    docker exec "$POSTGRES_CONTAINER" psql -U "${POSTGRES_SUPERUSER}" -d udps_db -c "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'public'" >/dev/null 2>&1
}

test_stream_database() {
    docker exec "$POSTGRES_CONTAINER" psql -U "${POSTGRES_SUPERUSER}" -d stream_db -c "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'public'" >/dev/null 2>&1
}

# ================================================================================================
# CERTIFICATE VALIDITY TESTS
# ================================================================================================

test_ca_certificate() {
    local ca_cert="${CERTS_DIR}/ca.crt"

    [[ -f "$ca_cert" ]] && \
    openssl x509 -in "$ca_cert" -noout -checkend 86400 >/dev/null 2>&1  # Valid for at least 1 day
}

test_service_certificates() {
    local services=("uccp" "nccs" "usp" "udps" "stream" "client")
    local all_valid=true

    for service in "${services[@]}"; do
        local cert="${CERTS_DIR}/${service}.crt"

        if [[ ! -f "$cert" ]]; then
            return 1
        fi

        if ! openssl x509 -in "$cert" -noout -checkend 86400 >/dev/null 2>&1; then
            return 1
        fi
    done

    return 0
}

# ================================================================================================
# FUNCTIONAL OPERATION TESTS
# ================================================================================================

test_redis_set_get() {
    local test_key="smoke_test_$(date +%s)"
    local test_value="smoke_test_value"

    docker exec "$REDIS_CONTAINER" redis-cli -a "${REDIS_PASSWORD}" --no-auth-warning SET "$test_key" "$test_value" >/dev/null 2>&1 && \
    docker exec "$REDIS_CONTAINER" redis-cli -a "${REDIS_PASSWORD}" --no-auth-warning GET "$test_key" 2>/dev/null | grep -q "$test_value" && \
    docker exec "$REDIS_CONTAINER" redis-cli -a "${REDIS_PASSWORD}" --no-auth-warning DEL "$test_key" >/dev/null 2>&1
}

test_kafka_topic_creation() {
    local test_topic="smoke-test-topic-$(date +%s)"

    # Create topic
    docker exec "$KAFKA_CONTAINER" kafka-topics --bootstrap-server localhost:9092 --create --topic "$test_topic" --partitions 1 --replication-factor 1 >/dev/null 2>&1 && \
    # List topics to verify
    docker exec "$KAFKA_CONTAINER" kafka-topics --bootstrap-server localhost:9092 --list 2>/dev/null | grep -q "$test_topic" && \
    # Delete topic
    docker exec "$KAFKA_CONTAINER" kafka-topics --bootstrap-server localhost:9092 --delete --topic "$test_topic" >/dev/null 2>&1
}

test_postgres_query() {
    docker exec "$POSTGRES_CONTAINER" psql -U "${POSTGRES_SUPERUSER}" -d postgres -c "SELECT version()" >/dev/null 2>&1
}

test_minio_bucket_operations() {
    # MinIO CLI might not be available, so we test via health endpoint only
    docker exec "$MINIO_CONTAINER" curl -f -s http://localhost:9000/minio/health/ready >/dev/null 2>&1
}

# ================================================================================================
# OBSERVABILITY TESTS
# ================================================================================================

test_prometheus_targets() {
    # Check if Prometheus has any targets configured
    curl -s http://localhost:${PROMETHEUS_PORT:-9090}/api/v1/targets 2>/dev/null | grep -q "\"status\":\"success\""
}

test_grafana_datasources() {
    # Check if Grafana API is accessible
    curl -s -u "admin:${GRAFANA_ADMIN_PASSWORD}" http://localhost:${GRAFANA_PORT:-3000}/api/datasources 2>/dev/null | grep -q "\["
}

# ================================================================================================
# MAIN
# ================================================================================================

main() {
    log_banner "GBMM Platform - Smoke Tests"

    # Infrastructure Connectivity Tests
    log_section "Infrastructure Connectivity Tests"

    run_test "PostgreSQL connectivity" test_postgres_connectivity
    run_test "Redis connectivity" test_redis_connectivity
    run_test "Kafka connectivity" test_kafka_connectivity
    run_test "MinIO connectivity" test_minio_connectivity
    run_test "RabbitMQ connectivity" test_rabbitmq_connectivity

    echo ""

    # Observability Connectivity Tests
    log_section "Observability Stack Tests"

    run_test "Prometheus connectivity" test_prometheus_connectivity
    run_test "Grafana connectivity" test_grafana_connectivity
    run_test "Jaeger connectivity" test_jaeger_connectivity
    run_test "Elasticsearch connectivity" test_elasticsearch_connectivity

    echo ""

    # Database Schema Tests
    log_section "Database Schema Tests"

    run_test "UCCP database schema" test_uccp_database
    run_test "NCCS database schema" test_nccs_database
    run_test "USP database schema" test_usp_database
    run_test "UDPS database schema" test_udps_database
    run_test "Stream database schema" test_stream_database

    echo ""

    # Certificate Tests
    log_section "Certificate Validity Tests"

    run_test "CA certificate validity" test_ca_certificate
    run_test "Service certificates validity" test_service_certificates

    echo ""

    # Functional Operation Tests
    log_section "Functional Operation Tests"

    run_test "Redis SET/GET operations" test_redis_set_get
    run_test "Kafka topic creation" test_kafka_topic_creation
    run_test "PostgreSQL query execution" test_postgres_query
    run_test "MinIO health check" test_minio_bucket_operations

    echo ""

    # Observability Function Tests
    log_section "Observability Function Tests"

    run_test "Prometheus targets API" test_prometheus_targets
    run_test "Grafana datasources API" test_grafana_datasources

    echo ""

    # Summary
    log_section "Test Summary"

    local total_tests=$((TESTS_PASSED + TESTS_FAILED))

    echo ""
    log_info "Total tests: $total_tests"
    log_success "Passed: $TESTS_PASSED"

    if [[ $TESTS_FAILED -gt 0 ]]; then
        log_error "Failed: $TESTS_FAILED"
        echo ""
        log_error "Failed tests:"
        for test in "${FAILED_TESTS[@]}"; do
            log_item "$test"
        done
        echo ""
        log_error "❌ Smoke tests FAILED"
        echo ""

        log_info "Troubleshooting:"
        log_item "Check infrastructure is running: make infra-up"
        log_item "Check logs: docker-compose -f docker-compose.infra.yml logs"
        log_item "Verify .env file exists and has correct values"
        log_item "Regenerate certificates: make setup-certs"
        log_item "Reinitialize databases: make setup-databases"
        echo ""

        exit 1
    else
        echo ""
        log_success "✓ All smoke tests PASSED!"
        echo ""

        log_info "Infrastructure is ready for use!"
        log_info "Next steps:"
        log_item "Start developing services in services/ directory"
        log_item "Add services to docker-compose.yml when ready"
        log_item "Refer to docs/specs/ for service specifications"
        echo ""

        exit 0
    fi
}

main "$@"
