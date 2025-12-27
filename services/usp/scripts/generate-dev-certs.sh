#!/bin/bash
set -euo pipefail

# Script: generate-dev-certs.sh
# Purpose: Generate self-signed TLS certificates for USP development environment
# Usage: ./generate-dev-certs.sh [output-dir]

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

# Validate dependencies
info "Checking dependencies..."
if ! command_exists openssl; then
    error "openssl is not installed. Please install it first."
    exit 1
fi

# Determine output directory
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
CERTS_DIR="${1:-$SCRIPT_DIR/../src/USP.API/certs}"

# Create certs directory if it doesn't exist
mkdir -p "$CERTS_DIR"

# Certificate configuration
CERT_SUBJECT="/C=US/ST=State/L=City/O=Development/OU=USP/CN=localhost"
CERT_DAYS=365
CERT_PASSWORD="dev-cert-password"

info "Generating certificates in: $CERTS_DIR"

# Check if certificates already exist
if [[ -f "$CERTS_DIR/usp-dev.pfx" ]]; then
    warn "Certificate already exists at $CERTS_DIR/usp-dev.pfx"
    read -p "Do you want to overwrite it? (y/N): " -n 1 -r
    echo
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        info "Skipping certificate generation."
        exit 0
    fi
    rm -f "$CERTS_DIR/usp-dev.pfx" "$CERTS_DIR/usp-dev.key" "$CERTS_DIR/usp-dev.crt"
fi

# Generate private key
info "Generating private key..."
openssl genrsa -out "$CERTS_DIR/usp-dev.key" 2048

# Generate certificate signing request (CSR)
info "Generating certificate signing request..."
openssl req -new -key "$CERTS_DIR/usp-dev.key" \
    -out "$CERTS_DIR/usp-dev.csr" \
    -subj "$CERT_SUBJECT"

# Create config file for SAN (Subject Alternative Names)
cat > "$CERTS_DIR/usp-dev.cnf" <<EOF
[req]
default_bits = 2048
distinguished_name = req_distinguished_name
req_extensions = v3_req
prompt = no

[req_distinguished_name]
C = US
ST = State
L = City
O = Development
OU = USP
CN = localhost

[v3_req]
keyUsage = keyEncipherment, dataEncipherment, digitalSignature
extendedKeyUsage = serverAuth, clientAuth
subjectAltName = @alt_names

[alt_names]
DNS.1 = localhost
DNS.2 = *.localhost
DNS.3 = usp
DNS.4 = usp.local
DNS.5 = *.usp.local
IP.1 = 127.0.0.1
IP.2 = ::1
EOF

# Generate self-signed certificate
info "Generating self-signed certificate..."
openssl x509 -req -days $CERT_DAYS \
    -in "$CERTS_DIR/usp-dev.csr" \
    -signkey "$CERTS_DIR/usp-dev.key" \
    -out "$CERTS_DIR/usp-dev.crt" \
    -extensions v3_req \
    -extfile "$CERTS_DIR/usp-dev.cnf"

# Convert to PKCS12/PFX format (required by Kestrel)
info "Converting to PFX format..."
openssl pkcs12 -export \
    -out "$CERTS_DIR/usp-dev.pfx" \
    -inkey "$CERTS_DIR/usp-dev.key" \
    -in "$CERTS_DIR/usp-dev.crt" \
    -password "pass:$CERT_PASSWORD"

# Set secure file permissions
chmod 600 "$CERTS_DIR/usp-dev.key"
chmod 600 "$CERTS_DIR/usp-dev.pfx"
chmod 644 "$CERTS_DIR/usp-dev.crt"

# Clean up temporary files
rm -f "$CERTS_DIR/usp-dev.csr" "$CERTS_DIR/usp-dev.cnf"

# Display certificate information
info "Certificate generated successfully!"
echo
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "  Certificate Details:"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "  PFX File:     $CERTS_DIR/usp-dev.pfx"
echo "  Private Key:  $CERTS_DIR/usp-dev.key"
echo "  Certificate:  $CERTS_DIR/usp-dev.crt"
echo "  Password:     $CERT_PASSWORD"
echo "  Valid for:    $CERT_DAYS days"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo
warn "This is a DEVELOPMENT certificate. Do NOT use in production!"
echo
info "To trust this certificate on your system:"
echo "  - Linux:   sudo cp $CERTS_DIR/usp-dev.crt /usr/local/share/ca-certificates/ && sudo update-ca-certificates"
echo "  - macOS:   sudo security add-trusted-cert -d -r trustRoot -k /Library/Keychains/System.keychain $CERTS_DIR/usp-dev.crt"
echo "  - Windows: Import $CERTS_DIR/usp-dev.crt into Trusted Root Certification Authorities"
echo
info "USP will be accessible at:"
echo "  - Primary API:  https://localhost:8443"
echo "  - Admin API:    https://localhost:5001"
echo "  - gRPC API:     https://localhost:50005"
echo "  - Metrics:      http://localhost:9090/metrics"
echo
