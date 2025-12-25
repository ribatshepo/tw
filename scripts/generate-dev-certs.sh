#!/usr/bin/env bash

set -euo pipefail

# ================================================================================================
# Generate Development mTLS Certificates
# ================================================================================================
# Generates CA and service certificates for mTLS in development
# - CA certificate (4096-bit RSA, 10-year validity)
# - Service certificates (2048-bit RSA, 1-year validity)
# - Proper SAN (Subject Alternative Names)
# - Idempotent (checks existing certificates)
# ================================================================================================

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

# Source logging functions
source "$SCRIPT_DIR/helpers/logging.sh"

# Certificate directory
CERTS_DIR="${PROJECT_ROOT}/certs"

# Certificate validity periods
CA_VALIDITY_DAYS=3650  # 10 years
SERVICE_VALIDITY_DAYS=365  # 1 year

# Services to generate certificates for
SERVICES=("uccp" "nccs" "usp" "udps" "stream")

# ================================================================================================
# HELPER FUNCTIONS
# ================================================================================================

# Check if a certificate exists and is valid
check_cert_valid() {
    local cert_file="$1"
    local min_days_remaining="${2:-30}"

    if [[ ! -f "$cert_file" ]]; then
        return 1
    fi

    # Check if certificate is still valid
    if ! openssl x509 -in "$cert_file" -noout -checkend $((min_days_remaining * 86400)) >/dev/null 2>&1; then
        return 1
    fi

    return 0
}

# Get certificate expiry date
get_cert_expiry() {
    local cert_file="$1"

    if [[ ! -f "$cert_file" ]]; then
        echo "N/A"
        return
    fi

    openssl x509 -in "$cert_file" -noout -enddate 2>/dev/null | cut -d= -f2 || echo "N/A"
}

# Get certificate subject
get_cert_subject() {
    local cert_file="$1"

    if [[ ! -f "$cert_file" ]]; then
        echo "N/A"
        return
    fi

    openssl x509 -in "$cert_file" -noout -subject 2>/dev/null | sed 's/subject=//' || echo "N/A"
}

# ================================================================================================
# CERTIFICATE GENERATION
# ================================================================================================

# Generate CA certificate
generate_ca_cert() {
    local ca_key="${CERTS_DIR}/ca.key"
    local ca_cert="${CERTS_DIR}/ca.crt"

    log_step "1" "Generating CA Certificate"

    # Check if CA cert exists and is valid
    if check_cert_valid "$ca_cert" 30; then
        log_info "CA certificate already exists and is valid"
        log_kv "Certificate" "$ca_cert"
        log_kv "Expires" "$(get_cert_expiry "$ca_cert")"
        return 0
    fi

    log_info "Generating new CA certificate..."

    # Generate CA private key
    log_progress "Generating CA private key (4096-bit RSA)"
    openssl genrsa -out "$ca_key" 4096 >/dev/null 2>&1
    chmod 600 "$ca_key"
    log_progress_done

    # Generate CA certificate
    log_progress "Generating CA certificate (10-year validity)"
    openssl req -new -x509 \
        -days "$CA_VALIDITY_DAYS" \
        -key "$ca_key" \
        -out "$ca_cert" \
        -subj "/C=US/ST=Development/L=Development/O=GBMM Platform/OU=Development/CN=GBMM-CA" \
        >/dev/null 2>&1
    chmod 644 "$ca_cert"
    log_progress_done

    log_success "CA certificate generated"
    log_kv "Certificate" "$ca_cert"
    log_kv "Key" "$ca_key"
    log_kv "Validity" "$CA_VALIDITY_DAYS days (10 years)"
    log_kv "Expires" "$(get_cert_expiry "$ca_cert")"

    echo ""
}

# Generate service certificate
generate_service_cert() {
    local service_name="$1"
    local ca_key="${CERTS_DIR}/ca.key"
    local ca_cert="${CERTS_DIR}/ca.crt"
    local service_key="${CERTS_DIR}/${service_name}.key"
    local service_csr="${CERTS_DIR}/${service_name}.csr"
    local service_cert="${CERTS_DIR}/${service_name}.crt"
    local service_ext="${CERTS_DIR}/${service_name}.ext"

    log_step "2.${service_name}" "Generating Certificate for ${service_name^^}"

    # Check if service cert exists and is valid
    if check_cert_valid "$service_cert" 30; then
        log_info "Certificate for ${service_name} already exists and is valid"
        log_kv "Certificate" "$service_cert"
        log_kv "Expires" "$(get_cert_expiry "$service_cert")"
        return 0
    fi

    log_info "Generating new certificate for ${service_name}..."

    # Generate service private key
    log_progress "Generating private key for ${service_name} (2048-bit RSA)"
    openssl genrsa -out "$service_key" 2048 >/dev/null 2>&1
    chmod 600 "$service_key"
    log_progress_done

    # Generate CSR
    log_progress "Generating CSR for ${service_name}"
    openssl req -new \
        -key "$service_key" \
        -out "$service_csr" \
        -subj "/C=US/ST=Development/L=Development/O=GBMM Platform/OU=Services/CN=${service_name}" \
        >/dev/null 2>&1
    log_progress_done

    # Create extensions file for SAN
    cat > "$service_ext" <<EOF
authorityKeyIdentifier=keyid,issuer
basicConstraints=CA:FALSE
keyUsage = digitalSignature, nonRepudiation, keyEncipherment, dataEncipherment
subjectAltName = @alt_names

[alt_names]
DNS.1 = ${service_name}
DNS.2 = ${service_name}.service.local
DNS.3 = ${service_name}.gbmm.local
DNS.4 = localhost
IP.1 = 127.0.0.1
IP.2 = 10.0.10.10
EOF

    # Sign certificate with CA
    log_progress "Signing certificate for ${service_name}"
    openssl x509 -req \
        -in "$service_csr" \
        -CA "$ca_cert" \
        -CAkey "$ca_key" \
        -CAcreateserial \
        -out "$service_cert" \
        -days "$SERVICE_VALIDITY_DAYS" \
        -sha256 \
        -extfile "$service_ext" \
        >/dev/null 2>&1
    chmod 644 "$service_cert"
    log_progress_done

    # Clean up CSR and extensions file
    rm -f "$service_csr" "$service_ext"

    log_success "Certificate generated for ${service_name}"
    log_kv "Certificate" "$service_cert"
    log_kv "Key" "$service_key"
    log_kv "Validity" "$SERVICE_VALIDITY_DAYS days (1 year)"
    log_kv "Expires" "$(get_cert_expiry "$service_cert")"

    echo ""
}

