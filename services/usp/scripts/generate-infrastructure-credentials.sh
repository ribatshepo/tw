#!/bin/bash
set -euo pipefail

# Script: generate-infrastructure-credentials.sh
# Purpose: Generate secure random passwords for infrastructure components
# Usage: ./generate-infrastructure-credentials.sh [--output .env]

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
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

# Function to generate secure random password
generate_password() {
    local length="${1:-32}"
    openssl rand -base64 48 | tr -d "=+/" | cut -c1-${length}
}

# Function to generate base64 key
generate_base64_key() {
    local bytes="${1:-32}"
    openssl rand -base64 ${bytes}
}

# Validate dependencies
info "Checking dependencies..."
if ! command_exists openssl; then
    error "openssl is not installed. Please install it first."
    exit 1
fi

# Determine output file
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
OUTPUT_FILE="${1:-$SCRIPT_DIR/../.env}"

# Check if .env already exists
if [[ -f "$OUTPUT_FILE" ]]; then
    warn "File $OUTPUT_FILE already exists!"
    read -p "Do you want to overwrite it? (y/N): " -n 1 -r
    echo
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        error "Aborted. Existing file not modified."
        exit 1
    fi
fi

info "Generating secure random credentials..."
echo

# Generate credentials
DB_PASSWORD=$(generate_password 32)
REDIS_PASSWORD=$(generate_password 32)
MASTER_KEY=$(generate_base64_key 32)
PRIMARY_CERT_PASSWORD=$(generate_password 24)
ADMIN_CERT_PASSWORD=$(generate_password 24)
GRPC_CERT_PASSWORD=$(generate_password 24)
ELASTICSEARCH_PASSWORD=$(generate_password 32)
RABBITMQ_PASSWORD=$(generate_password 32)

# Create .env file
info "Writing credentials to: $OUTPUT_FILE"

cat > "$OUTPUT_FILE" <<EOF
# ============================================================================
# USP (Unified Security Platform) - Bootstrap Credentials
# ============================================================================
# Generated: $(date -u +"%Y-%m-%d %H:%M:%S UTC")
# WARNING: Keep this file secure! Contains sensitive credentials.
# ============================================================================

# ----------------------------------------------------------------------------
# Database Configuration (PostgreSQL)
# ----------------------------------------------------------------------------
USP_Database__Host=localhost
USP_Database__Port=5432
USP_Database__Database=usp_db
USP_Database__Username=usp_app
USP_Database__Password=$DB_PASSWORD

# ----------------------------------------------------------------------------
# Redis Configuration
# ----------------------------------------------------------------------------
USP_Redis__Host=localhost
USP_Redis__Port=6379
USP_Redis__Password=$REDIS_PASSWORD

# ----------------------------------------------------------------------------
# Master Encryption Key
# ----------------------------------------------------------------------------
# CRITICAL: Back this up securely! Without it, encrypted data cannot be recovered.
USP_Encryption__MasterKey=$MASTER_KEY

# ----------------------------------------------------------------------------
# TLS Certificates
# ----------------------------------------------------------------------------
# Primary API (port 8443)
USP_Certificates__Primary__Path=/etc/usp/certs/usp-primary.pfx
USP_Certificates__Primary__Password=$PRIMARY_CERT_PASSWORD

# Admin API (port 5001)
USP_Certificates__Admin__Path=/etc/usp/certs/usp-admin.pfx
USP_Certificates__Admin__Password=$ADMIN_CERT_PASSWORD

# gRPC API (port 50005)
USP_Certificates__Grpc__Path=/etc/usp/certs/usp-grpc.pfx
USP_Certificates__Grpc__Password=$GRPC_CERT_PASSWORD

# ----------------------------------------------------------------------------
# Observability
# ----------------------------------------------------------------------------
USP_Elasticsearch__Username=elastic
USP_Elasticsearch__Password=$ELASTICSEARCH_PASSWORD

# ----------------------------------------------------------------------------
# RabbitMQ Configuration
# ----------------------------------------------------------------------------
USP_RabbitMQ__Host=localhost
USP_RabbitMQ__Port=5672
USP_RabbitMQ__Username=usp
USP_RabbitMQ__Password=$RABBITMQ_PASSWORD

# ----------------------------------------------------------------------------
# Application Configuration
# ----------------------------------------------------------------------------
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=https://+:8443;https://+:5001;https://+:50005;http://+:9090

# ============================================================================
# END OF BOOTSTRAP CREDENTIALS
# ============================================================================
EOF

# Set secure file permissions
chmod 600 "$OUTPUT_FILE"

success "Bootstrap credentials generated successfully!"
echo
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo -e "  ${BLUE}Credentials Summary${NC}"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo
echo "  📄 File:              $OUTPUT_FILE"
echo "  🔒 Permissions:       600 (read/write owner only)"
echo "  📊 Generated values:  8 credentials"
echo
echo "  Database Password:    ${DB_PASSWORD:0:8}... (32 chars)"
echo "  Redis Password:       ${REDIS_PASSWORD:0:8}... (32 chars)"
echo "  Master Key:           ${MASTER_KEY:0:12}... (base64)"
echo "  Primary Cert Pass:    ${PRIMARY_CERT_PASSWORD:0:8}... (24 chars)"
echo "  Admin Cert Pass:      ${ADMIN_CERT_PASSWORD:0:8}... (24 chars)"
echo "  gRPC Cert Pass:       ${GRPC_CERT_PASSWORD:0:8}... (24 chars)"
echo "  Elasticsearch Pass:   ${ELASTICSEARCH_PASSWORD:0:8}... (32 chars)"
echo "  RabbitMQ Pass:        ${RABBITMQ_PASSWORD:0:8}... (32 chars)"
echo
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo
warn "IMPORTANT SECURITY NOTES:"
echo
echo "  1. ⚠️  Back up the master encryption key immediately!"
echo "     Without it, all encrypted secrets will be unrecoverable."
echo
echo "  2. 🔐 Store this .env file securely:"
echo "     - Never commit to version control"
echo "     - Restrict file permissions (already set to 600)"
echo "     - Use a secrets management solution in production"
echo
echo "  3. 🔄 Rotate these credentials regularly:"
echo "     - Database password: quarterly"
echo "     - Redis password: quarterly"
echo "     - Certificate passwords: when rotating certificates"
echo
echo "  4. 📋 Next steps:"
echo "     - Generate TLS certificates: ./scripts/generate-dev-certs.sh"
echo "     - Initialize database: ./scripts/migrate-databases.sh"
echo "     - Start USP service"
echo "     - Initialize secrets: ./scripts/init-secrets.sh"
echo
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo
info "Bootstrap credentials are ready!"
