#!/bin/bash
# Generate a secure 256-bit master key for USP encryption service

echo "Generating 256-bit (32-byte) master encryption key..."

# Generate random key and encode as base64
MASTER_KEY=$(openssl rand -base64 32)

echo ""
echo "==================================================================="
echo "MASTER ENCRYPTION KEY (CRITICAL - BACKUP SECURELY)"
echo "==================================================================="
echo ""
echo "Add this to your environment or configuration:"
echo ""
echo "ENCRYPTION__MASTER_KEY=${MASTER_KEY}"
echo ""
echo "==================================================================="
echo "WARNING: Store this key securely. All encrypted data depends on it."
echo "If this key is lost, ALL encrypted secrets will be unrecoverable."
echo "==================================================================="
echo ""
