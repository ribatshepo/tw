#!/usr/bin/env bash

# ================================================================================================
# Logging Helper Functions
# ================================================================================================
# Provides colored, timestamped logging functions for all GBMM platform scripts
# Usage: source "$SCRIPT_DIR/helpers/logging.sh"
# ================================================================================================

# Color codes
readonly COLOR_RESET='\033[0m'
readonly COLOR_RED='\033[0;31m'
readonly COLOR_GREEN='\033[0;32m'
readonly COLOR_YELLOW='\033[0;33m'
readonly COLOR_BLUE='\033[0;34m'
readonly COLOR_MAGENTA='\033[0;35m'
readonly COLOR_CYAN='\033[0;36m'
readonly COLOR_WHITE='\033[0;37m'
readonly COLOR_BOLD='\033[1m'
readonly COLOR_DIM='\033[2m'

# Log level values
readonly LOG_LEVEL_DEBUG=0
readonly LOG_LEVEL_INFO=1
readonly LOG_LEVEL_SUCCESS=2
readonly LOG_LEVEL_WARN=3
readonly LOG_LEVEL_ERROR=4

# Default log level (can be overridden by LOG_LEVEL environment variable)
CURRENT_LOG_LEVEL=${LOG_LEVEL:-$LOG_LEVEL_INFO}

# Convert log level name to value
get_log_level_value() {
    local level_name="${1:-INFO}"
    case "${level_name^^}" in
        DEBUG) echo $LOG_LEVEL_DEBUG ;;
        INFO) echo $LOG_LEVEL_INFO ;;
        SUCCESS) echo $LOG_LEVEL_SUCCESS ;;
        WARN|WARNING) echo $LOG_LEVEL_WARN ;;
        ERROR) echo $LOG_LEVEL_ERROR ;;
        *) echo $LOG_LEVEL_INFO ;;
    esac
}

# Get current timestamp
get_timestamp() {
    date '+%Y-%m-%d %H:%M:%S'
}

# Get script name (caller)
get_script_name() {
    basename "${BASH_SOURCE[2]}"
}

# Check if output supports colors
supports_color() {
    if [[ -t 1 ]] && [[ "${TERM:-}" != "dumb" ]] && [[ "${NO_COLOR:-}" != "1" ]]; then
        return 0
    else
        return 1
    fi
}

# Internal log function
_log() {
    local level="$1"
    local level_value="$2"
    local color="$3"
    local icon="$4"
    shift 4
    local message="$*"

    # Check if this log level should be printed
    local current_level_value=$(get_log_level_value "$CURRENT_LOG_LEVEL")
    if [[ $level_value -lt $current_level_value ]]; then
        return
    fi

    local timestamp=$(get_timestamp)
    local script_name=$(get_script_name)

    if supports_color; then
        echo -e "${COLOR_DIM}${timestamp}${COLOR_RESET} ${color}${icon} [${level}]${COLOR_RESET} ${COLOR_DIM}${script_name}:${COLOR_RESET} ${message}" >&2
    else
        echo "${timestamp} ${icon} [${level}] ${script_name}: ${message}" >&2
    fi
}

# Debug log - detailed information for troubleshooting
# Usage: log_debug "Debug message"
log_debug() {
    _log "DEBUG" $LOG_LEVEL_DEBUG "$COLOR_MAGENTA" "🔍" "$@"
}

# Info log - general informational messages
# Usage: log_info "Info message"
log_info() {
    _log "INFO" $LOG_LEVEL_INFO "$COLOR_BLUE" "ℹ️ " "$@"
}

# Success log - successful operations
# Usage: log_success "Success message"
log_success() {
    _log "SUCCESS" $LOG_LEVEL_SUCCESS "$COLOR_GREEN" "✓" "$@"
}

# Warning log - warning messages that don't stop execution
# Usage: log_warn "Warning message"
log_warn() {
    _log "WARN" $LOG_LEVEL_WARN "$COLOR_YELLOW" "⚠️ " "$@"
}

# Error log - error messages
# Usage: log_error "Error message"
log_error() {
    _log "ERROR" $LOG_LEVEL_ERROR "$COLOR_RED" "✗" "$@"
}

# Print a section header
# Usage: log_section "Section Name"
log_section() {
    local message="$*"
    local separator="================================================================================================"

    if supports_color; then
        echo -e "\n${COLOR_BOLD}${COLOR_CYAN}${separator}${COLOR_RESET}" >&2
        echo -e "${COLOR_BOLD}${COLOR_CYAN}${message}${COLOR_RESET}" >&2
        echo -e "${COLOR_BOLD}${COLOR_CYAN}${separator}${COLOR_RESET}\n" >&2
    else
        echo -e "\n${separator}" >&2
        echo "${message}" >&2
        echo -e "${separator}\n" >&2
    fi
}

# Print a step message
# Usage: log_step "1" "Step description"
log_step() {
    local step_number="$1"
    shift
    local message="$*"

    if supports_color; then
        echo -e "${COLOR_BOLD}${COLOR_CYAN}[Step ${step_number}]${COLOR_RESET} ${message}" >&2
    else
        echo "[Step ${step_number}] ${message}" >&2
    fi
}

