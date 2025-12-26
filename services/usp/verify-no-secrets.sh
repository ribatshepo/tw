#!/bin/bash

# USP Service - Verify No Hardcoded Secrets Script
# This script verifies that no hardcoded secrets exist in the codebase

set -e

echo "========================================="
echo "USP Service - No Hardcoded Secrets Check"
echo "========================================="
echo ""

# Define prohibited patterns
PROHIBITED_PATTERNS=(
    "Password=changeme"
    "Password=password123"
    "Password=admin123"
    "Password=\"changeme\""
    "Secret.*your-super-secret"
    "Secret.*development-secret"
    "guest.*guest"
)

# Directories to search
SEARCH_DIRS="src/"

# Files to check
FILES_TO_CHECK=(
    "src/USP.Api/appsettings.json"
    "src/USP.Api/Program.cs"
    "src/USP.Infrastructure/Data/ApplicationDbContextFactory.cs"
)

VIOLATIONS_FOUND=0

echo "Checking for prohibited patterns in source files..."
echo ""

# Check each prohibited pattern
for pattern in "${PROHIBITED_PATTERNS[@]}"; do
    echo -n "  Checking for: $pattern ... "

    # Search for pattern, excluding validator and documentation files
    MATCHES=$(grep -r "$pattern" $SEARCH_DIRS \
        --include="*.cs" \
        --include="*.json" \
        --exclude-dir=bin \
        --exclude-dir=obj \
        --exclude="*ConfigurationValidator.cs" \
        --exclude="*Settings.cs" \
        --exclude="*.example" \
        --exclude="*.md" \
        2>/dev/null || true)

    if [ -n "$MATCHES" ]; then
        echo "FOUND!"
        echo "$MATCHES"
        VIOLATIONS_FOUND=$((VIOLATIONS_FOUND + 1))
    else
        echo "OK"
    fi
done

echo ""
echo "Checking specific files for empty secret values..."
echo ""

# Check appsettings.json
echo -n "  appsettings.json - Database:Password ... "
DB_PASS=$(jq -r '.Database.Password' src/USP.Api/appsettings.json 2>/dev/null || echo "")
if [ "$DB_PASS" = "" ]; then
    echo "OK (empty)"
else
    echo "VIOLATION: Contains value '$DB_PASS'"
    VIOLATIONS_FOUND=$((VIOLATIONS_FOUND + 1))
fi

echo -n "  appsettings.json - Jwt:Secret ... "
JWT_SECRET=$(jq -r '.Jwt.Secret' src/USP.Api/appsettings.json 2>/dev/null || echo "")
if [ "$JWT_SECRET" = "" ]; then
    echo "OK (empty)"
else
    echo "VIOLATION: Contains value (length: ${#JWT_SECRET})"
    VIOLATIONS_FOUND=$((VIOLATIONS_FOUND + 1))
fi

echo -n "  appsettings.json - Redis:Password ... "
REDIS_PASS=$(jq -r '.Redis.Password' src/USP.Api/appsettings.json 2>/dev/null || echo "")
if [ "$REDIS_PASS" = "" ]; then
    echo "OK (empty)"
else
    echo "VIOLATION: Contains value '$REDIS_PASS'"
    VIOLATIONS_FOUND=$((VIOLATIONS_FOUND + 1))
fi

echo -n "  appsettings.json - RabbitMQ:Password ... "
RABBITMQ_PASS=$(jq -r '.RabbitMQ.Password' src/USP.Api/appsettings.json 2>/dev/null || echo "")
if [ "$RABBITMQ_PASS" = "" ]; then
    echo "OK (empty)"
else
    echo "VIOLATION: Contains value '$RABBITMQ_PASS'"
    VIOLATIONS_FOUND=$((VIOLATIONS_FOUND + 1))
fi

echo ""
echo "Verifying appsettings.Development.json is deleted..."
echo ""

if [ -f "src/USP.Api/appsettings.Development.json" ]; then
    echo "  VIOLATION: appsettings.Development.json still exists"
    VIOLATIONS_FOUND=$((VIOLATIONS_FOUND + 1))
else
    echo "  OK: appsettings.Development.json deleted"
fi

echo ""
echo "Verifying required files exist..."
echo ""

REQUIRED_FILES=(
    "src/USP.Core/Models/Configuration/DatabaseSettings.cs"
    "src/USP.Core/Models/Configuration/JwtSettings.cs"
    "src/USP.Core/Models/Configuration/RedisSettings.cs"
    "src/USP.Core/Models/Configuration/RabbitMqSettings.cs"
    "src/USP.Core/Validators/ConfigurationValidator.cs"
    "src/USP.Api/appsettings.Development.json.example"
    "src/USP.Api/.env.example"
    "SECRETS-SETUP.md"
)

for file in "${REQUIRED_FILES[@]}"; do
    echo -n "  $file ... "
    if [ -f "$file" ]; then
        echo "OK"
    else
        echo "MISSING"
        VIOLATIONS_FOUND=$((VIOLATIONS_FOUND + 1))
    fi
done

echo ""
echo "Checking UserSecretsId in project file..."
echo ""

if grep -q "<UserSecretsId>" src/USP.Api/USP.Api.csproj; then
    echo "  OK: UserSecretsId configured"
else
    echo "  VIOLATION: UserSecretsId not found in USP.Api.csproj"
    VIOLATIONS_FOUND=$((VIOLATIONS_FOUND + 1))
fi

echo ""
echo "========================================="
echo "Verification Results"
echo "========================================="
echo ""

if [ $VIOLATIONS_FOUND -eq 0 ]; then
    echo "SUCCESS: No hardcoded secrets found!"
    echo ""
    echo "All checks passed:"
    echo "  - No prohibited patterns in source code"
    echo "  - All password fields are empty in appsettings.json"
    echo "  - appsettings.Development.json deleted"
    echo "  - All required configuration files created"
    echo "  - UserSecretsId configured"
    echo ""
    echo "The codebase is clean and ready for secure deployment."
    exit 0
else
    echo "FAILED: $VIOLATIONS_FOUND violation(s) found!"
    echo ""
    echo "Please review the output above and fix the issues."
    exit 1
fi
