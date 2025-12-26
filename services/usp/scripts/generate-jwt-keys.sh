#!/bin/bash
# Generate RSA key pair for JWT signing
# Usage: ./generate-jwt-keys.sh [output_directory]
#
# Example:
#   ./generate-jwt-keys.sh ./keys
#   ./generate-jwt-keys.sh /etc/usp/keys

set -e

KEY_DIR="${1:-./keys}"

# Create directory if it doesn't exist
mkdir -p "$KEY_DIR"

echo "Generating RSA 2048-bit key pair for JWT signing..."

# Generate private key
openssl genrsa -out "$KEY_DIR/jwt-private.pem" 2048

# Extract public key
openssl rsa -in "$KEY_DIR/jwt-private.pem" -pubout -out "$KEY_DIR/jwt-public.pem"

# Set secure permissions
chmod 600 "$KEY_DIR/jwt-private.pem"
chmod 644 "$KEY_DIR/jwt-public.pem"

echo ""
echo "✓ Keys generated successfully:"
echo "  Private: $KEY_DIR/jwt-private.pem"
echo "  Public:  $KEY_DIR/jwt-public.pem"
echo ""
echo "IMPORTANT:"
echo "  - Keep the private key secure and never commit it to version control"
echo "  - Set JWT_PRIVATE_KEY_PATH environment variable to point to jwt-private.pem"
echo "  - Set JWT_PUBLIC_KEY_PATH environment variable to point to jwt-public.pem"
echo ""
echo "Example environment configuration:"
echo "  export JWT_PRIVATE_KEY_PATH=\"$KEY_DIR/jwt-private.pem\""
echo "  export JWT_PUBLIC_KEY_PATH=\"$KEY_DIR/jwt-public.pem\""
