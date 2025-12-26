# USP Service - Secrets Configuration Setup

This document explains how to configure secrets for the USP (Unified Security Platform) service after the Track 2 remediation that removed all hardcoded secrets.

## Overview

The USP service requires the following secrets to be configured:

- Database password
- JWT secret key
- Redis password
- RabbitMQ password
- Email SMTP credentials (optional)

## Development Environment Setup

### Option 1: User Secrets (Recommended for Development)

User Secrets are stored outside the project directory and are never committed to version control.

#### Initialize User Secrets

The project is already configured with `UserSecretsId: usp-service-secrets-2024` in `USP.Api.csproj`.

#### Set Required Secrets

Navigate to the USP.Api project directory and run:

```bash
cd services/usp/src/USP.Api

# Database password
dotnet user-secrets set "Database:Password" "your-secure-database-password"

# JWT secret (minimum 32 characters - generate a strong one)
dotnet user-secrets set "Jwt:Secret" "$(openssl rand -base64 64 | tr -d '\n')"

# Redis password
dotnet user-secrets set "Redis:Password" "your-redis-password"

# RabbitMQ password
dotnet user-secrets set "RabbitMQ:Password" "your-rabbitmq-password"

# Email credentials (optional - only if using email features)
dotnet user-secrets set "Email:SmtpUsername" "your-email@example.com"
dotnet user-secrets set "Email:SmtpPassword" "your-smtp-password"
```

#### Generate Strong Secrets

Use OpenSSL to generate cryptographically secure secrets:

```bash
# Generate JWT secret (base64, 64 bytes)
openssl rand -base64 64 | tr -d '\n'

# Generate random password (32 characters, alphanumeric + symbols)
openssl rand -base64 32

# Generate UUID-based password
uuidgen
```

#### View Configured Secrets

```bash
# List all secrets for the project
dotnet user-secrets list

# Remove a specific secret
dotnet user-secrets remove "Database:Password"

# Clear all secrets
dotnet user-secrets clear
```

### Option 2: Environment Variables

You can also use environment variables with the `USP_` prefix:

```bash
# Set environment variables (Linux/macOS)
export USP_Database__Password="your-database-password"
export USP_Jwt__Secret="your-jwt-secret-min-32-chars"
export USP_Redis__Password="your-redis-password"
export USP_RabbitMQ__Password="your-rabbitmq-password"

# Windows PowerShell
$env:USP_Database__Password="your-database-password"
$env:USP_Jwt__Secret="your-jwt-secret-min-32-chars"
$env:USP_Redis__Password="your-redis-password"
$env:USP_RabbitMQ__Password="your-rabbitmq-password"
```

Note: Use double underscores `__` for nested configuration (e.g., `Database:Password` becomes `Database__Password`).

### Option 3: .env File (Local Development)

Copy the example file and fill in your secrets:

```bash
cp src/USP.Api/.env.example src/USP.Api/.env
```

Then edit `.env` and add your secrets. The `.env` file is in `.gitignore` and will never be committed.

**Note:** .NET doesn't load `.env` files by default. You'll need to use User Secrets or environment variables.

## Production Environment Setup

### Kubernetes Secrets

For production deployments on Kubernetes, use Kubernetes Secrets:

```bash
# Create a secret for database credentials
kubectl create secret generic usp-database-secret \
  --from-literal=password='your-production-db-password' \
  -n usp-namespace

# Create a secret for JWT
kubectl create secret generic usp-jwt-secret \
  --from-literal=secret='your-production-jwt-secret' \
  -n usp-namespace

# Create a secret for Redis
kubectl create secret generic usp-redis-secret \
  --from-literal=password='your-production-redis-password' \
  -n usp-namespace

# Create a secret for RabbitMQ
kubectl create secret generic usp-rabbitmq-secret \
  --from-literal=password='your-production-rabbitmq-password' \
  -n usp-namespace
```

Then reference these secrets in your Kubernetes deployment manifests as environment variables:

```yaml
env:
  - name: USP_Database__Password
    valueFrom:
      secretKeyRef:
        name: usp-database-secret
        key: password
  - name: USP_Jwt__Secret
    valueFrom:
      secretKeyRef:
        name: usp-jwt-secret
        key: secret
  - name: USP_Redis__Password
    valueFrom:
      secretKeyRef:
        name: usp-redis-secret
        key: password
  - name: USP_RabbitMQ__Password
    valueFrom:
      secretKeyRef:
        name: usp-rabbitmq-secret
        key: password
```

### Docker Secrets

For Docker Swarm or standalone Docker containers:

```bash
# Create Docker secrets
echo "your-db-password" | docker secret create usp_db_password -
echo "your-jwt-secret" | docker secret create usp_jwt_secret -
echo "your-redis-password" | docker secret create usp_redis_password -
echo "your-rabbitmq-password" | docker secret create usp_rabbitmq_password -
```

### Azure Key Vault / AWS Secrets Manager / HashiCorp Vault

