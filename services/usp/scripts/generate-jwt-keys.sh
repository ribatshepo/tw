#!/bin/bash
set -euo pipefail

# Script: generate-jwt-keys.sh
# Purpose: Generate RSA key pair for JWT signing (RS256 algorithm)
# Usage: ./generate-jwt-keys.sh [output_directory]
#
# Examples:
#   ./generate-jwt-keys.sh              # Generates in ./keys
#   ./generate-jwt-keys.sh /etc/usp/keys  # Generates in /etc/usp/keys

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Error handling
trap 'echo -e "${RED}Error occurred on line $LINENO${NC}" >&2; cleanup; exit 1' ERR
trap 'cleanup; exit 130' INT TERM

# Temporary files for cleanup
TEMP_FILES=()

# Cleanup function
cleanup() {
    for file in "${TEMP_FILES[@]}"; do
        if [[ -f "$file" ]]; then
            rm -f "$file"
        fi
    done
}

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
info "Checking dependencies..."
if ! command_exists openssl; then
    error "openssl is not installed. Please install it first."
    echo "  - Ubuntu/Debian: sudo apt-get install openssl"
    echo "  - RHEL/CentOS:   sudo yum install openssl"
    echo "  - macOS:         brew install openssl"
    exit 1
fi

# Determine output directory
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
KEY_DIR="${1:-$SCRIPT_DIR/../keys}"

# Validate or create directory
if [[ ! -d "$KEY_DIR" ]]; then
    info "Creating directory: $KEY_DIR"
    mkdir -p "$KEY_DIR"
fi

# Define key file paths
PRIVATE_KEY="$KEY_DIR/jwt-private.pem"
PUBLIC_KEY="$KEY_DIR/jwt-public.pem"

# Check if keys already exist
if [[ -f "$PRIVATE_KEY" ]] || [[ -f "$PUBLIC_KEY" ]]; then
    warn "JWT keys already exist in $KEY_DIR"
    echo "  Private key: ${PRIVATE_KEY}"
    echo "  Public key:  ${PUBLIC_KEY}"
    echo
    read -p "Do you want to overwrite the existing keys? (y/N): " -n 1 -r
    echo
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        info "Operation cancelled. Existing keys were not modified."
        exit 0
    fi

    # Backup existing keys
    if [[ -f "$PRIVATE_KEY" ]]; then
        BACKUP_PRIVATE="${PRIVATE_KEY}.backup.$(date +%Y%m%d%H%M%S)"
        info "Backing up existing private key to: $BACKUP_PRIVATE"
        cp "$PRIVATE_KEY" "$BACKUP_PRIVATE"
    fi
    if [[ -f "$PUBLIC_KEY" ]]; then
        BACKUP_PUBLIC="${PUBLIC_KEY}.backup.$(date +%Y%m%d%H%M%S)"
        info "Backing up existing public key to: $BACKUP_PUBLIC"
        cp "$PUBLIC_KEY" "$BACKUP_PUBLIC"
    fi
fi

info "Generating RSA 2048-bit key pair for JWT signing..."

# Generate private key
if ! openssl genrsa -out "$PRIVATE_KEY" 2048 2>/dev/null; then
    error "Failed to generate private key"
    exit 1
fi
TEMP_FILES+=("$PRIVATE_KEY")

# Extract public key
if ! openssl rsa -in "$PRIVATE_KEY" -pubout -out "$PUBLIC_KEY" 2>/dev/null; then
    error "Failed to extract public key"
    rm -f "$PRIVATE_KEY"
    exit 1
fi
TEMP_FILES+=("$PUBLIC_KEY")

# Set secure permissions
chmod 600 "$PRIVATE_KEY"
chmod 644 "$PUBLIC_KEY"

# Verify keys were generated correctly
if ! openssl rsa -in "$PRIVATE_KEY" -check -noout 2>/dev/null; then
    error "Generated private key is invalid"
    cleanup
    exit 1
fi

# Clear temp files list (we want to keep the keys)
TEMP_FILES=()

success "JWT keys generated successfully!"
echo
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "  Key Details:"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "  Private key: $PRIVATE_KEY"
echo "  Public key:  $PUBLIC_KEY"
echo "  Algorithm:   RS256 (RSA with SHA-256)"
echo "  Key size:    2048 bits"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo
warn "SECURITY IMPORTANT:"
echo
echo "  ⚠️  The private key MUST be kept secure!"
echo "     - Never commit to version control"
echo "     - Restrict file permissions (already set to 600)"
echo "     - Store in a secure location (e.g., /etc/usp/keys)"
echo "     - Use a secrets manager in production"
echo
echo "  🔐 File Permissions:"
echo "     - Private key: 600 (read/write owner only)"
echo "     - Public key:  644 (read all, write owner)"
echo
info "Configuration:"
echo
echo "Update your appsettings.json or environment variables:"
echo
echo '  "Jwt": {'
echo '    "PrivateKeyPath": "'"$PRIVATE_KEY"'",'
echo '    "PublicKeyPath": "'"$PUBLIC_KEY"'",'
echo '    "Algorithm": "RS256"'
echo '  }'
echo
echo "Or set environment variables:"
echo "  export USP_Jwt__PrivateKeyPath=\"$PRIVATE_KEY\""
echo "  export USP_Jwt__PublicKeyPath=\"$PUBLIC_KEY\""
echo
info "JWT keys are ready for use!"
