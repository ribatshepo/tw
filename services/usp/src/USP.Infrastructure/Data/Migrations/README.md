# Database Migrations

This directory contains Entity Framework Core migrations for the Unified Security Platform (USP) database.

## Current Migrations

### 20251225000000_InitialCreate

Initial database schema creation with all core entities:

**Tables Created:**
- `users` - ASP.NET Identity users with MFA support
- `roles` - ASP.NET Identity roles with system role flag
- `permissions` - Resource-based permissions
- `user_roles` - User-to-role assignments (many-to-many)
- `role_permissions` - Role-to-permission assignments (many-to-many)
- `access_policies` - HCL/ABAC policy definitions
- `secrets` - Vault KV v2 secrets with versioning
- `secret_access_log` - Audit trail for secret access
- `sessions` - JWT session tracking
- `audit_logs` - Comprehensive audit trail
- `api_keys` - API key authentication
- `mfa_devices` - MFA device registrations
- `mfa_backup_codes` - MFA recovery codes
- `trusted_devices` - Trusted device fingerprints
- `seal_configurations` - Seal/unseal state

**Indexes Created:** 25+ indexes for optimal query performance

## Prerequisites

Before applying migrations, ensure you have:

1. **PostgreSQL Database Running**
   ```bash
   # Start infrastructure with docker-compose
   cd /home/tshepo/projects/tw
   docker-compose up -d postgres
   ```

2. **Connection String Configured**

   Set the connection string in `appsettings.json` or environment variable:
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Host=localhost;Port=5432;Database=usp_db;Username=usp_user;Password=your_password"
     }
   }
   ```

3. **EF Core Tools Installed** (Optional)
   ```bash
   dotnet tool install --global dotnet-ef
   ```

## Applying Migrations

### Method 1: Using EF Core Tools (Recommended)

```bash
# Navigate to the Infrastructure project
cd /home/tshepo/projects/tw/services/usp/src/USP.Infrastructure

# Apply all pending migrations
dotnet ef database update --startup-project ../USP.Api

# Apply to specific migration
dotnet ef database update 20251225000000_InitialCreate --startup-project ../USP.Api
```

### Method 2: Automatic Migration on Startup

Add this code to `Program.cs` to apply migrations automatically when the application starts:

```csharp
// After var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await context.Database.MigrateAsync();
}
```

### Method 3: Manual SQL Execution

Generate SQL script and apply manually:

```bash
# Generate SQL script for all migrations
dotnet ef migrations script --startup-project ../USP.Api --output InitialCreate.sql

# Apply SQL script to database
psql -h localhost -U usp_user -d usp_db -f InitialCreate.sql
```

## Verifying Migrations

### Check Migration Status

```bash
# List all migrations and their status
dotnet ef migrations list --startup-project ../USP.Api
```

### Verify Database Schema

```sql
-- Connect to database
psql -h localhost -U usp_user -d usp_db

-- List all tables
\dt

-- Check specific table structure
\d users
\d secrets
\d seal_configurations

-- Verify indexes
SELECT schemaname, tablename, indexname
FROM pg_indexes
WHERE schemaname = 'public'
ORDER BY tablename, indexname;
```

## Rolling Back Migrations

### Rollback to Previous Migration

```bash
# Rollback to before InitialCreate (empty database)
dotnet ef database update 0 --startup-project ../USP.Api
```

### Remove Last Migration (if not applied)

```bash
# Remove the migration files
dotnet ef migrations remove --startup-project ../USP.Api
```

## Creating New Migrations

When you modify entity models, create a new migration:

```bash
cd /home/tshepo/projects/tw/services/usp/src/USP.Infrastructure

# Create new migration
dotnet ef migrations add MigrationName --startup-project ../USP.Api

# Review generated migration
# Files will be created in Data/Migrations/

