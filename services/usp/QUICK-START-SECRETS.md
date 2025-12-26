# Quick Start - Set Up Secrets for USP Service

## One-Command Setup (Recommended)

Run this from the USP service root directory:

```bash
cd services/usp/src/USP.Api

# Generate and set all required secrets automatically
dotnet user-secrets set "Database:Password" "dev_$(openssl rand -hex 16)"
dotnet user-secrets set "Jwt:Secret" "$(openssl rand -base64 64 | tr -d '\n')"
dotnet user-secrets set "Redis:Password" "redis_$(openssl rand -hex 16)"
dotnet user-secrets set "RabbitMQ:Password" "rabbitmq_$(openssl rand -hex 16)"

echo "Secrets configured! You can now run the service."
```

## Verify Secrets

```bash
# List all configured secrets
dotnet user-secrets list

# Build the project
dotnet build

# Run the service (it will validate configuration at startup)
dotnet run
```

## Expected Output on Success

```
[Information] Starting USP (Unified Security Platform) service
[Information] Development environment detected - User Secrets enabled
[Information] Validating configuration...
[Information] Configuration validation successful
[Information] Database configured: localhost:5432/usp_db
[Information] JWT configured: Algorithm=HS256, Issuer=usp-service
[Information] Redis configured: localhost:6379
[Information] WebAuthn configured: RP=localhost
[Information] USP service started successfully
```

## What If It Fails?

If you see configuration validation errors, the error message will tell you exactly what's missing:

```
Configuration validation failed:
  - Database configuration: Database password is required. Set Database:Password in User Secrets...
```

Just run the command shown in the error message:

```bash
dotnet user-secrets set "Database:Password" "your-password"
```

## More Help

For detailed instructions, production setup, and troubleshooting:

- **Full Guide:** `SECRETS-SETUP.md`
- **Summary:** `TRACK2-REMEDIATION-SUMMARY.md`
- **Verification:** Run `./verify-no-secrets.sh`

## Using Different Infrastructure

If your local infrastructure uses different credentials:

```bash
# Database
dotnet user-secrets set "Database:Host" "your-db-host"
dotnet user-secrets set "Database:Port" "5432"
dotnet user-secrets set "Database:Database" "your_db_name"
dotnet user-secrets set "Database:Username" "your_username"
dotnet user-secrets set "Database:Password" "your_password"

# Redis
dotnet user-secrets set "Redis:Host" "your-redis-host"
dotnet user-secrets set "Redis:Port" "6379"
dotnet user-secrets set "Redis:Password" "your_redis_password"

# RabbitMQ
dotnet user-secrets set "RabbitMQ:HostName" "your-rabbitmq-host"
dotnet user-secrets set "RabbitMQ:UserName" "your_username"
dotnet user-secrets set "RabbitMQ:Password" "your_password"
```

## Clear All Secrets (Start Fresh)

```bash
cd services/usp/src/USP.Api
dotnet user-secrets clear
```

## That's It!

You're ready to develop. The service will fail fast with clear error messages if anything is misconfigured.
