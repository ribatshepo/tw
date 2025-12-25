#!/usr/bin/env bash

set -euo pipefail

# ================================================================================================
# Wait for Infrastructure Health Checks
# ================================================================================================
# Waits for all infrastructure components to be healthy before proceeding
# - Tests actual connectivity, not just port checks
# - Retry logic with exponential backoff (max 5 minutes)
# - Clear progress indicators
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
fi

# Configuration
MAX_WAIT_SECONDS=${MAX_WAIT_SECONDS:-300}  # 5 minutes
RETRY_DELAY_SECONDS=${RETRY_DELAY_SECONDS:-5}
BACKOFF_MULTIPLIER=1.2

# ================================================================================================
# HEALTH CHECK FUNCTIONS
# ================================================================================================

# Wait for PostgreSQL
check_postgres() {
    log_progress "PostgreSQL"

    local start_time=$(date +%s)
    local delay=$RETRY_DELAY_SECONDS
    local ready=false

    while true; do
        # Check if container is running
        if ! docker ps --format '{{.Names}}' | grep -q "^gbmm-postgres$"; then
            log_progress_fail
            log_error "PostgreSQL container is not running"
            return 1
        fi

        # Check pg_isready
        if docker exec gbmm-postgres pg_isready -U "${POSTGRES_SUPERUSER}" -d postgres >/dev/null 2>&1; then
            # Try to execute a query
            if docker exec gbmm-postgres psql -U "${POSTGRES_SUPERUSER}" -d postgres -c "SELECT 1" >/dev/null 2>&1; then
                ready=true
                break
            fi
        fi

        # Check timeout
        local current_time=$(date +%s)
        local elapsed=$((current_time - start_time))

        if [[ $elapsed -ge $MAX_WAIT_SECONDS ]]; then
            log_progress_fail
            log_error "PostgreSQL did not become ready within $MAX_WAIT_SECONDS seconds"
            return 1
        fi

        sleep "$delay"
        delay=$(echo "$delay * $BACKOFF_MULTIPLIER" | bc | awk '{printf "%.0f\n", $0}')
    done

    if [[ "$ready" == "true" ]]; then
        log_progress_done
        return 0
    fi

    return 1
}

# Wait for Redis
check_redis() {
    log_progress "Redis"

    local start_time=$(date +%s)
    local delay=$RETRY_DELAY_SECONDS
    local ready=false

    while true; do
        if ! docker ps --format '{{.Names}}' | grep -q "^gbmm-redis$"; then
            log_progress_fail
            log_error "Redis container is not running"
            return 1
        fi

        if docker exec gbmm-redis redis-cli -a "${REDIS_PASSWORD}" --no-auth-warning PING 2>/dev/null | grep -q "PONG"; then
            ready=true
            break
        fi

        local current_time=$(date +%s)
        local elapsed=$((current_time - start_time))

        if [[ $elapsed -ge $MAX_WAIT_SECONDS ]]; then
            log_progress_fail
            log_error "Redis did not become ready within $MAX_WAIT_SECONDS seconds"
            return 1
        fi

        sleep "$delay"
        delay=$(echo "$delay * $BACKOFF_MULTIPLIER" | bc | awk '{printf "%.0f\n", $0}')
    done

    if [[ "$ready" == "true" ]]; then
        log_progress_done
        return 0
    fi

    return 1
}

# Wait for Zookeeper
check_zookeeper() {
    log_progress "Zookeeper"

    local start_time=$(date +%s)
    local delay=$RETRY_DELAY_SECONDS
    local ready=false

    while true; do
        if ! docker ps --format '{{.Names}}' | grep -q "^gbmm-zookeeper$"; then
            log_progress_fail
            log_error "Zookeeper container is not running"
            return 1
        fi

        if docker exec gbmm-zookeeper bash -c "echo ruok | nc localhost 2181" 2>/dev/null | grep -q "imok"; then
            ready=true
            break
        fi

        local current_time=$(date +%s)
        local elapsed=$((current_time - start_time))

        if [[ $elapsed -ge $MAX_WAIT_SECONDS ]]; then
            log_progress_fail
            log_error "Zookeeper did not become ready within $MAX_WAIT_SECONDS seconds"
            return 1
        fi

        sleep "$delay"
        delay=$(echo "$delay * $BACKOFF_MULTIPLIER" | bc | awk '{printf "%.0f\n", $0}')
    done

    if [[ "$ready" == "true" ]]; then
        log_progress_done
        return 0
    fi

    return 1
}

