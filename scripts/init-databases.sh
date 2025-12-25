#!/usr/bin/env bash

set -euo pipefail

# ================================================================================================
# Initialize PostgreSQL Databases
# ================================================================================================
# Initializes all PostgreSQL databases and schemas for GBMM platform services
# - Waits for PostgreSQL to be ready
# - Verifies init scripts were executed
# - Creates databases: uccp_db, nccs_db, usp_db, udps_db, stream_db
# - Creates service-specific schemas and tables
# - Idempotent: Safe to run multiple times
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
    log_fatal ".env file not found. Run 'make setup-env' first."
fi

# Configuration
POSTGRES_CONTAINER="gbmm-postgres"
INIT_SCRIPTS_DIR="$PROJECT_ROOT/config/postgres/init-scripts"
MAX_WAIT_SECONDS=${MAX_WAIT_SECONDS:-300}
RETRY_DELAY_SECONDS=${RETRY_DELAY_SECONDS:-5}

# ================================================================================================
# HELPER FUNCTIONS
# ================================================================================================

# Wait for PostgreSQL to be ready
wait_for_postgres() {
    log_step "1" "Waiting for PostgreSQL"

    local start_time=$(date +%s)
    local ready=false

    while true; do
        # Check if container is running
        if ! docker ps --format '{{.Names}}' | grep -q "^${POSTGRES_CONTAINER}$"; then
            log_error "PostgreSQL container '${POSTGRES_CONTAINER}' is not running"
            log_info "Start infrastructure with: make infra-up"
            return 1
        fi

        # Check if PostgreSQL is accepting connections
        if docker exec "${POSTGRES_CONTAINER}" pg_isready -U "${POSTGRES_SUPERUSER}" -d postgres >/dev/null 2>&1; then
            # Try to execute a query
            if docker exec "${POSTGRES_CONTAINER}" psql -U "${POSTGRES_SUPERUSER}" -d postgres -c "SELECT 1" >/dev/null 2>&1; then
                ready=true
                break
            fi
        fi

        # Check timeout
        local current_time=$(date +%s)
        local elapsed=$((current_time - start_time))

        if [[ $elapsed -ge $MAX_WAIT_SECONDS ]]; then
            log_error "PostgreSQL did not become ready within $MAX_WAIT_SECONDS seconds"
            return 1
        fi

        sleep "$RETRY_DELAY_SECONDS"
    done

    if [[ "$ready" == "true" ]]; then
        log_success "PostgreSQL is ready"
        return 0
    fi

    return 1
}

# Check if database exists
check_database_exists() {
    local db_name="$1"

    docker exec "${POSTGRES_CONTAINER}" psql -U "${POSTGRES_SUPERUSER}" -lqt 2>/dev/null | \
        cut -d \| -f 1 | \
        grep -qw "${db_name}"
}

# Verify database schema
verify_database_schema() {
    local db_name="$1"
    local expected_tables_count="$2"

    local actual_count=$(docker exec "${POSTGRES_CONTAINER}" psql -U "${POSTGRES_SUPERUSER}" -d "${db_name}" -t -c \
        "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'public' AND table_type = 'BASE TABLE';" 2>/dev/null | xargs)

    if [[ "$actual_count" -ge "$expected_tables_count" ]]; then
        return 0
    else
        return 1
    fi
}

# Get table count for database
get_table_count() {
    local db_name="$1"

    docker exec "${POSTGRES_CONTAINER}" psql -U "${POSTGRES_SUPERUSER}" -d "${db_name}" -t -c \
        "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'public' AND table_type = 'BASE TABLE';" 2>/dev/null | xargs
}

# ================================================================================================
# DATABASE VERIFICATION
# ================================================================================================

verify_databases() {
    log_step "2" "Verifying Databases"

    local all_exist=true

    # Database definitions: name, min_expected_tables
    local databases=(
        "uccp_db:10"
        "nccs_db:5"
        "usp_db:8"
        "udps_db:8"
        "stream_db:10"
    )

    echo ""
    log_info "Checking database existence and schemas:"

    for db_entry in "${databases[@]}"; do
        local db_name="${db_entry%%:*}"
        local expected_tables="${db_entry##*:}"

        if check_database_exists "$db_name"; then
            local table_count=$(get_table_count "$db_name")

            if [[ "$table_count" -ge "$expected_tables" ]]; then
                log_success "  ✓ $db_name exists with $table_count tables (expected >=$expected_tables)"
            else
                log_warn "  ⚠ $db_name exists but has only $table_count tables (expected >=$expected_tables)"
                log_warn "    Init scripts may not have run completely"
                all_exist=false
            fi
        else
            log_error "  ✗ $db_name does not exist"
            all_exist=false
        fi
    done

    echo ""

    if [[ "$all_exist" == "true" ]]; then
        log_success "All databases verified successfully"
        return 0
    else
        log_error "Some databases are missing or incomplete"
        return 1
    fi
}