For cloud deployments, use managed secret services:

**Azure Key Vault:**
```bash
az keyvault secret set --vault-name usp-vault --name DatabasePassword --value "your-password"
az keyvault secret set --vault-name usp-vault --name JwtSecret --value "your-jwt-secret"
```

**AWS Secrets Manager:**
```bash
aws secretsmanager create-secret --name usp/database/password --secret-string "your-password"
aws secretsmanager create-secret --name usp/jwt/secret --secret-string "your-jwt-secret"
```

## Configuration Validation

The USP service validates all configuration at startup and will **fail fast** with clear error messages if any required secrets are missing or invalid.

### Validation Checks

The service validates:

1. **Database Configuration:**
   - Host, port, database name, username are not empty
   - Password is provided and not empty
   - Connection string can be built

2. **JWT Configuration:**
   - Algorithm is supported (HS256, HS384, HS512, RS256, RS384, RS512)
   - For HMAC algorithms: Secret is at least 32 characters
   - Issuer and audience are not empty
   - Expiration times are positive and reasonable

3. **Redis Configuration:**
   - Host is not empty
   - Port is valid (1-65535)
   - Database number is valid (0-15)

4. **RabbitMQ Configuration:**
   - Hostname is not empty
   - Username and password are not empty
   - Default credentials (guest/guest) are rejected

5. **Prohibited Patterns:**
   - No hardcoded weak passwords like "changeme", "password123", "admin123"
   - No default development secrets in production

### Testing Configuration

Run the service to test configuration:

```bash
cd services/usp/src/USP.Api
dotnet run
```

If configuration is invalid, you'll see clear error messages:

```
Configuration validation failed:
  - Database configuration: Database password is required. Set Database:Password in User Secrets...
  - JWT configuration: JWT secret must be at least 32 characters long...
```

## Running EF Core Migrations

EF Core migrations also require database credentials. The `ApplicationDbContextFactory` now loads secrets from User Secrets or environment variables.

```bash
# Make sure secrets are configured first (see above)
cd services/usp/src/USP.Api

# Create a migration
dotnet ef migrations add MigrationName

# Apply migrations
dotnet ef database update
```

If the password is not configured, you'll see:

```
Database password is required for migrations.
Set it in User Secrets: dotnet user-secrets set "Database:Password" "your-password"
OR set environment variable: export USP_Database__Password="your-password"
```

## Quick Setup Script

For rapid development setup:

```bash
#!/bin/bash
# setup-secrets.sh

cd services/usp/src/USP.Api

# Generate strong JWT secret
JWT_SECRET=$(openssl rand -base64 64 | tr -d '\n')

# Set secrets
dotnet user-secrets set "Database:Password" "dev_password_$(openssl rand -hex 8)"
dotnet user-secrets set "Jwt:Secret" "$JWT_SECRET"
dotnet user-secrets set "Redis:Password" "redis_$(openssl rand -hex 8)"
dotnet user-secrets set "RabbitMQ:Password" "rabbitmq_$(openssl rand -hex 8)"

echo "Secrets configured successfully!"
dotnet user-secrets list
```

Make it executable and run:

```bash
chmod +x setup-secrets.sh
./setup-secrets.sh
```

## Security Best Practices

1. **Never commit secrets** to version control
2. **Use different secrets** for development, staging, and production
3. **Rotate secrets regularly** (every 90 days recommended)
4. **Use strong, randomly generated secrets** (minimum 32 characters for JWT)
5. **Limit access** to production secrets using RBAC
6. **Audit secret access** and usage
7. **Use managed secret services** in production (Azure Key Vault, AWS Secrets Manager, etc.)

## Troubleshooting

### "Configuration validation failed: Database password is required"

**Solution:** Set the database password in User Secrets:
```bash
dotnet user-secrets set "Database:Password" "your-password"
```

### "JWT secret must be at least 32 characters long"

**Solution:** Generate a strong secret:
```bash
dotnet user-secrets set "Jwt:Secret" "$(openssl rand -base64 64)"
```

### "RabbitMQ password is required"

**Solution:** Set RabbitMQ credentials:
```bash
dotnet user-secrets set "RabbitMQ:Password" "your-password"
```

### Secrets not loading in development

**Solution:** Ensure you're in Development environment:
```bash
export ASPNETCORE_ENVIRONMENT=Development
dotnet run
```

### User Secrets not found for migrations

**Solution:** Make sure you're running commands from the `USP.Api` directory where the `.csproj` file is located.

## Reference

- [.NET User Secrets documentation](https://docs.microsoft.com/en-us/aspnet/core/security/app-secrets)
- [ASP.NET Core Configuration](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/configuration/)
- [Kubernetes Secrets](https://kubernetes.io/docs/concepts/configuration/secret/)
- [Azure Key Vault Configuration Provider](https://docs.microsoft.com/en-us/aspnet/core/security/key-vault-configuration)