# Wait for Kafka
check_kafka() {
    log_progress "Kafka"

    local start_time=$(date +%s)
    local delay=$RETRY_DELAY_SECONDS
    local ready=false

    while true; do
        if ! docker ps --format '{{.Names}}' | grep -q "^gbmm-kafka$"; then
            log_progress_fail
            log_error "Kafka container is not running"
            return 1
        fi

        if docker exec gbmm-kafka kafka-broker-api-versions --bootstrap-server localhost:9092 >/dev/null 2>&1; then
            ready=true
            break
        fi

        local current_time=$(date +%s)
        local elapsed=$((current_time - start_time))

        if [[ $elapsed -ge $MAX_WAIT_SECONDS ]]; then
            log_progress_fail
            log_error "Kafka did not become ready within $MAX_WAIT_SECONDS seconds"
            return 1
        fi

        sleep "$delay"
        delay=$(echo "$delay * $BACKOFF_MULTIPLIER" | bc | awk '{printf "%.0f\n", $0}')
    done

    if [[ "$ready" == "true" ]]; then
        log_progress_done
        return 0
    fi

    return 1
}

# Wait for MinIO
check_minio() {
    log_progress "MinIO"

    local start_time=$(date +%s)
    local delay=$RETRY_DELAY_SECONDS
    local ready=false

    while true; do
        if ! docker ps --format '{{.Names}}' | grep -q "^gbmm-minio$"; then
            log_progress_fail
            log_error "MinIO container is not running"
            return 1
        fi

        if docker exec gbmm-minio curl -f -s http://localhost:9000/minio/health/live >/dev/null 2>&1; then
            ready=true
            break
        fi

        local current_time=$(date +%s)
        local elapsed=$((current_time - start_time))

        if [[ $elapsed -ge $MAX_WAIT_SECONDS ]]; then
            log_progress_fail
            log_error "MinIO did not become ready within $MAX_WAIT_SECONDS seconds"
            return 1
        fi

        sleep "$delay"
        delay=$(echo "$delay * $BACKOFF_MULTIPLIER" | bc | awk '{printf "%.0f\n", $0}')
    done

    if [[ "$ready" == "true" ]]; then
        log_progress_done
        return 0
    fi

    return 1
}

# Wait for RabbitMQ
check_rabbitmq() {
    log_progress "RabbitMQ"

    local start_time=$(date +%s)
    local delay=$RETRY_DELAY_SECONDS
    local ready=false

    while true; do
        if ! docker ps --format '{{.Names}}' | grep -q "^gbmm-rabbitmq$"; then
            log_progress_fail
            log_error "RabbitMQ container is not running"
            return 1
        fi

        if docker exec gbmm-rabbitmq rabbitmq-diagnostics -q ping >/dev/null 2>&1; then
            ready=true
            break
        fi

        local current_time=$(date +%s)
        local elapsed=$((current_time - start_time))

        if [[ $elapsed -ge $MAX_WAIT_SECONDS ]]; then
            log_progress_fail
            log_error "RabbitMQ did not become ready within $MAX_WAIT_SECONDS seconds"
            return 1
        fi

        sleep "$delay"
        delay=$(echo "$delay * $BACKOFF_MULTIPLIER" | bc | awk '{printf "%.0f\n", $0}')
    done

    if [[ "$ready" == "true" ]]; then
        log_progress_done
        return 0
    fi

    return 1
}

# Wait for Prometheus
# Note: Vault health check removed - USP (Unified Security Platform) provides secrets management
# and will be added as a service, not infrastructure component
check_prometheus() {
    log_progress "Prometheus"

    local start_time=$(date +%s)
    local delay=$RETRY_DELAY_SECONDS
    local ready=false

    while true; do
        if ! docker ps --format '{{.Names}}' | grep -q "^gbmm-prometheus$"; then
            log_progress_fail
            log_error "Prometheus container is not running"
            return 1
        fi

        if curl -f -s http://localhost:${PROMETHEUS_PORT:-9090}/-/healthy >/dev/null 2>&1; then
            ready=true
            break
        fi

        local current_time=$(date +%s)
        local elapsed=$((current_time - start_time))

        if [[ $elapsed -ge $MAX_WAIT_SECONDS ]]; then
            log_progress_fail
            log_error "Prometheus did not become ready within $MAX_WAIT_SECONDS seconds"
            return 1
        fi

        sleep "$delay"
        delay=$(echo "$delay * $BACKOFF_MULTIPLIER" | bc | awk '{printf "%.0f\n", $0}')
    done

    if [[ "$ready" == "true" ]]; then
        log_progress_done
        return 0
    fi

    return 1
}

