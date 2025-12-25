#!/usr/bin/env bash

set -euo pipefail

# ================================================================================================
# GBMM Platform - Environment Cleanup
# ================================================================================================
# Cleans up the development environment including:
# - Docker containers (infrastructure and services)
# - Docker volumes (with confirmation)
# - Generated certificates
# - Generated secrets
# - Build artifacts
#
# Options:
#   --all              Clean everything (default)
#   --containers-only  Only stop and remove containers
#   --volumes-only     Only remove volumes
#   --certs-only       Only remove certificates
#   --secrets-only     Only remove secrets
#   --keep-volumes     Keep Docker volumes
#   --keep-certs       Keep certificates
#   --keep-secrets     Keep secrets
#   --force            Skip confirmation prompts (use with caution)
#   --help             Show this help message
#
# Safety:
#   - Prompts for confirmation before destructive operations
#   - Checks ENVIRONMENT variable to prevent accidental production cleanup
#   - Use --force to skip confirmations (not recommended)
# ================================================================================================

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

# Source logging functions
source "$SCRIPT_DIR/helpers/logging.sh"

# Load environment variables if .env exists
if [[ -f "$PROJECT_ROOT/.env" ]]; then
    set -a
    source "$PROJECT_ROOT/.env"
    set +a
fi

# ================================================================================================
# CONFIGURATION
# ================================================================================================

DOCKER_COMPOSE_INFRA="$PROJECT_ROOT/docker-compose.infra.yml"
DOCKER_COMPOSE_SERVICES="$PROJECT_ROOT/docker-compose.yml"
CERTS_DIR="$PROJECT_ROOT/certs"
SECRETS_DIR="$PROJECT_ROOT/secrets"

# Cleanup flags
CLEANUP_ALL=true
CLEANUP_CONTAINERS=false
CLEANUP_VOLUMES=false
CLEANUP_CERTS=false
CLEANUP_SECRETS=false

# Keep flags
KEEP_VOLUMES=false
KEEP_CERTS=false
KEEP_SECRETS=false

# Force flag
FORCE=false

# ================================================================================================
# HELP
# ================================================================================================

show_help() {
    cat << EOF
$(log_banner "GBMM Platform - Cleanup Script")

Usage: $0 [OPTIONS]

Options:
  --all              Clean everything (default)
  --containers-only  Only stop and remove containers
  --volumes-only     Only remove volumes
  --certs-only       Only remove certificates
  --secrets-only     Only remove secrets
  --keep-volumes     Keep Docker volumes
  --keep-certs       Keep certificates
  --keep-secrets     Keep secrets
  --force            Skip confirmation prompts
  --help             Show this help message

Examples:
  $0                         # Clean everything with prompts
  $0 --keep-volumes          # Clean everything except volumes
  $0 --certs-only            # Only remove certificates
  $0 --containers-only       # Only stop containers
  $0 --all --force           # Clean everything without prompts (dangerous!)

Safety:
  - Always prompts for confirmation (unless --force)
  - Checks ENVIRONMENT variable
  - Prevents accidental production cleanup

EOF
}

# ================================================================================================
# ARGUMENT PARSING
# ================================================================================================

parse_arguments() {
    while [[ $# -gt 0 ]]; do
        case $1 in
            --all)
                CLEANUP_ALL=true
                shift
                ;;
            --containers-only)
                CLEANUP_ALL=false
                CLEANUP_CONTAINERS=true
                shift
                ;;
            --volumes-only)
                CLEANUP_ALL=false
                CLEANUP_VOLUMES=true
                shift
                ;;
            --certs-only)
                CLEANUP_ALL=false
                CLEANUP_CERTS=true
                shift
                ;;
            --secrets-only)
                CLEANUP_ALL=false
                CLEANUP_SECRETS=true
                shift
                ;;
            --keep-volumes)
                KEEP_VOLUMES=true
                shift
                ;;
            --keep-certs)
                KEEP_CERTS=true
                shift
                ;;
            --keep-secrets)
                KEEP_SECRETS=true
                shift
                ;;
            --force)
                FORCE=true
                shift
                ;;
            --help|-h)
                show_help
                exit 0
                ;;
            *)
                log_error "Unknown option: $1"
                show_help
                exit 1
                ;;
        esac
    done
}

# ================================================================================================
# SAFETY CHECKS
# ================================================================================================

check_environment() {
    local env="${ENVIRONMENT:-development}"

    if [[ "$env" == "production" ]] || [[ "$env" == "prod" ]]; then
        log_error "ENVIRONMENT is set to '$env'"
        log_error "This script should NOT be run in production!"
        echo ""

        if [[ "$FORCE" == "true" ]]; then
            log_warn "Force flag detected - proceeding anyway (THIS IS DANGEROUS!)"
            echo ""
            sleep 3
        else
            log_fatal "Aborting for safety. If you really want to cleanup production, use --force (not recommended)"
        fi
    fi
}