# Apply the migration
dotnet ef database update --startup-project ../USP.Api
```

## Common Issues and Solutions

### Issue: "Connection refused" or "Could not connect to server"

**Solution:** Ensure PostgreSQL is running:
```bash
docker-compose up -d postgres
# Wait 10 seconds for PostgreSQL to start
sleep 10
```

### Issue: "Database does not exist"

**Solution:** Create the database first:
```bash
psql -h localhost -U postgres -c "CREATE DATABASE usp_db;"
psql -h localhost -U postgres -c "CREATE USER usp_user WITH PASSWORD 'your_password';"
psql -h localhost -U postgres -c "GRANT ALL PRIVILEGES ON DATABASE usp_db TO usp_user;"
```

### Issue: "The EF Core tools version is older than the runtime version"

**Solution:** Update EF Core tools:
```bash
dotnet tool update --global dotnet-ef
```

### Issue: "A command is already in progress"

**Solution:** Ensure no other migrations are running and PostgreSQL is not locked:
```bash
# Check for active connections
psql -h localhost -U postgres -c "SELECT * FROM pg_stat_activity WHERE datname = 'usp_db';"

# Terminate connections if needed (careful!)
psql -h localhost -U postgres -c "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = 'usp_db' AND pid <> pg_backend_pid();"
```

## Database Initialization Workflow

Complete workflow for setting up a fresh database:

```bash
# 1. Start PostgreSQL
cd /home/tshepo/projects/tw
docker-compose up -d postgres

# 2. Wait for PostgreSQL to be ready
sleep 10

# 3. Create database and user (if not exists)
psql -h localhost -U postgres -c "CREATE DATABASE usp_db;"
psql -h localhost -U postgres -c "CREATE USER usp_user WITH PASSWORD 'your_password';"
psql -h localhost -U postgres -c "GRANT ALL PRIVILEGES ON DATABASE usp_db TO usp_user;"
psql -h localhost -U postgres -d usp_db -c "GRANT ALL ON SCHEMA public TO usp_user;"

# 4. Apply migrations
cd services/usp/src/USP.Infrastructure
dotnet ef database update --startup-project ../USP.Api

# 5. Verify schema
psql -h localhost -U usp_user -d usp_db -c "\dt"

# 6. Initialize seal (first time only)
# Use the /api/sys/seal/init endpoint after starting the application

# 7. Start the application
cd ../USP.Api
dotnet run
```

## Migration Best Practices

1. **Always review generated migrations** before applying to production
2. **Test migrations on a copy of production data** before deploying
3. **Create database backups** before running migrations in production
4. **Use migration scripts** for production deployments (not auto-migrate)
5. **Never modify existing migrations** that have been applied to production
6. **Use descriptive migration names** that explain what changed

## Production Deployment

For production environments:

```bash
# 1. Generate SQL script
dotnet ef migrations script --idempotent --startup-project ../USP.Api --output migrations.sql

# 2. Review SQL script carefully
cat migrations.sql

# 3. Create database backup
pg_dump -h prod-db-host -U usp_user usp_db > backup_$(date +%Y%m%d_%H%M%S).sql

# 4. Apply SQL script during maintenance window
psql -h prod-db-host -U usp_user -d usp_db -f migrations.sql

# 5. Verify migration applied successfully
psql -h prod-db-host -U usp_user -d usp_db -c "SELECT * FROM __EFMigrationsHistory ORDER BY migration_id;"
```

## Schema Versioning

Migrations are tracked in the `__EFMigrationsHistory` table:

```sql
-- View all applied migrations
SELECT migration_id, product_version
FROM "__EFMigrationsHistory"
ORDER BY migration_id;
```

## Next Steps

After applying the initial migration:

1. **Initialize the seal** - Use POST `/api/sys/seal/init` to create encryption keys
2. **Initialize built-in roles** - Application will create on first startup
3. **Create admin user** - Use POST `/api/auth/register` to create first user
4. **Test all endpoints** - Verify authentication, secrets, MFA work correctly

## Support

For issues with migrations:
- Check PostgreSQL logs: `docker-compose logs postgres`
- Check application logs for EF Core errors
- Review migration SQL script for any issues
- Consult EF Core documentation: https://docs.microsoft.com/ef/core/