# ================================================================================================
# DATABASE INITIALIZATION STATUS
# ================================================================================================

check_init_status() {
    log_step "3" "Checking Initialization Status"

    # Check if init scripts have been run by checking for init marker
    # PostgreSQL init scripts only run on first container start
    # We can check if databases exist to determine if init ran

    if check_database_exists "uccp_db" && \
       check_database_exists "nccs_db" && \
       check_database_exists "usp_db" && \
       check_database_exists "udps_db" && \
       check_database_exists "stream_db"; then
        log_info "All databases exist - init scripts have been executed"
        return 0
    else
        log_info "Some databases missing - init scripts need to run"
        return 1
    fi
}

# ================================================================================================
# MANUAL INITIALIZATION
# ================================================================================================

run_init_scripts_manually() {
    log_step "4" "Running Initialization Scripts Manually"

    log_info "Executing SQL init scripts from: $INIT_SCRIPTS_DIR"
    echo ""

    # Get all SQL files in order
    local sql_files=(
        "01-create-databases.sql"
        "02-create-roles.sql"
        "03-enable-extensions.sql"
        "04-uccp-schema.sql"
        "05-usp-schema.sql"
        "06-nccs-schema.sql"
        "07-udps-schema.sql"
        "08-stream-schema.sql"
    )

    for sql_file in "${sql_files[@]}"; do
        local file_path="${INIT_SCRIPTS_DIR}/${sql_file}"

        if [[ ! -f "$file_path" ]]; then
            log_error "SQL file not found: $file_path"
            return 1
        fi

        log_progress "Executing $sql_file"

        if docker exec -i "${POSTGRES_CONTAINER}" psql -U "${POSTGRES_SUPERUSER}" -d postgres < "$file_path" >/dev/null 2>&1; then
            log_progress_done
        else
            log_progress_fail
            log_error "Failed to execute $sql_file"
            return 1
        fi
    done

    echo ""
    log_success "All initialization scripts executed successfully"
    return 0
}

# ================================================================================================
# MAIN
# ================================================================================================

main() {
    log_banner "GBMM Platform - Database Initialization"

    # Check prerequisites
    log_section "Prerequisites Check"

    if ! command -v docker >/dev/null 2>&1; then
        log_fatal "Docker is not installed"
    fi

    log_success "Docker is installed"
    echo ""

    # Wait for PostgreSQL
    log_section "PostgreSQL Readiness"
    if ! wait_for_postgres; then
        log_fatal "PostgreSQL is not ready"
    fi
    echo ""

    # Check initialization status
    log_section "Initialization Status"
    if check_init_status; then
        log_info "Databases already initialized (init scripts ran on first container start)"
        echo ""

        # Verify they're correct
        log_section "Database Verification"
        if verify_databases; then
            log_success "All databases verified and ready!"
            echo ""
            return 0
        else
            log_warn "Some databases need re-initialization"
            echo ""

            # Ask if user wants to re-run init scripts
            if confirm "Re-run initialization scripts?"; then
                log_section "Re-running Initialization"
                if run_init_scripts_manually; then
                    log_section "Database Verification"
                    if verify_databases; then
                        log_success "All databases re-initialized successfully!"
                        echo ""
                        return 0
                    else
                        log_error "Database verification failed after re-initialization"
                        return 1
                    fi
                else
                    log_fatal "Failed to re-run initialization scripts"
                fi
            else
                log_info "Skipping re-initialization"
                return 1
            fi
        fi
    else
        log_info "Databases not fully initialized - running init scripts"
        echo ""

        # Run init scripts manually
        log_section "Database Initialization"
        if run_init_scripts_manually; then
            log_section "Database Verification"
            if verify_databases; then
                log_success "All databases initialized successfully!"
                echo ""
                return 0
            else
                log_error "Database verification failed after initialization"
                return 1
            fi
        else
            log_fatal "Failed to run initialization scripts"
        fi
    fi
}

main "$@"