confirm_cleanup() {
    local action="$1"

    if [[ "$FORCE" == "true" ]]; then
        log_info "Force mode enabled - skipping confirmation for: $action"
        return 0
    fi

    log_warn "About to: $action"
    if confirm "Are you sure you want to proceed?"; then
        return 0
    else
        log_info "Cancelled by user"
        return 1
    fi
}

# ================================================================================================
# CLEANUP FUNCTIONS
# ================================================================================================

cleanup_containers() {
    log_step "1" "Stopping and Removing Containers"

    if ! confirm_cleanup "Stop and remove all Docker containers"; then
        return 0
    fi

    # Stop infrastructure containers
    if [[ -f "$DOCKER_COMPOSE_INFRA" ]]; then
        log_progress "Stopping infrastructure services"
        if docker-compose -f "$DOCKER_COMPOSE_INFRA" down >/dev/null 2>&1; then
            log_progress_done
        else
            log_progress_fail
            log_warn "Failed to stop infrastructure services (they may not be running)"
        fi
    else
        log_warn "docker-compose.infra.yml not found, skipping"
    fi

    # Stop service containers (if they exist)
    if [[ -f "$DOCKER_COMPOSE_SERVICES" ]]; then
        log_progress "Stopping application services"
        if docker-compose -f "$DOCKER_COMPOSE_SERVICES" down >/dev/null 2>&1; then
            log_progress_done
        else
            log_progress_fail
            log_warn "Failed to stop application services (they may not be running)"
        fi
    else
        log_info "docker-compose.yml not found (services not yet implemented)"
    fi

    echo ""
    log_success "Containers stopped and removed"
}

cleanup_volumes() {
    log_step "2" "Removing Docker Volumes"

    if ! confirm_cleanup "Remove all Docker volumes (THIS WILL DELETE ALL DATA!)"; then
        return 0
    fi

    log_warn "⚠️  WARNING: This will permanently delete all data in volumes!"
    log_warn "⚠️  Databases, logs, and all persistent data will be lost!"
    echo ""

    if [[ "$FORCE" == "true" ]]; then
        log_warn "Force mode - proceeding with volume deletion"
    else
        if ! confirm "Type 'yes' to confirm volume deletion"; then
            log_info "Volume deletion cancelled"
            return 0
        fi
    fi

    # Remove volumes for infrastructure
    if [[ -f "$DOCKER_COMPOSE_INFRA" ]]; then
        log_progress "Removing infrastructure volumes"
        if docker-compose -f "$DOCKER_COMPOSE_INFRA" down -v >/dev/null 2>&1; then
            log_progress_done
        else
            log_progress_fail
            log_warn "Failed to remove infrastructure volumes"
        fi
    fi

    # Remove volumes for services (if they exist)
    if [[ -f "$DOCKER_COMPOSE_SERVICES" ]]; then
        log_progress "Removing application service volumes"
        if docker-compose -f "$DOCKER_COMPOSE_SERVICES" down -v >/dev/null 2>&1; then
            log_progress_done
        else
            log_progress_fail
            log_warn "Failed to remove application service volumes"
        fi
    fi

    echo ""
    log_success "Volumes removed"
}

cleanup_certificates() {
    log_step "3" "Removing Generated Certificates"

    if [[ ! -d "$CERTS_DIR" ]]; then
        log_info "Certificates directory does not exist, skipping"
        return 0
    fi

    # Count certificates
    local cert_count=$(find "$CERTS_DIR" -type f \( -name "*.crt" -o -name "*.key" -o -name "*.pem" \) 2>/dev/null | wc -l)

    if [[ "$cert_count" -eq 0 ]]; then
        log_info "No certificates found, skipping"
        return 0
    fi

    if ! confirm_cleanup "Remove $cert_count certificate(s) from $CERTS_DIR"; then
        return 0
    fi

    log_progress "Removing certificates"

    # Remove all certificate files
    find "$CERTS_DIR" -type f \( -name "*.crt" -o -name "*.key" -o -name "*.pem" -o -name "*.srl" \) -delete 2>/dev/null

    # Remove empty directories
    find "$CERTS_DIR" -type d -empty -delete 2>/dev/null || true

    log_progress_done
    echo ""
    log_success "Certificates removed"
}

