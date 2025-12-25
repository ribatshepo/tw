#!/usr/bin/env bash

# ================================================================================================
# GBMM Platform - Validation Helper Functions
# ================================================================================================
# Common validation utilities used by setup and infrastructure scripts
# - Prerequisite checks (tools, versions)
# - Docker daemon verification
# - Port availability checks
# - Disk space validation
# - Environment file validation
#
# Usage:
#   source scripts/helpers/validation.sh
#   check_prerequisites
#   check_docker_running
# ================================================================================================

# Source logging functions if not already loaded
if ! command -v log_info >/dev/null 2>&1; then
    SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
    source "$SCRIPT_DIR/logging.sh"
fi

# ================================================================================================
# PREREQUISITE CHECKS
# ================================================================================================

# Check if required tools are installed
check_prerequisites() {
    log_info "Checking prerequisites..."
    echo ""

    local all_found=true

    # Required tools with version check support
    local tools=(
        "docker:Docker:--version"
        "docker-compose:Docker Compose:--version"
        "openssl:OpenSSL:version"
        "curl:cURL:--version"
        "jq:jq (JSON processor):--version"
        "bc:bc (calculator):--version"
    )

    # Optional tools
    local optional_tools=(
        "git:Git:--version"
        "nc:netcat:--version"
    )

    # Check required tools
    for tool_spec in "${tools[@]}"; do
        local tool="${tool_spec%%:*}"
        local name="${tool_spec#*:}"
        name="${name%%:*}"
        local version_flag="${tool_spec##*:}"

        if command -v "$tool" >/dev/null 2>&1; then
            local version=$($tool $version_flag 2>&1 | head -n 1)
            log_success "✓ $name installed: $version"
        else
            log_error "✗ $name not found"
            log_info "  Install with: apt-get install $tool (Debian/Ubuntu) or brew install $tool (macOS)"
            all_found=false
        fi
    done

    echo ""

    # Check optional tools
    log_info "Optional tools:"
    for tool_spec in "${optional_tools[@]}"; do
        local tool="${tool_spec%%:*}"
        local name="${tool_spec#*:}"
        name="${name%%:*}"

        if command -v "$tool" >/dev/null 2>&1; then
            log_success "✓ $name installed"
        else
            log_warn "⚠ $name not found (optional)"
        fi
    done

    echo ""

    if [[ "$all_found" == "true" ]]; then
        log_success "All required prerequisites found"
        return 0
    else
        log_error "Some required tools are missing"
        return 1
    fi
}

# Check specific tool version
check_tool_version() {
    local tool="$1"
    local min_version="$2"

    if ! command -v "$tool" >/dev/null 2>&1; then
        return 1
    fi

    # Version comparison would require more complex logic
    # For now, just check if tool exists
    return 0
}

# ================================================================================================
# DOCKER CHECKS
# ================================================================================================

# Check if Docker daemon is running
check_docker_running() {
    log_info "Checking Docker daemon..."

    if ! command -v docker >/dev/null 2>&1; then
        log_error "Docker is not installed"
        return 1
    fi

    if ! docker info >/dev/null 2>&1; then
        log_error "Docker daemon is not running"
        log_info "Start Docker with: sudo systemctl start docker (Linux) or start Docker Desktop (macOS/Windows)"
        return 1
    fi

    log_success "Docker daemon is running"
    return 0
}

# Check Docker Compose availability
check_docker_compose() {
    log_info "Checking Docker Compose..."

    # Check for docker-compose standalone
    if command -v docker-compose >/dev/null 2>&1; then
        local version=$(docker-compose --version 2>&1)
        log_success "Docker Compose installed: $version"
        return 0
    fi

    # Check for docker compose plugin
    if docker compose version >/dev/null 2>&1; then
        local version=$(docker compose version 2>&1)
        log_success "Docker Compose (plugin) installed: $version"
        return 0
    fi

    log_error "Docker Compose not found"
    return 1
}

# ================================================================================================
# PORT AVAILABILITY CHECKS
# ================================================================================================