# Wait for Grafana
check_grafana() {
    log_progress "Grafana"

    local start_time=$(date +%s)
    local delay=$RETRY_DELAY_SECONDS
    local ready=false

    while true; do
        if ! docker ps --format '{{.Names}}' | grep -q "^gbmm-grafana$"; then
            log_progress_fail
            log_error "Grafana container is not running"
            return 1
        fi

        if curl -f -s http://localhost:${GRAFANA_PORT:-3000}/api/health >/dev/null 2>&1; then
            ready=true
            break
        fi

        local current_time=$(date +%s)
        local elapsed=$((current_time - start_time))

        if [[ $elapsed -ge $MAX_WAIT_SECONDS ]]; then
            log_progress_fail
            log_error "Grafana did not become ready within $MAX_WAIT_SECONDS seconds"
            return 1
        fi

        sleep "$delay"
        delay=$(echo "$delay * $BACKOFF_MULTIPLIER" | bc | awk '{printf "%.0f\n", $0}')
    done

    if [[ "$ready" == "true" ]]; then
        log_progress_done
        return 0
    fi

    return 1
}

# Wait for Jaeger
check_jaeger() {
    log_progress "Jaeger"

    local start_time=$(date +%s)
    local delay=$RETRY_DELAY_SECONDS
    local ready=false

    while true; do
        if ! docker ps --format '{{.Names}}' | grep -q "^gbmm-jaeger$"; then
            log_progress_fail
            log_error "Jaeger container is not running"
            return 1
        fi

        if curl -f -s http://localhost:${JAEGER_ADMIN_PORT:-14269}/ >/dev/null 2>&1; then
            ready=true
            break
        fi

        local current_time=$(date +%s)
        local elapsed=$((current_time - start_time))

        if [[ $elapsed -ge $MAX_WAIT_SECONDS ]]; then
            log_progress_fail
            log_error "Jaeger did not become ready within $MAX_WAIT_SECONDS seconds"
            return 1
        fi

        sleep "$delay"
        delay=$(echo "$delay * $BACKOFF_MULTIPLIER" | bc | awk '{printf "%.0f\n", $0}')
    done

    if [[ "$ready" == "true" ]]; then
        log_progress_done
        return 0
    fi

    return 1
}

# Wait for Elasticsearch
check_elasticsearch() {
    log_progress "Elasticsearch"

    local start_time=$(date +%s)
    local delay=$RETRY_DELAY_SECONDS
    local ready=false

    while true; do
        if ! docker ps --format '{{.Names}}' | grep -q "^gbmm-elasticsearch$"; then
            log_progress_fail
            log_error "Elasticsearch container is not running"
            return 1
        fi

        if curl -f -s -u "elastic:${ELASTICSEARCH_PASSWORD}" http://localhost:${ELASTICSEARCH_PORT:-9200}/_cluster/health >/dev/null 2>&1; then
            ready=true
            break
        fi

        local current_time=$(date +%s)
        local elapsed=$((current_time - start_time))

        if [[ $elapsed -ge $MAX_WAIT_SECONDS ]]; then
            log_progress_fail
            log_error "Elasticsearch did not become ready within $MAX_WAIT_SECONDS seconds"
            return 1
        fi

        sleep "$delay"
        delay=$(echo "$delay * $BACKOFF_MULTIPLIER" | bc | awk '{printf "%.0f\n", $0}')
    done

    if [[ "$ready" == "true" ]]; then
        log_progress_done
        return 0
    fi

    return 1
}

# ================================================================================================
# MAIN
# ================================================================================================

main() {
    log_banner "GBMM Platform - Infrastructure Health Checks"

    log_section "Waiting for Infrastructure Components"

    local all_healthy=true

    # Data layer
    log_info "Data Layer Components:"
    check_postgres || all_healthy=false
    check_redis || all_healthy=false
    check_zookeeper || all_healthy=false
    check_kafka || all_healthy=false
    check_minio || all_healthy=false
    check_rabbitmq || all_healthy=false

    echo ""

    # Observability layer
    # Note: Security layer (USP) will be checked when services are implemented
    log_info "Observability Layer Components:"
    check_prometheus || all_healthy=false
    check_grafana || all_healthy=false
    check_jaeger || all_healthy=false
    check_elasticsearch || all_healthy=false

    echo ""

    if [[ "$all_healthy" == "true" ]]; then
        log_success "All infrastructure components are healthy!"
        return 0
    else
        log_error "Some infrastructure components failed health checks"
        return 1
    fi
}

main "$@"
