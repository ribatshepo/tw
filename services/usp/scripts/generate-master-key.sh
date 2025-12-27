#!/bin/bash
set -euo pipefail

# Script: generate-master-key.sh
# Purpose: Generate a secure 256-bit (32-byte) master encryption key for USP
# Usage: ./generate-master-key.sh [--output .env] [--append]
#
# Options:
#   --output FILE   Write key to specified file (default: stdout only)
#   --append        Append to file instead of overwriting
#
# Examples:
#   ./generate-master-key.sh                    # Display key only
#   ./generate-master-key.sh --output .env      # Write to .env (overwrite)
#   ./generate-master-key.sh --output .env --append  # Append to .env

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Error handling
trap 'echo -e "${RED}Error occurred on line $LINENO${NC}" >&2; exit 1' ERR

# Function to check if command exists
command_exists() {
    command -v "$1" >/dev/null 2>&1
}

# Function to print colored messages
info() {
    echo -e "${GREEN}[INFO]${NC} $1"
}

warn() {
    echo -e "${YELLOW}[WARN]${NC} $1"
}

error() {
    echo -e "${RED}[ERROR]${NC} $1" >&2
}

success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

# Validate dependencies
if ! command_exists openssl; then
    error "openssl is not installed. Please install it first."
    echo "  - Ubuntu/Debian: sudo apt-get install openssl"
    echo "  - RHEL/CentOS:   sudo yum install openssl"
    echo "  - macOS:         brew install openssl"
    exit 1
fi

# Parse command line arguments
OUTPUT_FILE=""
APPEND_MODE=false

while [[ $# -gt 0 ]]; do
    case $1 in
        --output)
            OUTPUT_FILE="$2"
            shift 2
            ;;
        --append)
            APPEND_MODE=true
            shift
            ;;
        *)
            error "Unknown option: $1"
            echo "Usage: $0 [--output FILE] [--append]"
            exit 1
            ;;
    esac
done

info "Generating 256-bit (32-byte) master encryption key..."

# Generate random key and encode as base64
MASTER_KEY=$(openssl rand -base64 32)

# Verify key was generated
if [[ -z "$MASTER_KEY" ]]; then
    error "Failed to generate master key"
    exit 1
fi

# Display key to user
echo
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "  MASTER ENCRYPTION KEY (CRITICAL - BACKUP SECURELY!)"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo
echo "  USP_Encryption__MasterKey=$MASTER_KEY"
echo
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo

# Write to file if specified
if [[ -n "$OUTPUT_FILE" ]]; then
    # Check if file exists when not in append mode
    if [[ -f "$OUTPUT_FILE" ]] && [[ "$APPEND_MODE" == false ]]; then
        warn "File $OUTPUT_FILE already exists!"
        read -p "Do you want to overwrite it? (y/N): " -n 1 -r
        echo
        if [[ ! $REPLY =~ ^[Yy]$ ]]; then
            warn "Operation cancelled. File not modified."
            warn "Use --append flag to add to existing file."
            exit 0
        fi
    fi

    # Write or append to file
    if [[ "$APPEND_MODE" == true ]]; then
        echo "USP_Encryption__MasterKey=$MASTER_KEY" >> "$OUTPUT_FILE"
        success "Master key appended to: $OUTPUT_FILE"
    else
        echo "# USP Master Encryption Key" > "$OUTPUT_FILE"
        echo "# Generated: $(date -u +"%Y-%m-%d %H:%M:%S UTC")" >> "$OUTPUT_FILE"
        echo "# WARNING: Keep this file secure! All encrypted data depends on this key." >> "$OUTPUT_FILE"
        echo "USP_Encryption__MasterKey=$MASTER_KEY" >> "$OUTPUT_FILE"
        chmod 600 "$OUTPUT_FILE"
        success "Master key written to: $OUTPUT_FILE"
        info "File permissions set to 600 (read/write owner only)"
    fi
    echo
fi

# Display warnings and instructions
warn "⚠️  CRITICAL SECURITY WARNINGS:"
echo
echo "  1. BACKUP THIS KEY IMMEDIATELY!"
echo "     - Store in a secure location (password manager, hardware security module)"
echo "     - Keep multiple secure backups"
echo "     - Document the backup locations"
echo
echo "  2. PROTECT THIS KEY:"
echo "     - Never commit to version control"
echo "     - Never email or send via unsecured channels"
echo "     - Restrict access to authorized personnel only"
echo "     - Use a secrets management solution in production"
echo
echo "  3. KEY RECOVERY:"
echo "     - Without this key, ALL encrypted secrets are UNRECOVERABLE"
echo "     - There is NO way to recover encrypted data if key is lost"
echo "     - Losing this key means losing all encrypted secrets permanently"
echo
echo "  4. KEY ROTATION:"
echo "     - Plan for regular key rotation (e.g., annually)"
echo "     - Document key rotation procedures"
echo "     - Re-encrypt all secrets when rotating keys"
echo
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo

info "Usage in configuration:"
echo
echo "Add to your .env file or environment:"
echo "  USP_Encryption__MasterKey=$MASTER_KEY"
echo
echo "Or in appsettings.json (NOT recommended for production):"
echo '  "Encryption": {'
echo '    "MasterKey": "'"$MASTER_KEY"'"'
echo '  }'
echo
info "Master key generated successfully!"
echo
warn "Remember to backup this key in a secure location immediately!"