# Print a progress message (without newline)
# Usage: log_progress "Processing..."
log_progress() {
    local message="$*"

    if supports_color; then
        echo -ne "${COLOR_DIM}${message}${COLOR_RESET}" >&2
    else
        echo -n "${message}" >&2
    fi
}

# Print a completion message for progress
# Usage: log_progress_done
log_progress_done() {
    if supports_color; then
        echo -e " ${COLOR_GREEN}✓${COLOR_RESET}" >&2
    else
        echo " ✓" >&2
    fi
}

# Print a failure message for progress
# Usage: log_progress_fail
log_progress_fail() {
    if supports_color; then
        echo -e " ${COLOR_RED}✗${COLOR_RESET}" >&2
    else
        echo " ✗" >&2
    fi
}

# Print a command being executed
# Usage: log_command "docker-compose up -d"
log_command() {
    local command="$*"

    if supports_color; then
        echo -e "${COLOR_DIM}$ ${command}${COLOR_RESET}" >&2
    else
        echo "$ ${command}" >&2
    fi
}

# Print a key-value pair
# Usage: log_kv "Key" "Value"
log_kv() {
    local key="$1"
    local value="$2"

    if supports_color; then
        echo -e "  ${COLOR_CYAN}${key}:${COLOR_RESET} ${value}" >&2
    else
        echo "  ${key}: ${value}" >&2
    fi
}

# Print a list item
# Usage: log_item "Item description"
log_item() {
    local message="$*"

    if supports_color; then
        echo -e "  ${COLOR_BLUE}•${COLOR_RESET} ${message}" >&2
    else
        echo "  • ${message}" >&2
    fi
}

# Print an error and exit
# Usage: log_fatal "Fatal error message"
log_fatal() {
    log_error "$@"
    exit 1
}

# Print a banner
# Usage: log_banner "GBMM Platform Setup"
log_banner() {
    local message="$*"
    local width=100
    local padding=$(( (width - ${#message} - 2) / 2 ))
    local line=$(printf '%*s' "$width" '' | tr ' ' '=')
    local padded_message=$(printf '%*s' $padding '')

    if supports_color; then
        echo -e "\n${COLOR_BOLD}${COLOR_GREEN}${line}${COLOR_RESET}" >&2
        echo -e "${COLOR_BOLD}${COLOR_GREEN}${padded_message} ${message}${COLOR_RESET}" >&2
        echo -e "${COLOR_BOLD}${COLOR_GREEN}${line}${COLOR_RESET}\n" >&2
    else
        echo -e "\n${line}" >&2
        echo "${padded_message} ${message}" >&2
        echo -e "${line}\n" >&2
    fi
}

# Confirmation prompt
# Usage: if confirm "Are you sure?"; then ... fi
confirm() {
    local message="$1"
    local default="${2:-n}"

    local prompt
    if [[ "${default}" == "y" ]]; then
        prompt="[Y/n]"
    else
        prompt="[y/N]"
    fi

    if supports_color; then
        echo -ne "${COLOR_YELLOW}❓ ${message} ${prompt}${COLOR_RESET} " >&2
    else
        echo -n "? ${message} ${prompt} " >&2
    fi

    read -r response
    response=${response:-$default}

    case "${response,,}" in
        y|yes) return 0 ;;
        *) return 1 ;;
    esac
}

# Spinner for long-running operations
# Usage:
#   start_spinner "Processing..."
#   long_running_command
#   stop_spinner
SPINNER_PID=""

start_spinner() {
    local message="$1"

    if ! supports_color; then
        log_info "$message"
        return
    fi

    local spinstr='⠋⠙⠹⠸⠼⠴⠦⠧⠇⠏'

    (
        while true; do
            local temp=${spinstr#?}
            printf "\r${COLOR_CYAN}%s${COLOR_RESET} %s " "${spinstr:0:1}" "$message" >&2
            spinstr=$temp${spinstr:0:1}
            sleep 0.1
        done
    ) &

    SPINNER_PID=$!
}

stop_spinner() {
    local success="${1:-true}"

    if [[ -n "$SPINNER_PID" ]]; then
        kill "$SPINNER_PID" 2>/dev/null
        wait "$SPINNER_PID" 2>/dev/null
        SPINNER_PID=""
    fi

    printf "\r" >&2

    if [[ "$success" == "true" ]]; then
        log_progress_done
    else
        log_progress_fail
    fi
}

# Export functions for use in subshells
export -f get_log_level_value
export -f get_timestamp
export -f get_script_name
export -f supports_color
export -f _log
export -f log_debug
export -f log_info
export -f log_success
export -f log_warn
export -f log_error
export -f log_section
export -f log_step
export -f log_progress
export -f log_progress_done
export -f log_progress_fail
export -f log_command
export -f log_kv
export -f log_item
export -f log_fatal
export -f log_banner
export -f confirm
export -f start_spinner
export -f stop_spinner