# Generate client certificate (for testing)
generate_client_cert() {
    local ca_key="${CERTS_DIR}/ca.key"
    local ca_cert="${CERTS_DIR}/ca.crt"
    local client_key="${CERTS_DIR}/client.key"
    local client_csr="${CERTS_DIR}/client.csr"
    local client_cert="${CERTS_DIR}/client.crt"
    local client_ext="${CERTS_DIR}/client.ext"

    log_step "3" "Generating Client Certificate (for testing)"

    # Check if client cert exists and is valid
    if check_cert_valid "$client_cert" 30; then
        log_info "Client certificate already exists and is valid"
        log_kv "Certificate" "$client_cert"
        log_kv "Expires" "$(get_cert_expiry "$client_cert")"
        return 0
    fi

    log_info "Generating new client certificate..."

    # Generate client private key
    log_progress "Generating client private key (2048-bit RSA)"
    openssl genrsa -out "$client_key" 2048 >/dev/null 2>&1
    chmod 600 "$client_key"
    log_progress_done

    # Generate CSR
    log_progress "Generating CSR for client"
    openssl req -new \
        -key "$client_key" \
        -out "$client_csr" \
        -subj "/C=US/ST=Development/L=Development/O=GBMM Platform/OU=Clients/CN=gbmm-client" \
        >/dev/null 2>&1
    log_progress_done

    # Create extensions file
    cat > "$client_ext" <<EOF
authorityKeyIdentifier=keyid,issuer
basicConstraints=CA:FALSE
keyUsage = digitalSignature, nonRepudiation, keyEncipherment
extendedKeyUsage = clientAuth
EOF

    # Sign certificate with CA
    log_progress "Signing client certificate"
    openssl x509 -req \
        -in "$client_csr" \
        -CA "$ca_cert" \
        -CAkey "$ca_key" \
        -CAcreateserial \
        -out "$client_cert" \
        -days "$SERVICE_VALIDITY_DAYS" \
        -sha256 \
        -extfile "$client_ext" \
        >/dev/null 2>&1
    chmod 644 "$client_cert"
    log_progress_done

    # Clean up CSR and extensions file
    rm -f "$client_csr" "$client_ext"

    log_success "Client certificate generated"
    log_kv "Certificate" "$client_cert"
    log_kv "Key" "$client_key"
    log_kv "Validity" "$SERVICE_VALIDITY_DAYS days (1 year)"
    log_kv "Expires" "$(get_cert_expiry "$client_cert")"

    echo ""
}

# ================================================================================================
# MAIN
# ================================================================================================

main() {
    log_banner "GBMM Platform - mTLS Certificate Generation"

    # Check prerequisites
    log_section "Prerequisites Check"

    if ! command -v openssl >/dev/null 2>&1; then
        log_error "OpenSSL is not installed"
        log_info "Please install OpenSSL: sudo apt-get install openssl"
        exit 1
    fi

    log_success "OpenSSL is installed: $(openssl version)"
    echo ""

    # Create certs directory
    log_section "Directory Setup"

    if [[ ! -d "$CERTS_DIR" ]]; then
        log_info "Creating certificates directory: $CERTS_DIR"
        mkdir -p "$CERTS_DIR"
    fi

    log_success "Certificates directory ready: $CERTS_DIR"
    echo ""

    # Generate CA certificate
    log_section "CA Certificate Generation"
    generate_ca_cert

    # Generate service certificates
    log_section "Service Certificate Generation"
    for service in "${SERVICES[@]}"; do
        generate_service_cert "$service"
    done

    # Generate client certificate
    log_section "Client Certificate Generation"
    generate_client_cert

    # Summary
    log_section "Certificate Summary"

    log_info "Generated Certificates:"
    log_item "CA: ${CERTS_DIR}/ca.crt (expires: $(get_cert_expiry "${CERTS_DIR}/ca.crt"))"

    for service in "${SERVICES[@]}"; do
        log_item "${service^^}: ${CERTS_DIR}/${service}.crt (expires: $(get_cert_expiry "${CERTS_DIR}/${service}.crt"))"
    done

    log_item "Client: ${CERTS_DIR}/client.crt (expires: $(get_cert_expiry "${CERTS_DIR}/client.crt"))"

    echo ""
    log_success "All certificates generated successfully!"
    echo ""

    log_info "Next steps:"
    log_item "Certificates are in: $CERTS_DIR"
    log_item "Services will use these certificates for mTLS"
    log_item "Use client.crt and client.key for testing"
    echo ""

    log_warn "Important: These are development certificates only!"
    log_warn "DO NOT use these certificates in production!"
    echo ""
}

main "$@"