cleanup_secrets() {
    log_step "4" "Removing Generated Secrets"

    if [[ ! -d "$SECRETS_DIR" ]]; then
        log_info "Secrets directory does not exist, skipping"
        return 0
    fi

    # Count secret files
    local secret_count=$(find "$SECRETS_DIR" -type f 2>/dev/null | wc -l)

    if [[ "$secret_count" -eq 0 ]]; then
        log_info "No secret files found, skipping"
        return 0
    fi

    if ! confirm_cleanup "Remove $secret_count secret file(s) from $SECRETS_DIR"; then
        return 0
    fi

    log_progress "Removing secrets"

    # Remove all files in secrets directory
    find "$SECRETS_DIR" -type f -delete 2>/dev/null

    # Remove empty directories
    find "$SECRETS_DIR" -type d -empty -delete 2>/dev/null || true

    log_progress_done
    echo ""
    log_success "Secrets removed"
}

cleanup_build_artifacts() {
    log_step "5" "Removing Build Artifacts"

    local artifacts_found=false

    # Check for build directories
    local build_dirs=(
        "$PROJECT_ROOT/services/*/target"
        "$PROJECT_ROOT/services/*/build"
        "$PROJECT_ROOT/services/*/dist"
        "$PROJECT_ROOT/services/*/bin"
        "$PROJECT_ROOT/services/*/obj"
    )

    for pattern in "${build_dirs[@]}"; do
        if compgen -G "$pattern" > /dev/null 2>&1; then
            artifacts_found=true
            break
        fi
    done

    if [[ "$artifacts_found" == "false" ]]; then
        log_info "No build artifacts found, skipping"
        return 0
    fi

    if ! confirm_cleanup "Remove build artifacts (target, build, dist, bin, obj directories)"; then
        return 0
    fi

    log_progress "Removing build artifacts"

    # Remove build directories
    find "$PROJECT_ROOT/services" -type d \( -name target -o -name build -o -name dist -o -name bin -o -name obj \) -exec rm -rf {} + 2>/dev/null || true

    log_progress_done
    echo ""
    log_success "Build artifacts removed"
}

# ================================================================================================
# MAIN
# ================================================================================================

main() {
    # Parse arguments
    parse_arguments "$@"

    log_banner "GBMM Platform - Environment Cleanup"

    # Safety checks
    log_section "Safety Checks"
    check_environment
    log_success "Environment check passed"
    echo ""

    # Determine what to clean
    log_section "Cleanup Plan"

    if [[ "$CLEANUP_ALL" == "true" ]]; then
        log_info "Cleanup mode: ALL (everything)"
        CLEANUP_CONTAINERS=true
        CLEANUP_VOLUMES=true
        CLEANUP_CERTS=true
        CLEANUP_SECRETS=true
    else
        log_info "Cleanup mode: SELECTIVE"
    fi

    # Apply keep flags
    if [[ "$KEEP_VOLUMES" == "true" ]]; then
        CLEANUP_VOLUMES=false
        log_info "  - Keeping Docker volumes"
    fi

    if [[ "$KEEP_CERTS" == "true" ]]; then
        CLEANUP_CERTS=false
        log_info "  - Keeping certificates"
    fi

    if [[ "$KEEP_SECRETS" == "true" ]]; then
        CLEANUP_SECRETS=false
        log_info "  - Keeping secrets"
    fi

    echo ""
    log_info "Will cleanup:"
    [[ "$CLEANUP_CONTAINERS" == "true" ]] && log_item "Docker containers"
    [[ "$CLEANUP_VOLUMES" == "true" ]] && log_item "Docker volumes (DATA LOSS!)"
    [[ "$CLEANUP_CERTS" == "true" ]] && log_item "Generated certificates"
    [[ "$CLEANUP_SECRETS" == "true" ]] && log_item "Generated secrets"

    echo ""

    # Execute cleanup
    log_section "Cleanup Execution"

    if [[ "$CLEANUP_CONTAINERS" == "true" ]]; then
        cleanup_containers
    fi

    if [[ "$CLEANUP_VOLUMES" == "true" ]]; then
        cleanup_volumes
    fi

    if [[ "$CLEANUP_CERTS" == "true" ]]; then
        cleanup_certificates
    fi

    if [[ "$CLEANUP_SECRETS" == "true" ]]; then
        cleanup_secrets
    fi

    # Always cleanup build artifacts if they exist
    cleanup_build_artifacts

    # Summary
    log_section "Cleanup Summary"
    log_success "Cleanup completed successfully!"
    echo ""

    log_info "Next steps:"
    log_item "To start fresh: make dev"
    log_item "To regenerate certificates: make setup-certs"
    log_item "To start infrastructure: make infra-up"
    echo ""
}

main "$@"
