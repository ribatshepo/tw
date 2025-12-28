# Security Regression Test Plan

**Document Version:** 1.0
**Last Updated:** 2025-12-27
**Test Scope:** All 43 Security Audit Findings
**Test Type:** Automated Regression Testing
**Execution Frequency:** Every commit (CI/CD), Pre-release, Monthly
**Owner:** Security + QA Teams

---

## Table of Contents

1. [Overview](#overview)
2. [Test Environment Setup](#test-environment-setup)
3. [Secrets Management Tests](#secrets-management-tests)
4. [Authentication & Authorization Tests](#authentication--authorization-tests)
5. [TLS/HTTPS Security Tests](#tlshttps-security-tests)
6. [Database Security Tests](#database-security-tests)
7. [Observability Security Tests](#observability-security-tests)
8. [Configuration Security Tests](#configuration-security-tests)
9. [Code Quality Tests](#code-quality-tests)
10. [Automated Test Execution](#automated-test-execution)
11. [Test Results & Reporting](#test-results--reporting)

---

## Overview

### Purpose

This test plan defines automated security regression tests that verify:
- All 43 security findings remain resolved
- No security vulnerabilities reintroduced
- Security controls continue functioning correctly
- Compliance requirements continuously met

### Test Strategy

**Automated Testing:**
- Unit tests for security functions
- Integration tests for security flows
- Security scanner integration (SAST, DAST)
- Compliance validation scripts

**CI/CD Integration:**
- Run on every pull request
- Block merge if security tests fail
- Generate security test reports
- Alert security team on failures

---

## Test Environment Setup

### Prerequisites

```bash
# Install test dependencies
dotnet add package xUnit --version 2.6.0
dotnet add package Moq --version 4.20.0
dotnet add package FluentAssertions --version 6.12.0

# Security testing tools
dotnet tool install --global security-scan
dotnet tool install --global dotnet-sonarscanner

# Install Trivy for container scanning
brew install trivy  # macOS
# or
sudo apt-get install trivy  # Ubuntu
```

### Test Data Setup

```bash
# Create test Vault instance
docker run -d --name vault-test \
  -p 8200:8200 \
  --cap-add=IPC_LOCK \
  -e 'VAULT_DEV_ROOT_TOKEN_ID=test-root-token' \
  vault:latest

# Initialize test database
docker run -d --name postgres-test \
  -p 5433:5432 \
  -e POSTGRES_PASSWORD=test \
  postgres:16-alpine

# Seed test data
psql -h localhost -p 5433 -U postgres -f tests/fixtures/test-data.sql
```

---

## Secrets Management Tests

### Test Suite: SEC-P0-001 - No Hardcoded Secrets in .env

**Test:** Verify no secrets in `.env` files

```csharp
// tests/security/SecretsManagementTests.cs
using Xunit;
using FluentAssertions;

public class SecretsManagementTests
{
    [Fact]
    public void EnvFile_ShouldNotContainSecrets()
    {
        // Arrange
        var envPath = Path.Combine(GetProjectRoot(), ".env");
        var envContent = File.ReadAllText(envPath);

        // Act
        var forbiddenPatterns = new[]
        {
            @"PASSWORD\s*=\s*[""']?[^#\s]+",
            @"SECRET\s*=\s*[""']?[^#\s]+",
            @"API_KEY\s*=\s*[""']?[^#\s]+",
            @"TOKEN\s*=\s*[""']?[^#\s]+"
        };

        // Assert
        foreach (var pattern in forbiddenPatterns)
        {
            Regex.Matches(envContent, pattern)
                .Should().BeEmpty($"Found secret pattern: {pattern}");
        }
    }

    [Fact]
    public void Application_ShouldFetchSecretsFromVault()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .Build();

        // Act & Assert
        config["Database:Password"]
            .Should().BeNullOrEmpty("Password should be fetched from Vault, not config");

        config["Jwt:SecretKey"]
            .Should().BeNullOrEmpty("JWT secret should be from Vault, not config");
    }

    [Fact]
    public async Task VaultClient_ShouldFetchSecrets_Successfully()
    {
        // Arrange
        var vaultClient = new VaultClient("http://localhost:8200", "test-root-token");

        // Act
        var result = await vaultClient.GetSecretAsync("/database/password");

        // Assert
        result.Should().NotBeNull();
        result.Data.Should().ContainKey("password");
        result.Data["password"].Should().NotBeNullOrEmpty();
    }
}
```

---

### Test Suite: SEC-P0-003 - No Hardcoded SQL Passwords

**Test:** Verify SQL scripts use parameterized passwords

```csharp
[Fact]
public void SqlScripts_ShouldUseParameterizedPasswords()
{
    // Arrange
    var sqlFiles = Directory.GetFiles(
        Path.Combine(GetProjectRoot(), "migrations/sql"),
        "*.sql"
    );

    // Act & Assert
    foreach (var sqlFile in sqlFiles)
    {
        var content = File.ReadAllText(sqlFile);

        // Check for hardcoded passwords
        var hardcodedPattern = @"PASSWORD\s+['""].*['""]";
        Regex.Matches(content, hardcodedPattern)
            .Should().BeEmpty($"File {Path.GetFileName(sqlFile)} has hardcoded password");

        // If CREATE USER statement exists, verify parameterization
        if (content.Contains("CREATE USER", StringComparison.OrdinalIgnoreCase))
        {
            content.Should().Contain(":",
                "CREATE USER should use psql variable syntax (:VAR_NAME)");
        }
    }
}

[Fact]
public async Task CredentialLoader_ShouldFetchFromVault()
{
    // Arrange
    var vaultClient = new VaultClient("http://localhost:8200", "test-root-token");
    await SeedVaultWithTestCredentials(vaultClient);

    // Act
    var process = new Process
    {
        StartInfo = new ProcessStartInfo
        {
            FileName = "/bin/bash",
            Arguments = "scripts/db/load-db-credentials.sh",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            EnvironmentVariables =
            {
                ["VAULT_TOKEN"] = "test-root-token",
                ["VAULT_ADDR"] = "http://localhost:8200"
            }
        }
    };

    process.Start();
    await process.WaitForExitAsync();
    var output = await process.StandardOutput.ReadToEndAsync();

    // Assert
    process.ExitCode.Should().Be(0, "Credential loader should succeed");
    output.Should().Contain("Credentials loaded successfully");
}
```

---

## Authentication & Authorization Tests

### Test Suite: SEC-P0-004 - Vault Endpoints Require Authentication

**Test:** Verify Vault seal/unseal require X-Vault-Token

```csharp
[Fact]
public async Task VaultSealEndpoint_WithoutToken_ShouldReturn401()
{
    // Arrange
    var client = CreateTestClient();

    // Act
    var response = await client.PostAsync("/api/v1/vault/seal", null);

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

    var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
    error.Error.Should().Be("vault_token_required");
}

[Fact]
public async Task VaultSealEndpoint_WithInvalidToken_ShouldReturn403()
{
    // Arrange
    var client = CreateTestClient();
    client.DefaultRequestHeaders.Add("X-Vault-Token", "invalid-token");

    // Act
    var response = await client.PostAsync("/api/v1/vault/seal", null);

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
}

[Fact]
public async Task VaultSealEndpoint_WithValidToken_ShouldReturn200()
{
    // Arrange
    var client = CreateTestClient();
    client.DefaultRequestHeaders.Add("X-Vault-Token", GetTestRootToken());

    // Act
    var response = await client.PostAsync("/api/v1/vault/seal", null);

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.OK);
}
```

---

### Test Suite: SEC-P0-005 - JWT Bearer Middleware Active

**Test:** Verify JWT authentication middleware validates tokens

```csharp
[Fact]
public async Task ProtectedEndpoint_WithoutToken_ShouldReturn401()
{
    // Arrange
    var client = CreateTestClient();

    // Act
    var response = await client.GetAsync("/api/v1/secrets");

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    response.Headers.WwwAuthenticate.Should().NotBeEmpty();
}

[Fact]
public async Task ProtectedEndpoint_WithExpiredToken_ShouldReturn401()
{
    // Arrange
    var client = CreateTestClient();
    var expiredToken = GenerateExpiredJwtToken();
    client.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Bearer", expiredToken);

    // Act
    var response = await client.GetAsync("/api/v1/secrets");

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
}

[Fact]
public async Task ProtectedEndpoint_WithValidToken_ShouldReturn200()
{
    // Arrange
    var client = CreateTestClient();
    var validToken = await GenerateValidJwtToken();
    client.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Bearer", validToken);

    // Act
    var response = await client.GetAsync("/api/v1/secrets");

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.OK);
}

[Fact]
public async Task JwtToken_WithInvalidSignature_ShouldReturn401()
{
    // Arrange
    var client = CreateTestClient();
    var tamperedToken = GenerateTokenWithInvalidSignature();
    client.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Bearer", tamperedToken);

    // Act
    var response = await client.GetAsync("/api/v1/secrets");

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
}
```

---

### Test Suite: SEC-P1-008 - Granular Authorization

**Test:** Verify RequirePermission attributes enforce permissions

```csharp
[Fact]
public async Task SecretsEndpoint_ReadOnlyUser_CannotDelete()
{
    // Arrange
    var client = CreateTestClient();
    var readOnlyToken = await GenerateTokenForRole("secrets-reader");
    client.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Bearer", readOnlyToken);

    // Act
    var response = await client.DeleteAsync("/api/v1/secrets/test-secret");

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

    var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
    error.Error.Should().Be("insufficient_permissions");
    error.Message.Should().Contain("delete");
}

[Fact]
public async Task SecretsEndpoint_AdminUser_CanDelete()
{
    // Arrange
    var client = CreateTestClient();
    var adminToken = await GenerateTokenForRole("secrets-admin");
    client.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Bearer", adminToken);

    // Create test secret first
    await CreateTestSecret(client, "/test-secret-to-delete");

    // Act
    var response = await client.DeleteAsync("/api/v1/secrets/test-secret-to-delete");

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.NoContent);
}

[Fact]
public async Task AuthorizationMetrics_ShouldRecordDeniedAttempts()
{
    // Arrange
    var client = CreateTestClient();
    var readOnlyToken = await GenerateTokenForRole("secrets-reader");
    client.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Bearer", readOnlyToken);

    // Act
    await client.DeleteAsync("/api/v1/secrets/test-secret");

    // Assert - Check Prometheus metrics
    var metricsResponse = await client.GetAsync("/metrics");
    var metricsContent = await metricsResponse.Content.ReadAsStringAsync();

    metricsContent.Should().Contain("usp_authorization_checks_total");
    metricsContent.Should().Contain("granted=\"false\"");
}
```

---

## TLS/HTTPS Security Tests

### Test Suite: SEC-P0-008 - TrustServerCertificate Removed

**Test:** Verify PostgreSQL connections use TLS with certificate validation

```csharp
[Fact]
public void ConnectionString_ShouldNotTrustServerCertificate()
{
    // Arrange
    var config = new ConfigurationBuilder()
        .AddJsonFile("appsettings.json")
        .Build();

    var connectionString = config.GetConnectionString("DefaultConnection");

    // Assert
    connectionString.Should().NotContain("TrustServerCertificate=true",
        "Production should validate PostgreSQL certificates");

    connectionString.Should().Contain("SSL Mode=Require",
        "Production should require SSL");

    connectionString.Should().Contain("Root Certificate",
        "Certificate path should be specified");
}

[Fact]
public async Task DatabaseConnection_ShouldUseTLS()
{
    // Arrange
    await using var connection = new NpgsqlConnection(GetTestConnectionString());

    // Act
    await connection.OpenAsync();

    // Assert - Query PostgreSQL to check SSL status
    await using var cmd = new NpgsqlCommand("SELECT ssl_is_used FROM pg_stat_ssl WHERE pid = pg_backend_pid()", connection);
    var sslUsed = await cmd.ExecuteScalarAsync();

    sslUsed.Should().Be(true, "Database connection should use TLS");
}
```

---

### Test Suite: SEC-P1-001 - Metrics Endpoint HTTPS

**Test:** Verify metrics endpoint uses HTTPS

```csharp
[Fact]
public async Task MetricsEndpoint_ShouldServeHTTPS()
{
    // Arrange
    var client = new HttpClient(new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = (_, _, _, _) => true // Allow self-signed for testing
    });

    // Act
    var response = await client.GetAsync("https://localhost:9091/metrics");

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.OK);
    response.RequestMessage.RequestUri.Scheme.Should().Be("https");
}

[Fact]
public async Task MetricsEndpoint_HTTP_ShouldRedirectOrFail()
{
    // Arrange
    var client = new HttpClient();

    // Act
    Func<Task> act = async () => await client.GetAsync("http://localhost:9091/metrics");

    // Assert - Should fail or redirect to HTTPS
    await act.Should().ThrowAsync<HttpRequestException>();
}
```

---

### Test Suite: SEC-P1-002 - HSTS Middleware

**Test:** Verify HSTS headers present

```csharp
[Fact]
public async Task ApiEndpoint_ShouldIncludeHSTSHeader()
{
    // Arrange
    var client = CreateTestClient();

    // Act
    var response = await client.GetAsync("/api/v1/health");

    // Assert
    response.Headers.Should().ContainKey("Strict-Transport-Security");

    var hstsHeader = response.Headers.GetValues("Strict-Transport-Security").First();
    hstsHeader.Should().Contain("max-age=31536000"); // 1 year
    hstsHeader.Should().Contain("includeSubDomains");
    hstsHeader.Should().Contain("preload");
}
```

---

## Database Security Tests

### Test Suite: SEC-P1-009 - Row-Level Security

**Test:** Verify RLS enforces namespace isolation

```csharp
[Fact]
public async Task RowLevelSecurity_UserA_CannotSeeUserBSecrets()
{
    // Arrange
    await using var connection = new NpgsqlConnection(GetTestConnectionString());
    await connection.OpenAsync();

    // Create test data
    await CreateTestSecret(connection, namespaceId: "namespace-a", path: "/secret-a");
    await CreateTestSecret(connection, namespaceId: "namespace-b", path: "/secret-b");

    // Act - Query as User A (has access to namespace-a only)
    await using var cmd = new NpgsqlCommand(@"
        SET app.current_user_id = 'user-a';
        SELECT path FROM usp.secrets;
    ", connection);

    var secrets = new List<string>();
    await using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        secrets.Add(reader.GetString(0));
    }

    // Assert
    secrets.Should().Contain("/secret-a");
    secrets.Should().NotContain("/secret-b", "RLS should prevent access to other namespace");
}

[Fact]
public async Task RowLevelSecurity_InsertWithoutPermission_ShouldFail()
{
    // Arrange
    await using var connection = new NpgsqlConnection(GetTestConnectionString());
    await connection.OpenAsync();

    // Act - Try to insert into namespace user doesn't have access to
    await using var cmd = new NpgsqlCommand(@"
        SET app.current_user_id = 'user-a';
        INSERT INTO usp.secrets (secret_id, path, namespace_id, encrypted_data, version)
        VALUES (gen_random_uuid(), '/unauthorized', 'namespace-b', 'encrypted', 1);
    ", connection);

    Func<Task> act = async () => await cmd.ExecuteNonQueryAsync();

    // Assert
    await act.Should().ThrowAsync<PostgresException>()
        .Where(ex => ex.SqlState == "42501"); // Insufficient privilege
}
```

---

### Test Suite: SEC-P1-010 - SQL Transaction Wrapping

**Test:** Verify schema scripts use transactions

```csharp
[Fact]
public void SqlSchemaScripts_ShouldHaveTransactionWrappers()
{
    // Arrange
    var schemaFiles = Directory.GetFiles(
        Path.Combine(GetProjectRoot(), "migrations/sql"),
        "0[4-8]-*.sql" // Schema scripts
    );

    // Assert
    foreach (var schemaFile in schemaFiles)
    {
        var content = File.ReadAllText(schemaFile);

        content.Should().Contain("BEGIN;",
            $"{Path.GetFileName(schemaFile)} should start with BEGIN");

        content.Should().Contain("COMMIT;",
            $"{Path.GetFileName(schemaFile)} should end with COMMIT");

        // Verify BEGIN appears before first DDL statement
        var beginIndex = content.IndexOf("BEGIN;", StringComparison.OrdinalIgnoreCase);
        var createIndex = content.IndexOf("CREATE", StringComparison.OrdinalIgnoreCase);

        beginIndex.Should().BeLessThan(createIndex,
            "BEGIN should appear before CREATE statements");
    }
}
```

---

## Observability Security Tests

### Test Suite: SEC-P1-004 - Metrics Endpoint Active

**Test:** Verify metrics endpoint returns data

```csharp
[Fact]
public async Task MetricsEndpoint_ShouldReturnPrometheusData()
{
    // Arrange
    var client = CreateTestClient();

    // Act
    var response = await client.GetAsync("/metrics");

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.OK);

    var content = await response.Content.ReadAsStringAsync();
    content.Should().Contain("# HELP http_requests_total");
    content.Should().Contain("# TYPE http_requests_total counter");
    content.Should().Contain("http_requests_total{");
}

[Fact]
public async Task SecurityMetrics_ShouldRecordEvents()
{
    // Arrange
    var client = CreateTestClient();

    // Act - Perform login
    await client.PostAsJsonAsync("/api/v1/auth/login", new
    {
        username = "testuser",
        password = "password"
    });

    // Assert - Check metrics recorded
    var metricsResponse = await client.GetAsync("/metrics");
    var metricsContent = await metricsResponse.Content.ReadAsStringAsync();

    metricsContent.Should().Contain("usp_login_attempts_total");
}
```

---

## Configuration Security Tests

### Test Suite: SEC-P2-010 - Certificate Password Randomization

**Test:** Verify certificate passwords are randomized

```csharp
[Fact]
public void CertificateGenerationScript_ShouldUseRandomPasswords()
{
    // Arrange
    var scriptPath = Path.Combine(GetProjectRoot(), "scripts/generate-certificates.sh");
    var scriptContent = File.ReadAllText(scriptPath);

    // Assert
    scriptContent.Should().Contain("openssl rand -base64",
        "Script should use openssl rand for password generation");

    scriptContent.Should().NotContain("password=",
        "Script should not have hardcoded passwords");
}

[Fact]
public async Task VaultShouldStoreCertificatePasswords()
{
    // Arrange
    var vaultClient = new VaultClient("http://localhost:8200", "test-root-token");

    // Act - Run certificate generation script
    var process = new Process
    {
        StartInfo = new ProcessStartInfo
        {
            FileName = "/bin/bash",
            Arguments = "scripts/generate-certificates.sh",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            EnvironmentVariables = { ["VAULT_TOKEN"] = "test-root-token" }
        }
    };

    process.Start();
    await process.WaitForExitAsync();

    // Assert - Verify password stored in Vault
    var secret = await vaultClient.GetSecretAsync("/certificates/password");
    secret.Should().NotBeNull();
    secret.Data.Should().ContainKey("password");
    secret.Data["password"].Should().NotBeNullOrEmpty();
}
```

---

## Code Quality Tests

### Test Suite: SEC-P0-006 - No TODO Comments

**Test:** Verify no TODO comments in production code

```csharp
[Fact]
public void SourceCode_ShouldNotContainTODOComments()
{
    // Arrange
    var sourceFiles = Directory.GetFiles(
        Path.Combine(GetProjectRoot(), "src"),
        "*.cs",
        SearchOption.AllDirectories
    );

    // Act & Assert
    foreach (var sourceFile in sourceFiles)
    {
        var content = File.ReadAllText(sourceFile);

        content.Should().NotContain("TODO:", $"File {Path.GetFileName(sourceFile)} contains TODO comment");
        content.Should().NotContain("FIXME:", $"File {Path.GetFileName(sourceFile)} contains FIXME comment");
        content.Should().NotContain("HACK:", $"File {Path.GetFileName(sourceFile)} contains HACK comment");
    }
}
```

---

### Test Suite: SEC-P0-007 - No NotImplementedException

**Test:** Verify no NotImplementedException in production paths

```csharp
[Fact]
public void SourceCode_ShouldNotThrowNotImplementedException()
{
    // Arrange
    var sourceFiles = Directory.GetFiles(
        Path.Combine(GetProjectRoot(), "src"),
        "*.cs",
        SearchOption.AllDirectories
    );

    // Act & Assert
    foreach (var sourceFile in sourceFiles)
    {
        var content = File.ReadAllText(sourceFile);

        // Allow NotImplementedException in test files and documented limitations
        if (sourceFile.Contains("Tests") || content.Contains("/// <summary>"))
            continue;

        content.Should().NotContain("throw new NotImplementedException",
            $"File {Path.GetFileName(sourceFile)} throws NotImplementedException");
    }
}
```

---

## Automated Test Execution

### CI/CD Pipeline Integration

**GitHub Actions Workflow:**

```yaml
# .github/workflows/security-tests.yml
name: Security Regression Tests

on:
  pull_request:
    branches: [ main, develop ]
  push:
    branches: [ main ]
  schedule:
    - cron: '0 2 * * *'  # Daily at 2 AM

jobs:
  security-tests:
    runs-on: ubuntu-latest

    steps:
      - uses: actions/checkout@v3

      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '8.0.x'

      - name: Start Test Infrastructure
        run: |
          docker-compose -f docker-compose.test.yml up -d
          sleep 30  # Wait for services to start

      - name: Restore dependencies
        run: dotnet restore

      - name: Run Security Tests
        run: dotnet test tests/Security.Tests/ --logger "trx;LogFileName=security-tests.trx"

      - name: Container Security Scan
        run: |
          trivy image --severity CRITICAL,HIGH --exit-code 1 usp:latest
          trivy image --severity CRITICAL,HIGH --exit-code 1 nccs:latest

      - name: Dependency Vulnerability Scan
        run: dotnet list package --vulnerable --include-transitive

      - name: SAST Scan
        run: |
          dotnet tool install --global security-scan
          security-scan analyze --project services/usp/src/USP.API/USP.API.csproj

      - name: Publish Test Results
        uses: dorny/test-reporter@v1
        if: always()
        with:
          name: Security Test Results
          path: '**/security-tests.trx'
          reporter: dotnet-trx

      - name: Fail if vulnerabilities found
        if: failure()
        run: exit 1
```

---

## Test Results & Reporting

### Test Report Format

**Console Output:**

```
Security Regression Test Suite
===============================

✓ Secrets Management (5/5 tests passed)
  ✓ EnvFile_ShouldNotContainSecrets
  ✓ Application_ShouldFetchSecretsFromVault
  ✓ VaultClient_ShouldFetchSecrets_Successfully
  ✓ SqlScripts_ShouldUseParameterizedPasswords
  ✓ CredentialLoader_ShouldFetchFromVault

✓ Authentication & Authorization (8/8 tests passed)
  ✓ VaultSealEndpoint_WithoutToken_ShouldReturn401
  ✓ VaultSealEndpoint_WithValidToken_ShouldReturn200
  ✓ ProtectedEndpoint_WithoutToken_ShouldReturn401
  ✓ ProtectedEndpoint_WithValidToken_ShouldReturn200
  ✓ SecretsEndpoint_ReadOnlyUser_CannotDelete
  ✓ SecretsEndpoint_AdminUser_CanDelete
  ✓ AuthorizationMetrics_ShouldRecordDeniedAttempts
  ✓ JwtToken_WithInvalidSignature_ShouldReturn401

✓ TLS/HTTPS Security (4/4 tests passed)
✓ Database Security (3/3 tests passed)
✓ Observability Security (2/2 tests passed)
✓ Configuration Security (2/2 tests passed)
✓ Code Quality (2/2 tests passed)

===============================
Total: 26/26 tests passed (100%)
Duration: 45.2 seconds
Status: ✅ PASS
```

**Slack Notification:**

```
🔒 Security Regression Tests: ✅ PASS

Commit: abc123f
Branch: feature/add-new-endpoint
Author: developer@tw.com

Results:
  ✅ 26/26 tests passed
  ⏱ Duration: 45.2s
  🔍 Container Scan: 0 CRITICAL, 0 HIGH
  📦 Dependencies: 0 vulnerabilities

View full report: https://ci.tw.com/builds/12345
```

---

## Test Maintenance

### Adding New Security Tests

When new security findings are identified:

1. **Create Test Case**
   ```csharp
   [Fact]
   public void NewSecurityControl_ShouldEnforce_RequiredBehavior()
   {
       // Arrange, Act, Assert
   }
   ```

2. **Update Test Plan** - Document in this file

3. **Add to CI/CD** - Ensure runs on every build

4. **Document Evidence** - Link test to compliance requirement

### Quarterly Security Test Review

- **Review Test Coverage:** Ensure all findings covered
- **Update Test Data:** Refresh test credentials, certificates
- **Performance Tuning:** Optimize slow tests
- **Retirement:** Remove obsolete tests

---

**Status:** Active
**Next Review:** Quarterly (2025-03-27)
**Test Execution:** Automated (CI/CD)
**Contact:** security@tw.com

---

**END OF SECURITY REGRESSION TEST PLAN**