# Check if a port is available
check_port_available() {
    local port="$1"
    local protocol="${2:-tcp}"

    if command -v nc >/dev/null 2>&1; then
        # Use netcat to check port
        if nc -z localhost "$port" 2>/dev/null; then
            return 1  # Port is in use
        else
            return 0  # Port is available
        fi
    else
        # Fallback: try to bind to port with bash
        if (echo >/dev/tcp/localhost/"$port") 2>/dev/null; then
            return 1  # Port is in use
        else
            return 0  # Port is available
        fi
    fi
}

# Check if multiple ports are available
check_ports_available() {
    local ports=("$@")
    local all_available=true

    log_info "Checking port availability..."

    for port in "${ports[@]}"; do
        if check_port_available "$port"; then
            log_success "  Port $port is available"
        else
            log_error "  Port $port is already in use"
            all_available=false
        fi
    done

    if [[ "$all_available" == "true" ]]; then
        return 0
    else
        log_error "Some ports are not available"
        log_info "Stop conflicting services or change port assignments in .env"
        return 1
    fi
}

# ================================================================================================
# DISK SPACE CHECKS
# ================================================================================================

# Check available disk space
check_disk_space() {
    local min_gb="${1:-50}"  # Default: 50GB minimum
    local path="${2:-.}"     # Default: current directory

    log_info "Checking disk space..."

    # Get available space in GB
    local available_kb=$(df "$path" | tail -1 | awk '{print $4}')
    local available_gb=$((available_kb / 1024 / 1024))

    log_info "Available disk space: ${available_gb} GB"

    if [[ $available_gb -ge $min_gb ]]; then
        log_success "Sufficient disk space (${available_gb} GB >= ${min_gb} GB required)"
        return 0
    else
        log_error "Insufficient disk space (${available_gb} GB < ${min_gb} GB required)"
        log_warn "GBMM platform requires at least ${min_gb} GB for development"
        log_info "Free up disk space or use a different volume"
        return 1
    fi
}

# ================================================================================================
# ENVIRONMENT FILE VALIDATION
# ================================================================================================

# Validate .env file exists and has required variables
validate_env_file() {
    local env_file="${1:-.env}"
    local required_vars="${2:-}"  # Space-separated list of required variables

    log_info "Validating environment file: $env_file"

    # Check if file exists
    if [[ ! -f "$env_file" ]]; then
        log_error ".env file not found at: $env_file"
        log_info "Create .env file: cp .env.template .env"
        return 1
    fi

    log_success ".env file exists"

    # If specific variables are required, check for them
    if [[ -n "$required_vars" ]]; then
        local all_found=true

        for var in $required_vars; do
            if grep -q "^${var}=" "$env_file" 2>/dev/null; then
                local value=$(grep "^${var}=" "$env_file" | cut -d= -f2-)
                if [[ -n "$value" ]]; then
                    log_success "  $var is set"
                else
                    log_warn "  $var is set but empty"
                fi
            else
                log_error "  $var is not set"
                all_found=false
            fi
        done

        if [[ "$all_found" == "false" ]]; then
            log_error "Some required variables are missing"
            return 1
        fi
    fi

    # Check for default/insecure passwords
    local insecure_patterns=("change_me" "password" "admin" "root")
    local insecure_found=false

    for pattern in "${insecure_patterns[@]}"; do
        if grep -q "$pattern" "$env_file" 2>/dev/null; then
            insecure_found=true
        fi
    done

    if [[ "$insecure_found" == "true" ]]; then
        log_warn "⚠️  Default/insecure passwords detected in .env"
        log_warn "   Update all passwords before deploying to production"
    fi

    log_success "Environment file validation complete"
    return 0
}

# Check for required environment variables in current environment
check_env_vars() {
    local required_vars=("$@")
    local all_set=true

    for var in "${required_vars[@]}"; do
        if [[ -z "${!var:-}" ]]; then
            log_error "Required environment variable not set: $var"
            all_set=false
        fi
    done

    if [[ "$all_set" == "false" ]]; then
        log_error "Some required environment variables are missing"
        log_info "Source .env file: set -a; source .env; set +a"
        return 1
    fi

    return 0
}

# ================================================================================================
# FILE AND DIRECTORY CHECKS
# ================================================================================================

# Check if required files exist
check_files_exist() {
    local files=("$@")
    local all_exist=true

    for file in "${files[@]}"; do
        if [[ ! -f "$file" ]]; then
            log_error "Required file not found: $file"
            all_exist=false
        fi
    done

    if [[ "$all_exist" == "false" ]]; then
        return 1
    fi

    return 0
}

# Check if required directories exist
check_directories_exist() {
    local dirs=("$@")
    local all_exist=true

    for dir in "${dirs[@]}"; do
        if [[ ! -d "$dir" ]]; then
            log_error "Required directory not found: $dir"
            all_exist=false
        fi
    done

    if [[ "$all_exist" == "false" ]]; then
        return 1
    fi

    return 0
}

# Create directory if it doesn't exist
ensure_directory() {
    local dir="$1"
    local mode="${2:-755}"

    if [[ ! -d "$dir" ]]; then
        log_info "Creating directory: $dir"
        mkdir -p "$dir"
        chmod "$mode" "$dir"
    fi
}

# ================================================================================================
# CERTIFICATE CHECKS
# ================================================================================================

# Check if certificate is valid and not expired
check_certificate_valid() {
    local cert_file="$1"
    local min_days_remaining="${2:-7}"  # Default: warn if expires in less than 7 days

    if [[ ! -f "$cert_file" ]]; then
        log_error "Certificate not found: $cert_file"
        return 1
    fi

    # Check if certificate is expired or expiring soon
    if ! openssl x509 -in "$cert_file" -noout -checkend $((min_days_remaining * 86400)) >/dev/null 2>&1; then
        local expiry=$(openssl x509 -in "$cert_file" -noout -enddate 2>/dev/null | cut -d= -f2)
        log_error "Certificate expired or expiring soon: $cert_file"
        log_info "  Expiry: $expiry"
        log_info "  Regenerate with: make setup-certs"
        return 1
    fi

    return 0
}

# ================================================================================================
# NETWORK CHECKS
# ================================================================================================

# Check if a host is reachable
check_host_reachable() {
    local host="$1"
    local port="${2:-80}"
    local timeout="${3:-5}"

    if command -v nc >/dev/null 2>&1; then
        if nc -z -w "$timeout" "$host" "$port" >/dev/null 2>&1; then
            return 0
        else
            return 1
        fi
    else
        # Fallback to curl
        if curl -s --connect-timeout "$timeout" "http://${host}:${port}" >/dev/null 2>&1; then
            return 0
        else
            return 1
        fi
    fi
}

# ================================================================================================
# COMPREHENSIVE VALIDATION
# ================================================================================================

# Run all validation checks
validate_environment() {
    local min_disk_gb="${1:-50}"

    log_section "Environment Validation"

    local validation_failed=false

    # Check prerequisites
    if ! check_prerequisites; then
        validation_failed=true
    fi

    echo ""

    # Check Docker
    if ! check_docker_running; then
        validation_failed=true
    fi

    echo ""

    if ! check_docker_compose; then
        validation_failed=true
    fi

    echo ""

    # Check disk space
    if ! check_disk_space "$min_disk_gb"; then
        validation_failed=true
    fi

    echo ""

    # Check .env file
    if ! validate_env_file; then
        validation_failed=true
    fi

    echo ""

    if [[ "$validation_failed" == "true" ]]; then
        log_error "Environment validation failed"
        return 1
    else
        log_success "Environment validation passed"
        return 0
    fi
}

# Export functions for use in other scripts
export -f check_prerequisites
export -f check_docker_running
export -f check_docker_compose
export -f check_port_available
export -f check_ports_available
export -f check_disk_space
export -f validate_env_file
export -f check_env_vars
export -f check_files_exist
export -f check_directories_exist
export -f ensure_directory
export -f check_certificate_valid
export -f check_host_reachable
export -f validate_environment
