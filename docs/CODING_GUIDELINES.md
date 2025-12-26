# GBMM Platform Coding Guidelines - Hybrid Polyglot Architecture

**Version:** 2.0.0
**Based on:** 100% Roadmap Hybrid Polyglot Architecture
**Applies to:** All GBMM Services (.NET, Go, Rust, Scala, Python)
**Last Updated:** December 11, 2025

---

## Table of Contents

1. [Production Code Standards](#production-code-standards)
2. [Hybrid Architecture Overview](#hybrid-architecture-overview)
3. [.NET Microservices Guidelines](#net-microservices-guidelines)
4. [Go Services Guidelines (NCS, NRM, NMO, NSL)](#go-services-guidelines)
5. [Python/C++ Services Guidelines (NCF)](#pythonc-services-guidelines)
6. [Rust Services Guidelines (Storage, Stream)](#rust-services-guidelines)
7. [Scala/Java Services Guidelines (Query Engine)](#scalajava-services-guidelines)
8. [Python ML Services Guidelines (Ray Platform)](#python-ml-services-guidelines)
9. [Inter-Service Communication](#inter-service-communication)
10. [Shared Observability](#shared-observability)
11. [Testing Standards](#testing-standards)
12. [Code Quality Checklist](#code-quality-checklist)

---

## Production Code Standards

### Mandatory Requirements

**ALL code in this codebase MUST be production-ready. The following are STRICTLY PROHIBITED:**

| Prohibited Pattern | Description |
|-------------------|-------------|
| `// TODO:` | No TODO comments - implement the feature or remove it |
| `// FIXME:` | No FIXME comments - fix it now |
| `// In production...` | No deferred production logic - implement it now |
| `// For now...` | No temporary implementations - do it properly |
| `// In a real implementation...` | This IS the real implementation |
| `throw new NotImplementedException()` | All methods must be fully implemented |
| `// production would use` | All methods must be fully implemented |
| `// For production` | All methods must be fully implemented |
| Mock/Stub classes | Use real implementations with real packages |
| Simulated responses | Connect to actual services |
| Placeholder data | Use real data sources |
| Hardcoded test values in production code | Use configuration |

### Code Quality Requirements

```csharp
// WRONG - Prohibited patterns
public async Task<Result> ProcessPaymentAsync(PaymentRequest request)
{
    // TODO: Implement payment processing
    // For now, just return success
    // In production, this would connect to Stripe
    return Result.Success(); // Simulated response
}

// CORRECT - Production implementation
public async Task<Result> ProcessPaymentAsync(PaymentRequest request)
{
    try
    {
        var paymentIntent = await _stripeClient.PaymentIntents.CreateAsync(
            new PaymentIntentCreateOptions
            {
                Amount = request.Amount,
                Currency = request.Currency,
                PaymentMethod = request.PaymentMethodId,
                Confirm = true
            });

        await _auditService.LogAsync(request.UserId, "payment.processed",
            "Payment", paymentIntent.Id);

        return Result.Success(new PaymentResponse
        {
            TransactionId = paymentIntent.Id,
            Status = paymentIntent.Status
        });
    }
    catch (StripeException ex)
    {
        _logger.LogError(ex, "Payment processing failed for user {UserId}", request.UserId);
        return Result.Failure($"Payment failed: {ex.Message}", "PAYMENT_FAILED");
    }
}
```

### Build Requirements

- **Zero compilation errors**
- **Zero compilation warnings** (treat warnings as errors)
- **Zero linting errors**
- All packages must be from official sources (NuGet, crates.io, PyPI, Maven Central)
- All features must be fully functional

---

## Universal Requirements for ALL Languages

### Prohibited Patterns (ALL Tech Stack)

**The following patterns are STRICTLY FORBIDDEN in ALL languages (.NET, Go, Rust, Scala, Python):**

| Prohibited Pattern | Language Examples | Description |
|-------------------|-------------------|-------------|
| `// TODO:` `# TODO:` `// TODO` | All languages | No TODO comments - implement the feature or remove it |
| `// FIXME:` `# FIXME:` | All languages | No FIXME comments - fix it now |
| `// In production...` `# In production...` | All languages | No deferred production logic - implement it now |
| `// For now...` `# For now...` | All languages | No temporary implementations - do it properly |
| `// In a real implementation...` | All languages | This IS the real implementation |
| `throw new NotImplementedException()` | C# | All methods must be fully implemented |
| `panic!("not implemented")` | Rust | Use proper error handling |
| `???` | Scala | All cases must be implemented |
| `raise NotImplementedError()` | Python | All methods must be implemented |
| `panic("TODO")` | Go | Return errors, don't panic |
| Mock/Stub classes | All languages | Use real implementations with real packages |
| Simulated responses | All languages | Connect to actual services |
| Placeholder data | All languages | Use real data sources |
| Hardcoded secrets in code | All languages | Use Secrets Manager |
| Hardcoded test values in production code | All languages | Use configuration |

**Examples of FORBIDDEN code:**

```csharp
// ❌ WRONG - .NET
public async Task<Result> ProcessPaymentAsync(PaymentRequest request)
{
    // TODO: Implement Stripe integration
    // For now, just return success
    return Result.Success(); // Simulated response
}

// ✅ CORRECT - .NET
public async Task<Result> ProcessPaymentAsync(PaymentRequest request)
{
    var paymentIntent = await _stripeClient.PaymentIntents.CreateAsync(
        new PaymentIntentCreateOptions
        {
            Amount = request.Amount,
            Currency = request.Currency,
            PaymentMethod = request.PaymentMethodId,
            Confirm = true
        });

    return Result.Success(new PaymentResponse
    {
        TransactionId = paymentIntent.Id,
        Status = paymentIntent.Status
    });
}
```

```go
// ❌ WRONG - Go
func (s *Service) RegisterService(ctx context.Context, node *ServiceNode) error {
    // TODO: Implement Raft consensus
    // For now, just store in memory
    s.services[node.ID] = node
    return nil
}

// ✅ CORRECT - Go
func (s *Service) RegisterService(ctx context.Context, node *ServiceNode) error {
    // Validate input
    if node == nil || node.ID == "" {
        return ErrInvalidNode
    }

    // Register with Raft for consensus
    cmd := &RaftCommand{
        Type: CommandRegisterService,
        Data: node,
    }

    if err := s.raft.Apply(ctx, cmd); err != nil {
        return fmt.Errorf("raft apply failed: %w", err)
    }

    s.services[node.ID] = node
    return nil
}
```

```rust
// ❌ WRONG - Rust
pub fn compress(&mut self, algorithm: CompressionAlgorithm) -> Result<()> {
    // TODO: Implement ZSTD compression
    // For now, just use LZ4
    panic!("ZSTD not implemented yet");
}

// ✅ CORRECT - Rust
pub fn compress(&mut self, algorithm: CompressionAlgorithm) -> Result<()> {
    let compressed = match algorithm {
        CompressionAlgorithm::Lz4 => {
            lz4::block::compress(&self.values, None, false)
                .context("LZ4 compression failed")?
        }
        CompressionAlgorithm::Zstd => {
            zstd::encode_all(&self.values[..], 3)
                .context("ZSTD compression failed")?
        }
    };

    self.values = compressed;
    Ok(())
}
```

```python
# ❌ WRONG - Python
def train_model(self, dataset: ray.data.Dataset) -> Dict[str, Any]:
    # TODO: Implement distributed training
    # For now, return fake metrics
    return {"loss": 0.5}  # Simulated response

# ✅ CORRECT - Python
def train_model(self, dataset: ray.data.Dataset) -> Dict[str, Any]:
    scaling_config = ScalingConfig(
        num_workers=self.config.num_workers,
        use_gpu=self.config.use_gpu
    )

    trainer = train.TorchTrainer(
        train_loop_per_worker=self._train_loop,
        scaling_config=scaling_config,
        datasets={"train": dataset}
    )

    result = trainer.fit()
    return result.metrics
```

---

## Centralized Secrets Management (ALL Languages)

### Mandatory Requirements

**ALL secrets MUST be stored in Secrets Manager** - No exceptions across any language or service.

**FORBIDDEN:**
```bash
# ❌ WRONG - Hardcoded secrets
JWT_SECRET="my-secret-key-123"
DATABASE_PASSWORD="postgres123"
API_KEY="sk-1234567890"
```

**REQUIRED:**
```bash
# ✅ CORRECT - Only bootstrap credentials in environment
VAULT_ADDR=http://secrets-manager:8200
VAULT_ROLE_ID=${VAULT_ROLE_ID}  # From secure deployment
VAULT_SECRET_ID=${VAULT_SECRET_ID}  # From secure deployment
```

### Secrets Manager Integration by Language

#### .NET Services

```csharp
// Program.cs - Service startup
using GBMM.SecretsManager.Client;

var builder = WebApplication.CreateBuilder(args);

// Initialize Secrets Manager client
var vaultAddr = Environment.GetEnvironmentVariable("VAULT_ADDR")
    ?? "http://secrets-manager:8200";
var roleId = Environment.GetEnvironmentVariable("VAULT_ROLE_ID")
    ?? throw new InvalidOperationException("VAULT_ROLE_ID required");
var secretId = Environment.GetEnvironmentVariable("VAULT_SECRET_ID")
    ?? throw new InvalidOperationException("VAULT_SECRET_ID required");

var secretsClient = new SecretsManagerClient(vaultAddr);

// Authenticate with AppRole
var authToken = await secretsClient.AuthenticateAsync(roleId, secretId);

// Fetch ALL secrets from Secrets Manager
var jwtSecret = await secretsClient.GetSecretAsync("/system/jwt/signing-key", authToken);
var jwtIssuer = await secretsClient.GetSecretAsync("/system/jwt/issuer", authToken);
var jwtAudience = await secretsClient.GetSecretAsync("/system/jwt/audience", authToken);
var dbPassword = await secretsClient.GetSecretAsync("/databases/postgres-main/auth-service", authToken);

// Build connection string with fetched secret
var connectionString = $"Host=postgres;Database=auth_database;Username=auth_user;Password={dbPassword}";

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// Configure JWT with fetched secrets
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var key = Encoding.UTF8.GetBytes(jwtSecret);
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            // ... rest of configuration
        };
    });

// Register secrets client for runtime secret access
builder.Services.AddSingleton<ISecretsManagerClient>(sp => secretsClient);

var app = builder.Build();
app.Run();
```

#### Go Services (NCS, NRM, NMO, NSL)

```go
// cmd/server/main.go
package main

import (
    "context"
    "fmt"
    "os"

    vault "github.com/hashicorp/vault/api"
    "github.com/rs/zerolog/log"
)

func main() {
    ctx := context.Background()

    // Initialize Vault client
    vaultAddr := os.Getenv("VAULT_ADDR")
    if vaultAddr == "" {
        vaultAddr = "http://secrets-manager:8200"
    }

    config := vault.DefaultConfig()
    config.Address = vaultAddr

    client, err := vault.NewClient(config)
    if err != nil {
        log.Fatal().Err(err).Msg("failed to create vault client")
    }

    // Authenticate with AppRole
    roleID := os.Getenv("VAULT_ROLE_ID")
    secretID := os.Getenv("VAULT_SECRET_ID")

    if roleID == "" || secretID == "" {
        log.Fatal().Msg("VAULT_ROLE_ID and VAULT_SECRET_ID required")
    }

    loginData := map[string]interface{}{
        "role_id":   roleID,
        "secret_id": secretID,
    }

    resp, err := client.Logical().Write("auth/approle/login", loginData)
    if err != nil {
        log.Fatal().Err(err).Msg("failed to authenticate with vault")
    }

    client.SetToken(resp.Auth.ClientToken)

    // Fetch secrets
    dbSecret, err := client.Logical().Read("/databases/postgres-main/ncs-service")
    if err != nil {
        log.Fatal().Err(err).Msg("failed to read database secret")
    }

    dbPassword := dbSecret.Data["password"].(string)

    // Build connection string
    connStr := fmt.Sprintf("host=postgres port=5432 user=ncs_user password=%s dbname=coordination_db sslmode=disable", dbPassword)

    // Use in application
    db, err := sql.Open("postgres", connStr)
    if err != nil {
        log.Fatal().Err(err).Msg("failed to connect to database")
    }

    log.Info().Msg("successfully connected to database with secrets from vault")

    // Rest of application startup
}
```

#### Rust Services (Storage Engine)

```rust
// src/main.rs
use anyhow::{Context, Result};
use reqwest::Client;
use serde::{Deserialize, Serialize};
use std::env;

#[derive(Serialize)]
struct AppRoleLogin {
    role_id: String,
    secret_id: String,
}

#[derive(Deserialize)]
struct VaultAuthResponse {
    auth: VaultAuth,
}

#[derive(Deserialize)]
struct VaultAuth {
    client_token: String,
}

#[derive(Deserialize)]
struct SecretData {
    data: std::collections::HashMap<String, String>,
}

async fn get_secret_from_vault(path: &str, token: &str) -> Result<String> {
    let vault_addr = env::var("VAULT_ADDR")
        .unwrap_or_else(|_| "http://secrets-manager:8200".to_string());

    let client = Client::new();
    let url = format!("{}/v1{}", vault_addr, path);

    let response = client
        .get(&url)
        .header("X-Vault-Token", token)
        .send()
        .await
        .context("failed to fetch secret")?;

    let secret: SecretData = response.json().await.context("failed to parse secret")?;

    secret.data.get("value")
        .cloned()
        .ok_or_else(|| anyhow::anyhow!("secret value not found"))
}

#[tokio::main]
async fn main() -> Result<()> {
    // Authenticate with Vault
    let role_id = env::var("VAULT_ROLE_ID")
        .context("VAULT_ROLE_ID not set")?;
    let secret_id = env::var("VAULT_SECRET_ID")
        .context("VAULT_SECRET_ID not set")?;

    let vault_addr = env::var("VAULT_ADDR")
        .unwrap_or_else(|_| "http://secrets-manager:8200".to_string());

    let client = Client::new();
    let login_url = format!("{}/v1/auth/approle/login", vault_addr);

    let login = AppRoleLogin { role_id, secret_id };

    let auth_response: VaultAuthResponse = client
        .post(&login_url)
        .json(&login)
        .send()
        .await?
        .json()
        .await?;

    let vault_token = auth_response.auth.client_token;

    // Fetch database password
    let db_password = get_secret_from_vault(
        "/databases/postgres-main/storage-service",
        &vault_token
    ).await?;

    // Build connection string
    let database_url = format!(
        "postgres://storage_user:{}@postgres:5432/storage_db",
        db_password
    );

    // Connect to database
    let pool = sqlx::PgPool::connect(&database_url).await?;

    println!("Successfully connected to database with secrets from vault");

    // Rest of application startup

    Ok(())
}
```

#### Python Services (ML Platform, NCF)

```python
# gbmm_ml/config.py
import os
import hvac
from typing import Dict, Any

class SecretsManager:
    """Client for fetching secrets from Secrets Manager"""

    def __init__(self):
        self.vault_addr = os.getenv("VAULT_ADDR", "http://secrets-manager:8200")
        self.client = hvac.Client(url=self.vault_addr)

    def authenticate(self) -> None:
        """Authenticate with AppRole"""
        role_id = os.getenv("VAULT_ROLE_ID")
        secret_id = os.getenv("VAULT_SECRET_ID")

        if not role_id or not secret_id:
            raise ValueError("VAULT_ROLE_ID and VAULT_SECRET_ID required")

        self.client.auth.approle.login(
            role_id=role_id,
            secret_id=secret_id
        )

    def get_secret(self, path: str) -> str:
        """Fetch a secret from Vault"""
        secret = self.client.secrets.kv.v2.read_secret_version(path=path)
        return secret['data']['data']['value']

# server.py
from gbmm_ml.config import SecretsManager
import ray

def main():
    # Initialize secrets manager
    secrets = SecretsManager()
    secrets.authenticate()

    # Fetch secrets
    db_password = secrets.get_secret("databases/postgres-main/ml-platform-service")
    minio_access_key = secrets.get_secret("storage/minio/access-key")
    minio_secret_key = secrets.get_secret("storage/minio/secret-key")

    # Build connection string
    database_url = f"postgresql://ml_user:{db_password}@postgres:5432/ml_platform_db"

    # Initialize Ray with MinIO credentials
    ray.init(
        storage=f"s3://ml-models",
        _system_config={
            "object_store_memory": 10**9,
            "automatic_object_spilling_enabled": True,
            "object_spilling_config": {
                "type": "filesystem",
                "params": {
                    "directory_path": "/tmp/spill"
                }
            }
        }
    )

    print("Successfully initialized with secrets from vault")

    # Rest of application

if __name__ == "__main__":
    main()
```

#### Scala Services (Query Engine)

```scala
// src/main/scala/com/gbmm/query/config/SecretsManager.scala
package com.gbmm.query.config

import cats.effect.IO
import io.circe.generic.auto._
import io.circe.parser._
import org.http4s._
import org.http4s.client.Client
import org.http4s.client.dsl.io._
import org.http4s.circe._

case class VaultAuthResponse(auth: VaultAuth)
case class VaultAuth(client_token: String)
case class SecretData(data: Map[String, Map[String, String]])

class SecretsManager(client: Client[IO]) {

  private val vaultAddr = sys.env.getOrElse("VAULT_ADDR", "http://secrets-manager:8200")

  def authenticate(): IO[String] = {
    val roleId = sys.env.getOrElse("VAULT_ROLE_ID",
      throw new RuntimeException("VAULT_ROLE_ID required"))
    val secretId = sys.env.getOrElse("VAULT_SECRET_ID",
      throw new RuntimeException("VAULT_SECRET_ID required"))

    val loginUri = Uri.unsafeFromString(s"$vaultAddr/v1/auth/approle/login")

    val request = POST(
      Map("role_id" -> roleId, "secret_id" -> secretId),
      loginUri
    )

    client.expect[VaultAuthResponse](request).map(_.auth.client_token)
  }

  def getSecret(path: String, token: String): IO[String] = {
    val secretUri = Uri.unsafeFromString(s"$vaultAddr/v1$path")

    val request = GET(
      secretUri,
      Header("X-Vault-Token", token)
    )

    client.expect[SecretData](request).map(_.data("data")("value"))
  }
}

// src/main/scala/com/gbmm/query/Main.scala
package com.gbmm.query

import cats.effect.{IO, IOApp}
import org.http4s.blaze.client.BlazeClientBuilder
import com.gbmm.query.config.SecretsManager

object Main extends IOApp.Simple {

  def run: IO[Unit] = {
    BlazeClientBuilder[IO].resource.use { httpClient =>
      val secrets = new SecretsManager(httpClient)

      for {
        // Authenticate with Vault
        token <- secrets.authenticate()

        // Fetch secrets
        dbPassword <- secrets.getSecret("/databases/postgres-main/query-engine-service", token)
        jwtSecret <- secrets.getSecret("/system/jwt/signing-key", token)

        // Build connection string
        connectionString = s"jdbc:postgresql://postgres:5432/query_engine_db?user=query_user&password=$dbPassword"

        _ <- IO.println("Successfully fetched secrets from vault")

        // Rest of application startup

      } yield ()
    }
  }
}
```

### Secret Rotation Handling

All services MUST support secret rotation without restart:

```csharp
// .NET - Background service for secret refresh
public class SecretRefreshService : BackgroundService
{
    private readonly ISecretsManagerClient _secretsClient;
    private readonly IConfiguration _configuration;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);

            // Refresh secrets
            var newJwtSecret = await _secretsClient.GetSecretAsync("/system/jwt/signing-key");

            // Update configuration dynamically
            _configuration["Jwt:Secret"] = newJwtSecret;

            _logger.LogInformation("Secrets refreshed successfully");
        }
    }
}
```

---

## Hybrid Architecture Overview

### Technology Stack by Component

The GBMM platform uses a **hybrid polyglot architecture** where each component is built with the best tool for its specific requirements:

| Component | Language | Purpose | Rationale |
|-----------|----------|---------|-----------|
| **Core .NET Services** | C# (.NET 8) | Auth, Catalog, Secrets | Excellent for APIs, business logic, CRUD operations |
| **NCS** (Coordination Service) | Go 1.21+ | Service discovery, consensus | Low latency, excellent concurrency, battle-tested (K8s, etcd) |
| **NRM** (Resource Manager) | Go 1.21+ | Resource scheduling, auto-scaling | Efficient memory usage, fast compilation |
| **NMO** (Monitoring & Observability) | Go 1.21+ | Metrics, tracing, logging | High throughput, low overhead |
| **NSL** (Security Layer) | Go 1.21+ | Authentication, authorization, encryption | Secure, fast, proven in security tools |
| **NCF** (Compute Framework) | Python 3.11+ / C++ 17 | Distributed computing | Python for API, C++ for performance-critical paths |
| **Storage Engine** | Rust | Columnar storage, compression | Memory safety, zero-cost abstractions, SIMD |
| **Query Engine** | Scala 3 / Java 17 | SQL optimization, federation | Apache Calcite integration, JVM ecosystem |
| **ML Platform** | Python 3.11+ / Ray | Distributed ML, training | Ray for distributed ML, scikit-learn, PyTorch |
| **Stream Processing** | Rust / Flink | Real-time processing | Rust for custom processors, Flink for CEP |

### Integration Principles

**All services MUST:**
1. Expose **gRPC APIs** for high-performance inter-service communication
2. Provide **.NET client libraries** for seamless integration
3. Implement **OpenTelemetry** for distributed tracing
4. Use **Protocol Buffers** for serialization
5. Register with **NCS** (Coordination Service) for service discovery
6. Report metrics to **NMO** (Monitoring & Observability)
7. Use **NSL** (Security Layer) for authentication/authorization

### Architecture Layers

```
┌──────────────────────────────────────────────────────────────┐
│  Phase 1: Core Infrastructure (Foundation - Go)              │
│  NCS │ NRM │ NCF │ NMO │ NSL                                 │
└──────────────────────────────────────────────────────────────┘
                            ▼
┌──────────────────────────────────────────────────────────────┐
│  Phase 2: Storage Layer (Rust)                               │
│  Custom Columnar Storage │ Compression │ Indexing            │
└──────────────────────────────────────────────────────────────┘
                            ▼
┌──────────────────────────────────────────────────────────────┐
│  Phase 3: Query Layer (Scala/Java)                           │
│  Query Optimization │ Federation │ Calcite                   │
└──────────────────────────────────────────────────────────────┘
                            ▼
┌──────────────────────────────────────────────────────────────┐
│  Phase 4: Compute & ML (Python/Ray)                          │
│  Distributed Training │ AutoML │ Model Serving               │
└──────────────────────────────────────────────────────────────┘
                            ▼
┌──────────────────────────────────────────────────────────────┐
│  Phase 5: Stream Processing (Rust/Flink)                     │
│  Real-time CEP │ Windowing │ State Management                │
└──────────────────────────────────────────────────────────────┘
```

---

## .NET Microservices Guidelines

### When to Use .NET

Use .NET 8 for:
- ✅ REST API services
- ✅ Business logic and CRUD operations
- ✅ Authentication and authorization
- ✅ Metadata catalog management
- ✅ Secrets management
- ✅ Service orchestration

### Project Structure

Every microservice MUST follow this directory structure:

```
{service-name}/
├── Authentication/          # Custom authentication schemes
├── Controllers/            # API controllers
│   └── BaseApiController.cs
├── Data/                   # DbContext and database configuration
├── DTOs/                   # Request/Response models
├── Extensions/             # Service collection extensions
├── Helpers/               # Utility classes
├── Integration/           # External service clients
├── Middleware/            # Custom middleware
├── Models/                # Entity models and constants
├── Services/              # Business logic
├── Templates/             # Email/notification templates (if needed)
├── appsettings.json       # Configuration
├── Program.cs             # Application entry point
├── Dockerfile             # Container configuration
└── {ServiceName}.csproj   # Project file
```

### Project File Structure

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <DockerDefaultTargetOS>Linux</DockerDefaultTargetOS>
  </PropertyGroup>

  <ItemGroup>
    <InternalsVisibleTo Include="{service-name}.IntegrationTests" />
  </ItemGroup>

  <ItemGroup>
    <!-- Core packages -->
    <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="8.0.11" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="8.0.0" />
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="8.0.0" />
    <PackageReference Include="Swashbuckle.AspNetCore" Version="6.5.0" />

    <!-- Logging -->
    <PackageReference Include="Serilog.AspNetCore" Version="8.0.3" />
    <PackageReference Include="Serilog.Sinks.Console" Version="6.0.0" />
    <PackageReference Include="Serilog.Sinks.File" Version="6.0.0" />

    <!-- OpenTelemetry -->
    <PackageReference Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" Version="1.9.0" />
    <PackageReference Include="OpenTelemetry.Extensions.Hosting" Version="1.9.0" />
    <PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore" Version="1.9.0" />
    <PackageReference Include="OpenTelemetry.Instrumentation.Http" Version="1.9.0" />
    <PackageReference Include="OpenTelemetry.Instrumentation.EntityFrameworkCore" Version="1.0.0-beta.12" />

    <!-- Health checks -->
    <PackageReference Include="AspNetCore.HealthChecks.NpgSql" Version="8.0.0" />
    <PackageReference Include="AspNetCore.HealthChecks.Redis" Version="8.0.0" />
    <PackageReference Include="AspNetCore.HealthChecks.Rabbitmq" Version="8.0.0" />

    <!-- HTTP client resilience -->
    <PackageReference Include="Microsoft.Extensions.Http.Polly" Version="8.0.0" />
    <PackageReference Include="Polly" Version="8.2.0" />
  </ItemGroup>

  <ItemGroup>
    <!-- MANDATORY: Shared Libraries -->
    <ProjectReference Include="..\shared-libraries\GBMM.Common\GBMM.Common.csproj" />
    <ProjectReference Include="..\shared-libraries\GBMM.Contracts\GBMM.Contracts.csproj" />
    <ProjectReference Include="..\shared-libraries\GBMM.Security\GBMM.Security.csproj" />
    <ProjectReference Include="..\shared-libraries\GBMM.Observability\GBMM.Observability.csproj" />
  </ItemGroup>

</Project>
```

---

## Shared Libraries

### Mandatory Usage

**ALL services MUST use the shared libraries. Code duplication is PROHIBITED.**

| Library | Purpose | Must Use For |
|---------|---------|-------------|
| `GBMM.Common` | Common utilities | `Result<T>`, `ApiResponse<T>`, `PagedResult<T>`, Extensions, Validation |
| `GBMM.Security` | Security utilities | JWT validation, ABAC policies, Encryption |
| `GBMM.Observability` | Metrics & tracing | `MetricsCollector`, Logging, Health checks |
| `GBMM.Contracts` | Shared DTOs | Cross-service communication DTOs |
| `GBMM.Storage.Abstractions` | Storage interfaces | Storage providers, Repository patterns |

### GBMM.Common Usage

```csharp
using GBMM.Common;

// Use Result<T> for operation results
public async Task<Result<UserDto>> GetUserAsync(string userId)
{
    var user = await _context.Users.FindAsync(userId);

    if (user == null)
        return Result.Failure<UserDto>("User not found", "USER_NOT_FOUND");

    return Result.Success(MapToDto(user));
}

// Use ApiResponse<T> for API responses
[HttpGet("{id}")]
public async Task<ActionResult<ApiResponse<UserDto>>> GetUser(string id)
{
    var result = await _userService.GetUserAsync(id);

    if (result.IsFailure)
        return NotFound(ApiResponse<UserDto>.NotFound(result.Error, result.ErrorCode));

    return Ok(ApiResponse<UserDto>.Ok(result.Value));
}

// Use PagedResult<T> for paginated responses
public async Task<PagedResult<UserDto>> GetUsersAsync(int page, int pageSize)
{
    var query = _context.Users.AsQueryable();
    var total = await query.CountAsync();
    var items = await query
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .Select(u => MapToDto(u))
        .ToListAsync();

    return new PagedResult<UserDto>(items, total, page, pageSize);
}
```

### GBMM.Observability Usage

```csharp
using GBMM.Observability;

public class UserService
{
    private readonly MetricsCollector _metrics;
    private readonly ILogger<UserService> _logger;

    public UserService(MetricsCollector metrics, ILogger<UserService> logger)
    {
        _metrics = metrics;
        _logger = logger;
    }

    public async Task<Result<UserDto>> CreateUserAsync(CreateUserRequest request)
    {
        // Use metrics for operation tracking
        return await _metrics.TrackOperationAsync(
            "user_create",
            async () =>
            {
                // Implementation
                var user = new ApplicationUser { /* ... */ };
                await _context.Users.AddAsync(user);
                await _context.SaveChangesAsync();

                _logger.LogInformation("User created: {UserId}", user.Id);
                return Result.Success(MapToDto(user));
            },
            ex => _logger.LogError(ex, "Failed to create user")
        );
    }
}
```

### GBMM.Security Usage

```csharp
using GBMM.Security;
using GBMM.Security.ABAC;

// Register in Program.cs
builder.Services.AddSingleton(sp => new JwtTokenService(builder.Configuration));
builder.Services.AddSingleton<PolicyEngine>();
builder.Services.AddSingleton<IAuthorizationHandler, AttributeBasedAuthorizationHandler>();

// Use in services
public class DataAccessService
{
    private readonly PolicyEngine _policyEngine;

    public async Task<Result<DataDto>> GetSensitiveDataAsync(ClaimsPrincipal user, string dataId)
    {
        var context = new PolicyContext
        {
            User = user,
            Resource = await _repository.GetMetadataAsync(dataId),
            Action = "read",
            Environment = new { TimeOfDay = DateTime.UtcNow.Hour }
        };

        if (!await _policyEngine.EvaluateAsync(context))
            return Result.Failure<DataDto>("Access denied", "ACCESS_DENIED");

        return await _repository.GetDataAsync(dataId);
    }
}
```

### Extension Methods for Service Registration

Create an `Extensions/SharedLibrariesExtensions.cs` file in each service:

```csharp
using GBMM.Observability;
using GBMM.Security;
using GBMM.Security.ABAC;
using Microsoft.AspNetCore.Authorization;

namespace {ServiceName}.Extensions;

public static class SharedLibrariesExtensions
{
    public static IServiceCollection AddGbmmObservability(
        this IServiceCollection services,
        string serviceName)
    {
        services.AddSingleton(sp => new MetricsCollector(serviceName));
        return services;
    }

    public static IServiceCollection AddGbmmSecurity(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton(sp => new JwtTokenService(configuration));
        services.AddSingleton<PolicyEngine>();
        services.AddSingleton<IAuthorizationHandler, AttributeBasedAuthorizationHandler>();
        return services;
    }
}
```

---

## Controllers

### Base Controller Pattern

**ALL controllers MUST inherit from `BaseApiController`:**

```csharp
using GBMM.Common;
using GBMM.Observability;
using Microsoft.AspNetCore.Mvc;

namespace {ServiceName}.Controllers;

[ApiController]
public abstract class BaseApiController : ControllerBase
{
    protected readonly MetricsCollector Metrics;
    protected readonly ILogger Logger;

    protected BaseApiController(MetricsCollector metrics, ILogger logger)
    {
        Metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected async Task<ActionResult<ApiResponse<T>>> ExecuteWithMetricsAsync<T>(
        string operationName,
        Func<Task<Result<T>>> operation,
        int successStatusCode = 200)
    {
        using (Metrics.MeasureOperation(operationName))
        {
            try
            {
                var result = await operation();

                if (result.IsSuccess)
                {
                    Metrics.RecordOperation(operationName, "success");
                    var response = ApiResponse<T>.FromResult(result, successStatusCode);
                    return StatusCode(response.StatusCode, response);
                }
                else
                {
                    Metrics.RecordOperation(operationName, "failure");
                    var response = ApiResponse<T>.FromResult(result);
                    return StatusCode(response.StatusCode, response);
                }
            }
            catch (Exception ex)
            {
                Metrics.RecordError(ex.GetType().Name, operationName);
                Metrics.RecordOperation(operationName, "error");
                Logger.LogError(ex, "Error executing operation {OperationName}", operationName);

                var response = ApiResponse<T>.InternalError($"An error occurred: {ex.Message}");
                return StatusCode(response.StatusCode, response);
            }
        }
    }

    protected ActionResult<ApiResponse<T>> OkResponse<T>(T data, Dictionary<string, object>? metadata = null)
    {
        return Ok(ApiResponse<T>.Ok(data, metadata));
    }

    protected ActionResult<ApiResponse<T>> CreatedResponse<T>(T data, string? location = null)
    {
        return StatusCode(201, ApiResponse<T>.Created(data, location));
    }

    protected ActionResult<ApiResponse<T>> BadRequestResponse<T>(string error, string? errorCode = null)
    {
        return BadRequest(ApiResponse<T>.BadRequest(error, errorCode));
    }

    protected ActionResult<ApiResponse<T>> UnauthorizedResponse<T>(string error = "Unauthorized", string? errorCode = null)
    {
        return Unauthorized(ApiResponse<T>.Unauthorized(error, errorCode));
    }

    protected ActionResult<ApiResponse<T>> NotFoundResponse<T>(string error = "Resource not found", string? errorCode = null)
    {
        return NotFound(ApiResponse<T>.NotFound(error, errorCode));
    }

    protected ActionResult<ApiResponse<T>> ConflictResponse<T>(string error, string? errorCode = null)
    {
        return Conflict(ApiResponse<T>.Conflict(error, errorCode));
    }
}
```

### Controller Implementation Pattern

```csharp
using {ServiceName}.DTOs;
using {ServiceName}.Services;
using GBMM.Common;
using GBMM.Observability;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace {ServiceName}.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ResourceController : BaseApiController
{
    private readonly IResourceService _resourceService;
    private readonly IAuditService _auditService;

    public ResourceController(
        IResourceService resourceService,
        IAuditService auditService,
        MetricsCollector metrics,
        ILogger<ResourceController> logger)
        : base(metrics, logger)
    {
        _resourceService = resourceService;
        _auditService = auditService;
    }

    /// <summary>
    /// Get all resources with pagination
    /// </summary>
    [HttpGet]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<ResourceDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<ResourceDto>>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<PagedResult<ResourceDto>>>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        return await ExecuteWithMetricsAsync(
            "resource_list",
            async () => await _resourceService.GetAllAsync(page, pageSize)
        );
    }

    /// <summary>
    /// Get resource by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<ResourceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ResourceDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ResourceDto>>> GetById(Guid id)
    {
        return await ExecuteWithMetricsAsync(
            "resource_get",
            async () => await _resourceService.GetByIdAsync(id)
        );
    }

    /// <summary>
    /// Create a new resource
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "ManageResource")]
    [ProducesResponseType(typeof(ApiResponse<ResourceDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<ResourceDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<ResourceDto>>> Create([FromBody] CreateResourceRequest request)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        return await ExecuteWithMetricsAsync(
            "resource_create",
            async () =>
            {
                var result = await _resourceService.CreateAsync(request, userId!);

                if (result.IsSuccess)
                {
                    await _auditService.LogAsync(
                        userId!,
                        "resource.created",
                        "Resource",
                        result.Value.Id.ToString()
                    );
                }

                return result;
            },
            successStatusCode: 201
        );
    }

    /// <summary>
    /// Update an existing resource
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "ManageResource")]
    [ProducesResponseType(typeof(ApiResponse<ResourceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ResourceDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ResourceDto>>> Update(
        Guid id,
        [FromBody] UpdateResourceRequest request)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        return await ExecuteWithMetricsAsync(
            "resource_update",
            async () =>
            {
                var result = await _resourceService.UpdateAsync(id, request);

                if (result.IsSuccess)
                {
                    await _auditService.LogAsync(
                        userId!,
                        "resource.updated",
                        "Resource",
                        id.ToString()
                    );
                }

                return result;
            }
        );
    }

    /// <summary>
    /// Delete a resource
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "DeleteResource")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        using (Metrics.MeasureOperation("resource_delete"))
        {
            var result = await _resourceService.DeleteAsync(id);

            if (result.IsFailure)
            {
                Metrics.RecordOperation("resource_delete", "failure");
                return NotFound(ApiResponse<object>.NotFound(result.Error));
            }

            await _auditService.LogAsync(
                userId!,
                "resource.deleted",
                "Resource",
                id.ToString()
            );

            Metrics.RecordOperation("resource_delete", "success");
            return NoContent();
        }
    }
}
```

---

## Services

### Service Interface Pattern

```csharp
using GBMM.Common;

namespace {ServiceName}.Services;

public interface IResourceService
{
    Task<Result<PagedResult<ResourceDto>>> GetAllAsync(int page, int pageSize);
    Task<Result<ResourceDto>> GetByIdAsync(Guid id);
    Task<Result<ResourceDto>> CreateAsync(CreateResourceRequest request, string userId);
    Task<Result<ResourceDto>> UpdateAsync(Guid id, UpdateResourceRequest request);
    Task<Result> DeleteAsync(Guid id);
}
```

### Service Implementation Pattern

```csharp
using GBMM.Common;
using GBMM.Observability;
using Microsoft.EntityFrameworkCore;

namespace {ServiceName}.Services;

public class ResourceService : IResourceService
{
    private readonly AppDbContext _context;
    private readonly MetricsCollector _metrics;
    private readonly ILogger<ResourceService> _logger;

    public ResourceService(
        AppDbContext context,
        MetricsCollector metrics,
        ILogger<ResourceService> logger)
    {
        _context = context;
        _metrics = metrics;
        _logger = logger;
    }

    public async Task<Result<PagedResult<ResourceDto>>> GetAllAsync(int page, int pageSize)
    {
        try
        {
            var query = _context.Resources
                .Where(r => r.IsActive)
                .OrderByDescending(r => r.CreatedAt);

            var total = await query.CountAsync();
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(r => MapToDto(r))
                .ToListAsync();

            return Result.Success(new PagedResult<ResourceDto>(items, total, page, pageSize));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve resources");
            return Result.Failure<PagedResult<ResourceDto>>("Failed to retrieve resources", "QUERY_ERROR");
        }
    }

    public async Task<Result<ResourceDto>> GetByIdAsync(Guid id)
    {
        var resource = await _context.Resources
            .Include(r => r.Owner)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (resource == null)
            return Result.Failure<ResourceDto>("Resource not found", "NOT_FOUND");

        return Result.Success(MapToDto(resource));
    }

    public async Task<Result<ResourceDto>> CreateAsync(CreateResourceRequest request, string userId)
    {
        // Validation
        if (string.IsNullOrWhiteSpace(request.Name))
            return Result.Failure<ResourceDto>("Name is required", "VALIDATION_ERROR");

        // Check for duplicates
        var exists = await _context.Resources
            .AnyAsync(r => r.Name == request.Name && r.OwnerId == userId);

        if (exists)
            return Result.Failure<ResourceDto>("Resource with this name already exists", "DUPLICATE");

        var resource = new Resource
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            OwnerId = userId,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        _context.Resources.Add(resource);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Resource created: {ResourceId} by {UserId}", resource.Id, userId);

        return Result.Success(MapToDto(resource));
    }

    public async Task<Result<ResourceDto>> UpdateAsync(Guid id, UpdateResourceRequest request)
    {
        var resource = await _context.Resources.FindAsync(id);

        if (resource == null)
            return Result.Failure<ResourceDto>("Resource not found", "NOT_FOUND");

        // Apply updates
        if (!string.IsNullOrWhiteSpace(request.Name))
            resource.Name = request.Name;

        if (request.Description != null)
            resource.Description = request.Description;

        if (request.IsActive.HasValue)
            resource.IsActive = request.IsActive.Value;

        resource.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Resource updated: {ResourceId}", resource.Id);

        return Result.Success(MapToDto(resource));
    }

    public async Task<Result> DeleteAsync(Guid id)
    {
        var resource = await _context.Resources.FindAsync(id);

        if (resource == null)
            return Result.Failure("Resource not found", "NOT_FOUND");

        _context.Resources.Remove(resource);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Resource deleted: {ResourceId}", id);

        return Result.Success();
    }

    private static ResourceDto MapToDto(Resource resource) => new()
    {
        Id = resource.Id,
        Name = resource.Name,
        Description = resource.Description,
        OwnerId = resource.OwnerId,
        OwnerName = resource.Owner?.UserName ?? string.Empty,
        CreatedAt = resource.CreatedAt,
        UpdatedAt = resource.UpdatedAt,
        IsActive = resource.IsActive
    };
}
```

---

## Models

### Entity Model Pattern

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace {ServiceName}.Models;

/// <summary>
/// Resource entity with full audit trail
/// </summary>
public class Resource
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public string OwnerId { get; set; } = string.Empty;

    [ForeignKey(nameof(OwnerId))]
    public ApplicationUser? Owner { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public bool IsActive { get; set; } = true;

    // Navigation properties
    public ICollection<ResourceTag> Tags { get; set; } = new List<ResourceTag>();
}
```

### Constants Pattern

```csharp
namespace {ServiceName}.Models;

/// <summary>
/// System-wide roles
/// </summary>
public static class Roles
{
    public const string PlatformAdmin = "PlatformAdmin";
    public const string SystemAdmin = "SystemAdmin";
    public const string User = "User";
    public const string Viewer = "Viewer";

    public static string[] GetAllRoles() => new[]
    {
        PlatformAdmin, SystemAdmin, User, Viewer
    };
}

/// <summary>
/// Permission constants
/// </summary>
public static class Permissions
{
    public const string ViewResource = "resource.view";
    public const string ManageResource = "resource.manage";
    public const string DeleteResource = "resource.delete";

    public static string[] GetAllPermissions() => new[]
    {
        ViewResource, ManageResource, DeleteResource
    };
}
```

---

## DTOs

### DTO Pattern

```csharp
using System.ComponentModel.DataAnnotations;

namespace {ServiceName}.DTOs;

// ==================== Request DTOs ====================

public class CreateResourceRequest
{
    [Required, MinLength(3), MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public List<string> Tags { get; set; } = new();
}

public class UpdateResourceRequest
{
    [MinLength(3), MaxLength(200)]
    public string? Name { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }

    public bool? IsActive { get; set; }
}

// ==================== Response DTOs ====================

public class ResourceDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string OwnerId { get; set; } = string.Empty;
    public string OwnerName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsActive { get; set; }
    public List<string> Tags { get; set; } = new();
}

// ==================== Query DTOs ====================

public class ResourceQuery
{
    public string? SearchTerm { get; set; }
    public bool? IsActive { get; set; }
    public string? OwnerId { get; set; }
    public DateTime? CreatedAfter { get; set; }
    public DateTime? CreatedBefore { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string SortBy { get; set; } = "CreatedAt";
    public bool SortDescending { get; set; } = true;
}
```

**NOTE:** Common DTOs like `ApiResponse<T>` and `PagedResult<T>` are in `GBMM.Common`. Do NOT duplicate them.

---

## Database & Entity Framework

### DbContext Pattern

```csharp
using Microsoft.EntityFrameworkCore;

namespace {ServiceName}.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Resource> Resources { get; set; }
    public DbSet<ResourceTag> ResourceTags { get; set; }
    public DbSet<AuditLog> AuditLogs { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Resource configuration
        builder.Entity<Resource>()
            .HasIndex(r => r.Name);

        builder.Entity<Resource>()
            .HasIndex(r => r.OwnerId);

        builder.Entity<Resource>()
            .HasOne(r => r.Owner)
            .WithMany()
            .HasForeignKey(r => r.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        // ResourceTag configuration
        builder.Entity<ResourceTag>()
            .HasOne(rt => rt.Resource)
            .WithMany(r => r.Tags)
            .HasForeignKey(rt => rt.ResourceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ResourceTag>()
            .HasIndex(rt => new { rt.ResourceId, rt.TagName })
            .IsUnique();

        // AuditLog configuration
        builder.Entity<AuditLog>()
            .HasIndex(al => al.CreatedAt);

        builder.Entity<AuditLog>()
            .HasIndex(al => new { al.UserId, al.CreatedAt });

        // Seed initial data
        SeedData(builder);
    }

    private void SeedData(ModelBuilder builder)
    {
        // Seed required system data
    }
}
```

### EF Core Best Practices

```csharp
// Use async methods
var items = await _context.Resources.ToListAsync();

// Use projections for read-only queries
var dtos = await _context.Resources
    .Select(r => new ResourceDto { Id = r.Id, Name = r.Name })
    .ToListAsync();

// Use AsNoTracking for read-only queries
var resource = await _context.Resources
    .AsNoTracking()
    .FirstOrDefaultAsync(r => r.Id == id);

// Include navigation properties explicitly
var resource = await _context.Resources
    .Include(r => r.Owner)
    .Include(r => r.Tags)
    .FirstOrDefaultAsync(r => r.Id == id);

// Use indexes for frequently queried columns (in OnModelCreating)
builder.Entity<Resource>()
    .HasIndex(r => r.Name);

// Use JSON columns for nested objects (PostgreSQL)
builder.Entity<Resource>()
    .OwnsOne(r => r.Settings, settings =>
    {
        settings.ToJson();
    });
```

---

## Configuration

### Program.cs Pattern

```csharp
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Polly;
using Polly.Extensions.Http;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// =============================================================================
// Service Configuration
// =============================================================================
var serviceName = "{service-name}";
var serviceVersion = "1.0.0";

// =============================================================================
// Serilog Configuration
// =============================================================================
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("ServiceName", serviceName)
    .Enrich.WithProperty("ServiceVersion", serviceVersion)
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .WriteTo.File($"logs/{serviceName}-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// =============================================================================
// OpenTelemetry Configuration
// =============================================================================
var otelEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT") ?? "http://jaeger:4317";
var enableTracing = Environment.GetEnvironmentVariable("ENABLE_TRACING")?.ToLower() != "false";

if (enableTracing)
{
    builder.Services.AddOpenTelemetry()
        .ConfigureResource(resource => resource
            .AddService(serviceName: serviceName, serviceVersion: serviceVersion))
        .WithTracing(tracing => tracing
            .AddAspNetCoreInstrumentation(options =>
            {
                options.RecordException = true;
                options.Filter = ctx => !ctx.Request.Path.StartsWithSegments("/health");
            })
            .AddHttpClientInstrumentation()
            .AddEntityFrameworkCoreInstrumentation()
            .AddOtlpExporter(opts =>
            {
                opts.Endpoint = new Uri(otelEndpoint);
            }));

    Log.Information("OpenTelemetry tracing enabled, exporting to {Endpoint}", otelEndpoint);
}

// =============================================================================
// Core Services
// =============================================================================
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Swagger Configuration
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = $"{serviceName} API",
        Version = "v1",
        Description = $"API for {serviceName}"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// =============================================================================
// Database Configuration
// =============================================================================
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// =============================================================================
// JWT Authentication
// =============================================================================
var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET")
    ?? builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException("JWT_SECRET is required");

var jwtIssuer = Environment.GetEnvironmentVariable("JWT_ISSUER")
    ?? builder.Configuration["Jwt:Issuer"]
    ?? "PostgreSQLCatalogAuth";

var jwtAudience = Environment.GetEnvironmentVariable("JWT_AUDIENCE")
    ?? builder.Configuration["Jwt:Audience"]
    ?? "PostgreSQLCatalogAPI";

if (jwtSecret.Length < 32)
    throw new InvalidOperationException("JWT secret must be at least 32 characters");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    var key = Encoding.UTF8.GetBytes(jwtSecret);
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = true,
        ValidIssuer = jwtIssuer,
        ValidateAudience = true,
        ValidAudience = jwtAudience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };

    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            Log.Error("JWT Authentication failed: {Error}", context.Exception.Message);
            return Task.CompletedTask;
        }
    };
});

// =============================================================================
// Authorization Policies
// =============================================================================
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("PlatformAdmin", policy => policy.RequireRole(Roles.PlatformAdmin));
    options.AddPolicy("ManageResource", policy =>
        policy.RequireRole(Roles.PlatformAdmin, Roles.SystemAdmin));
    options.AddPolicy("ViewResource", policy => policy.RequireAuthenticatedUser());
});

// =============================================================================
// CORS Configuration
// =============================================================================
var corsOrigins = Environment.GetEnvironmentVariable("CORS_ORIGINS")?.Split(",")
    ?? new[] { "http://localhost:3000" };

builder.Services.AddCors(options =>
{
    options.AddPolicy("ConfiguredOrigins", builder =>
    {
        builder.WithOrigins(corsOrigins)
               .AllowAnyMethod()
               .AllowAnyHeader()
               .AllowCredentials();
    });
});

// =============================================================================
// HTTP Clients with Polly Resilience
// =============================================================================
var retryPolicy = HttpPolicyExtensions
    .HandleTransientHttpError()
    .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

var circuitBreakerPolicy = HttpPolicyExtensions
    .HandleTransientHttpError()
    .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30));

builder.Services.AddHttpClient("AuthService", client =>
{
    client.BaseAddress = new Uri(Environment.GetEnvironmentVariable("AUTH_SERVICE_URL")
        ?? "http://auth-service:5000");
    client.DefaultRequestHeaders.Add("X-Service-Name", serviceName);
})
.AddPolicyHandler(retryPolicy)
.AddPolicyHandler(circuitBreakerPolicy);

// =============================================================================
// Health Checks
// =============================================================================
builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString, name: "database", tags: new[] { "db", "postgres" })
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: new[] { "self" });

// =============================================================================
// GBMM Shared Libraries
// =============================================================================
builder.Services.AddSingleton(sp => new GBMM.Observability.MetricsCollector(serviceName));
builder.Services.AddSingleton(sp => new GBMM.Security.JwtTokenService(builder.Configuration));
builder.Services.AddSingleton<GBMM.Security.ABAC.PolicyEngine>();

// =============================================================================
// Application Services
// =============================================================================
builder.Services.AddScoped<IResourceService, ResourceService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

// =============================================================================
// Middleware Pipeline
// =============================================================================
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", $"{serviceName} API V1");
    c.RoutePrefix = "swagger";
});

app.UseSerilogRequestLogging();
app.UseCors(app.Environment.IsDevelopment() ? "AllowAll" : "ConfiguredOrigins");
app.UseAuthentication();
app.UseAuthorization();

// =============================================================================
// Health Check Endpoints
// =============================================================================
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("self")
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = _ => true
});

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("self")
});

app.MapControllers();

// =============================================================================
// Database Initialization
// =============================================================================
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    context.Database.EnsureCreated();
    Log.Information("Database initialized successfully");
}

Log.Information("{ServiceName} started. Version: {Version}", serviceName, serviceVersion);
app.Run();

public partial class Program { }
```

### appsettings.json Pattern

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=postgres;Database=service_db;Username=postgres;Password=postgres"
  },
  "Jwt": {
    "Secret": "your-secret-key-min-32-chars-long-for-production",
    "Issuer": "PostgreSQLCatalogAuth",
    "Audience": "PostgreSQLCatalogAPI",
    "ExpirationHours": 24
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft.AspNetCore": "Warning",
        "Microsoft.EntityFrameworkCore": "Warning"
      }
    }
  },
  "AllowedHosts": "*"
}
```

---

## Authentication & Authorization

### JWT Token Validation

All services MUST use the same JWT configuration:

```csharp
// Environment variables (shared across services)
JWT_SECRET=your-shared-secret-minimum-32-characters
JWT_ISSUER=PostgreSQLCatalogAuth
JWT_AUDIENCE=PostgreSQLCatalogAPI
```

### Policy-Based Authorization

```csharp
// Define policies in Program.cs
builder.Services.AddAuthorization(options =>
{
    // Role-based policies
    options.AddPolicy("PlatformAdmin", policy =>
        policy.RequireRole(Roles.PlatformAdmin));

    // Multi-role policies
    options.AddPolicy("ManageResource", policy =>
        policy.RequireRole(Roles.PlatformAdmin, Roles.SystemAdmin, Roles.ResourceAdmin));

    // Permission-based policies
    options.AddPolicy("ViewSensitiveData", policy =>
        policy.RequireClaim("Permission", "data.sensitive.view"));
});

// Apply in controllers
[HttpGet]
[Authorize(Policy = "ViewResource")]
public async Task<IActionResult> Get() { }

[HttpPost]
[Authorize(Policy = "ManageResource")]
public async Task<IActionResult> Create() { }
```

### Extracting User Context

```csharp
// In controllers
var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
var email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
var roles = User.FindAll(System.Security.Claims.ClaimTypes.Role).Select(c => c.Value).ToList();
var workspaceId = User.FindFirst("WorkspaceId")?.Value;
```

---

## Error Handling

### Consistent Error Response Pattern

```csharp
// Use Result<T> from GBMM.Common
public async Task<Result<ResourceDto>> GetResourceAsync(Guid id)
{
    try
    {
        var resource = await _context.Resources.FindAsync(id);

        if (resource == null)
            return Result.Failure<ResourceDto>("Resource not found", "RESOURCE_NOT_FOUND");

        if (!resource.IsActive)
            return Result.Failure<ResourceDto>("Resource is not active", "RESOURCE_INACTIVE");

        return Result.Success(MapToDto(resource));
    }
    catch (DbUpdateException ex)
    {
        _logger.LogError(ex, "Database error retrieving resource {ResourceId}", id);
        return Result.Failure<ResourceDto>("Database error occurred", "DATABASE_ERROR");
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Unexpected error retrieving resource {ResourceId}", id);
        return Result.Failure<ResourceDto>("An unexpected error occurred", "INTERNAL_ERROR");
    }
}
```

### Error Codes

Use consistent error codes across services:

| Code | Description |
|------|-------------|
| `NOT_FOUND` | Resource not found |
| `DUPLICATE` | Duplicate resource |
| `VALIDATION_ERROR` | Request validation failed |
| `UNAUTHORIZED` | Not authenticated |
| `FORBIDDEN` | Not authorized |
| `DATABASE_ERROR` | Database operation failed |
| `INTERNAL_ERROR` | Unexpected error |
| `CONFLICT` | State conflict |
| `RATE_LIMITED` | Rate limit exceeded |

---

## Logging & Observability

### Structured Logging

```csharp
// Use structured logging with context
_logger.LogInformation(
    "Resource created: {ResourceId} by {UserId} in workspace {WorkspaceId}",
    resource.Id,
    userId,
    workspaceId);

_logger.LogWarning(
    "Unauthorized access attempt to resource {ResourceId} by {UserId}",
    resourceId,
    userId);

_logger.LogError(
    ex,
    "Failed to process request for {ResourceId}: {ErrorMessage}",
    resourceId,
    ex.Message);
```

### Metrics Collection

```csharp
// Use MetricsCollector from GBMM.Observability
public class ResourceService
{
    private readonly MetricsCollector _metrics;

    public async Task<Result<ResourceDto>> CreateAsync(CreateResourceRequest request)
    {
        using (_metrics.MeasureOperation("resource_create"))
        {
            try
            {
                // Implementation
                _metrics.RecordOperation("resource_create", "success");
                return Result.Success(dto);
            }
            catch (Exception ex)
            {
                _metrics.RecordError(ex.GetType().Name, "resource_create");
                _metrics.RecordOperation("resource_create", "failure");
                throw;
            }
        }
    }
}
```

### Audit Logging

```csharp
public interface IAuditService
{
    Task LogAsync(
        string userId,
        string action,
        string entityType,
        string? entityId,
        Guid? workspaceId = null,
        string? ipAddress = null,
        bool success = true,
        string? errorMessage = null);
}

// Usage
await _auditService.LogAsync(
    userId: userId,
    action: "resource.created",
    entityType: "Resource",
    entityId: resource.Id.ToString(),
    workspaceId: workspaceId,
    ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());
```

---

## Middleware

### Custom Middleware Pattern

```csharp
namespace {ServiceName}.Middleware;

public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.Headers["X-Frame-Options"] = "DENY";
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
        context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        context.Response.Headers["Permissions-Policy"] =
            "geolocation=(), microphone=(), camera=()";
        context.Response.Headers["Content-Security-Policy"] =
            "default-src 'self'; script-src 'self' 'unsafe-inline' 'unsafe-eval'; " +
            "style-src 'self' 'unsafe-inline'; img-src 'self' data: https:; " +
            "font-src 'self' data:; connect-src 'self'; frame-ancestors 'none'";

        if (context.Request.IsHttps)
        {
            context.Response.Headers["Strict-Transport-Security"] =
                "max-age=31536000; includeSubDomains; preload";
        }

        context.Response.Headers.Remove("Server");
        context.Response.Headers.Remove("X-Powered-By");

        await _next(context);
    }
}

public static class SecurityHeadersMiddlewareExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<SecurityHeadersMiddleware>();
    }
}
```

---

## HTTP Clients & Resilience

### Polly Resilience Patterns

```csharp
// Retry policy with exponential backoff
var retryPolicy = HttpPolicyExtensions
    .HandleTransientHttpError()
    .WaitAndRetryAsync(3, retryAttempt =>
        TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

// Circuit breaker
var circuitBreakerPolicy = HttpPolicyExtensions
    .HandleTransientHttpError()
    .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30));

// Apply to HTTP client
builder.Services.AddHttpClient<IExternalServiceClient, ExternalServiceClient>("ExternalService")
    .AddPolicyHandler(retryPolicy)
    .AddPolicyHandler(circuitBreakerPolicy);
```

### Service Client Pattern

```csharp
public interface IAuthServiceClient
{
    Task<Result<UserDto>> GetUserAsync(string userId, string authToken);
    Task<Result<bool>> ValidateTokenAsync(string token);
}

public class AuthServiceClient : IAuthServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AuthServiceClient> _logger;

    public AuthServiceClient(HttpClient httpClient, ILogger<AuthServiceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<Result<UserDto>> GetUserAsync(string userId, string authToken)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"/api/users/{userId}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authToken);

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to get user {UserId}: {StatusCode}",
                    userId, response.StatusCode);
                return Result.Failure<UserDto>("Failed to retrieve user", "USER_FETCH_FAILED");
            }

            var content = await response.Content.ReadFromJsonAsync<ApiResponse<UserDto>>();
            return content?.Data != null
                ? Result.Success(content.Data)
                : Result.Failure<UserDto>("User not found", "USER_NOT_FOUND");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling auth service for user {UserId}", userId);
            return Result.Failure<UserDto>("Auth service unavailable", "SERVICE_UNAVAILABLE");
        }
    }
}
```

---

## Health Checks

### Health Check Implementation

```csharp
// Register in Program.cs
builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString, name: "database", tags: new[] { "db", "postgres" })
    .AddRedis(redisConnectionString, name: "redis", tags: new[] { "cache", "redis" })
    .AddRabbitMQ(rabbitMqUri, name: "rabbitmq", tags: new[] { "messaging" })
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: new[] { "self" })
    .AddCheck<ExternalServiceHealthCheck>("external-service", tags: new[] { "external" });

// Custom health check
public class ExternalServiceHealthCheck : IHealthCheck
{
    private readonly HttpClient _httpClient;

    public ExternalServiceHealthCheck(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("ExternalService");
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/health", cancellationToken);
            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy("External service is healthy")
                : HealthCheckResult.Degraded("External service returned non-success status");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("External service is unavailable", ex);
        }
    }
}
```

---

## Security

### Input Validation

```csharp
// Use Data Annotations
public class CreateUserRequest
{
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    [MaxLength(255)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required")]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters")]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).*$",
        ErrorMessage = "Password must contain uppercase, lowercase, and number")]
    public string Password { get; set; } = string.Empty;

    [Required]
    [MinLength(2)]
    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;
}

// Validate in service layer
public async Task<Result<UserDto>> CreateUserAsync(CreateUserRequest request)
{
    // Additional validation beyond data annotations
    if (await _context.Users.AnyAsync(u => u.Email == request.Email))
        return Result.Failure<UserDto>("Email already exists", "DUPLICATE_EMAIL");

    // Sanitize input
    var sanitizedFirstName = request.FirstName.Trim();
    var sanitizedEmail = request.Email.Trim().ToLowerInvariant();

    // Process...
}
```

### SQL Injection Prevention

```csharp
// ALWAYS use parameterized queries
// CORRECT
var user = await _context.Users
    .FirstOrDefaultAsync(u => u.Email == email);

// CORRECT (for raw SQL)
var users = await _context.Users
    .FromSqlInterpolated($"SELECT * FROM Users WHERE Email = {email}")
    .ToListAsync();

// NEVER do this
// WRONG
var query = $"SELECT * FROM Users WHERE Email = '{email}'";
```

### Sensitive Data Handling

```csharp
// Never log sensitive data
// WRONG
_logger.LogInformation("User login: {Email} with password {Password}", email, password);

// CORRECT
_logger.LogInformation("User login attempt: {Email}", email);

// Mask sensitive data in responses
public class UserDto
{
    public string Email { get; set; } = string.Empty;

    // Never expose
    [JsonIgnore]
    public string? PasswordHash { get; set; }
}
```

---

## Testing

### Unit Test Pattern

```csharp
using Xunit;
using Moq;
using FluentAssertions;

public class ResourceServiceTests
{
    private readonly Mock<AppDbContext> _contextMock;
    private readonly Mock<ILogger<ResourceService>> _loggerMock;
    private readonly Mock<MetricsCollector> _metricsMock;
    private readonly ResourceService _sut;

    public ResourceServiceTests()
    {
        _contextMock = new Mock<AppDbContext>();
        _loggerMock = new Mock<ILogger<ResourceService>>();
        _metricsMock = new Mock<MetricsCollector>("test-service");
        _sut = new ResourceService(_contextMock.Object, _metricsMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task GetByIdAsync_WhenResourceExists_ReturnsSuccess()
    {
        // Arrange
        var resourceId = Guid.NewGuid();
        var resource = new Resource { Id = resourceId, Name = "Test" };
        _contextMock.Setup(c => c.Resources.FindAsync(resourceId))
            .ReturnsAsync(resource);

        // Act
        var result = await _sut.GetByIdAsync(resourceId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Test");
    }

    [Fact]
    public async Task GetByIdAsync_WhenResourceNotFound_ReturnsFailure()
    {
        // Arrange
        var resourceId = Guid.NewGuid();
        _contextMock.Setup(c => c.Resources.FindAsync(resourceId))
            .ReturnsAsync((Resource?)null);

        // Act
        var result = await _sut.GetByIdAsync(resourceId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("NOT_FOUND");
    }
}
```

---

## Naming Conventions

### File Naming

| Type | Convention | Example |
|------|------------|---------|
| Controller | `{Entity}Controller.cs` | `UsersController.cs` |
| Service | `{Entity}Service.cs` | `UserService.cs` |
| Interface | `I{Name}.cs` | `IUserService.cs` |
| Model | `{Entity}.cs` | `User.cs` |
| DTO | `{Entity}Dto.cs`, `{Action}{Entity}Request.cs` | `UserDto.cs`, `CreateUserRequest.cs` |
| DbContext | `{Service}DbContext.cs` | `AuthDbContext.cs` |
| Middleware | `{Feature}Middleware.cs` | `SecurityHeadersMiddleware.cs` |

### Class Naming

| Type | Convention | Example |
|------|------------|---------|
| Controller | PascalCase + Controller suffix | `UsersController` |
| Service | PascalCase + Service suffix | `UserService` |
| Interface | I + PascalCase | `IUserService` |
| Model | PascalCase | `ApplicationUser` |
| DTO | PascalCase + Dto/Request/Response suffix | `UserDto`, `CreateUserRequest` |
| Constant | PascalCase static class | `Roles`, `Permissions` |

### Method Naming

| Type | Convention | Example |
|------|------------|---------|
| Async methods | PascalCase + Async suffix | `GetUserAsync` |
| GET endpoint | `Get`, `GetAll`, `GetById` | `GetAll()`, `GetById(id)` |
| POST endpoint | `Create`, `Add` | `Create(request)` |
| PUT endpoint | `Update`, `Modify` | `Update(id, request)` |
| DELETE endpoint | `Delete`, `Remove` | `Delete(id)` |

### Variable Naming

```csharp
// Private fields: underscore prefix + camelCase
private readonly IUserService _userService;
private readonly ILogger<UserController> _logger;

// Local variables: camelCase
var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
var existingUser = await _userService.GetByIdAsync(id);

// Constants: PascalCase
public const string DefaultRole = "User";
public const int MaxPageSize = 100;
```

---

## Code Quality Checklist

### Before Committing Code

- [ ] **No compilation errors or warnings**
- [ ] **No TODO, FIXME, or placeholder comments**
- [ ] **No NotImplementedException**
- [ ] **No mock/stub implementations**
- [ ] **All async methods use await properly**
- [ ] **All IDisposable objects are disposed**
- [ ] **All database queries use async methods**
- [ ] **Error handling uses Result<T> pattern**
- [ ] **Logging includes structured context**
- [ ] **Metrics are recorded for operations**
- [ ] **Audit logging for sensitive operations**
- [ ] **Input validation on all endpoints**
- [ ] **Authorization attributes on controllers**
- [ ] **XML documentation on public APIs**
- [ ] **Uses shared libraries (no duplication)**

### Security Checklist

- [ ] **JWT validation configured**
- [ ] **Authorization policies applied**
- [ ] **Input sanitization**
- [ ] **No SQL injection vulnerabilities**
- [ ] **No sensitive data in logs**
- [ ] **CORS properly configured**
- [ ] **Security headers middleware applied**
- [ ] **Rate limiting configured**
- [ ] **Secrets from environment variables**

### Performance Checklist

- [ ] **AsNoTracking for read-only queries**
- [ ] **Proper indexing configured**
- [ ] **Pagination for list endpoints**
- [ ] **HTTP client resilience (Polly)**
- [ ] **Database connection pooling**
- [ ] **Appropriate caching strategy**

---

## Go Services Guidelines

### When to Use Go

Use Go 1.21+ for:
- ✅ Core infrastructure services (NCS, NRM, NMO, NSL)
- ✅ High-concurrency services
- ✅ Low-latency requirements (<1ms)
- ✅ Resource-constrained environments
- ✅ Coordination and consensus protocols

### Project Structure

```
ncs/  (or nrm/, nmo/, nsl/)
├── cmd/
│   └── server/
│       └── main.go              # Entry point
├── internal/
│   ├── service/                 # Core business logic
│   ├── raft/                    # Consensus implementation
│   ├── registry/                # Service registry
│   └── grpc/                    # gRPC server
├── pkg/
│   ├── client/                  # Go client library
│   └── proto/                   # Protocol Buffers
├── api/
│   └── v1/
│       └── service.proto        # gRPC definitions
├── dotnet/
│   └── NCS.Client/              # .NET client library
├── go.mod
├── go.sum
├── Makefile
└── README.md
```

### Code Standards

```go
// Use meaningful package names
package registry

// All exported functions must have doc comments
// RegisterService adds a new service to the registry
func (r *ServiceRegistry) RegisterService(ctx context.Context, node *ServiceNode) error {
    r.mu.Lock()
    defer r.mu.Unlock()

    // Validate input
    if node == nil || node.ID == "" {
        return ErrInvalidNode
    }

    // Register with Raft for consensus
    cmd := &RaftCommand{
        Type: CommandRegisterService,
        Data: node,
    }

    if err := r.raft.Apply(ctx, cmd); err != nil {
        return fmt.Errorf("raft apply failed: %w", err)
    }

    r.services[node.ID] = node
    return nil
}

// Use context for cancellation and timeouts
func (s *Server) Start(ctx context.Context) error {
    lis, err := net.Listen("tcp", s.addr)
    if err != nil {
        return fmt.Errorf("failed to listen: %w", err)
    }

    go func() {
        <-ctx.Done()
        s.grpcServer.GracefulStop()
    }()

    return s.grpcServer.Serve(lis)
}
```

### Error Handling

```go
// Always wrap errors with context
return fmt.Errorf("failed to register service %s: %w", node.ID, err)

// Use sentinel errors for known conditions
var (
    ErrServiceNotFound = errors.New("service not found")
    ErrAlreadyRegistered = errors.New("service already registered")
)

// Return errors, don't panic (except in init functions or unrecoverable state)
if err != nil {
    log.Error().Err(err).Str("service_id", id).Msg("failed to discover service")
    return nil, err
}
```

### Logging

```go
import "github.com/rs/zerolog/log"

// Structured logging with zerolog
log.Info().
    Str("service_id", node.ID).
    Str("address", node.Address).
    Msg("service registered successfully")

log.Error().
    Err(err).
    Str("service_id", node.ID).
    Msg("failed to register service")

// Use levels appropriately
log.Debug().Msg("detailed debug info")
log.Info().Msg("normal operation")
log.Warn().Msg("something unusual but handled")
log.Error().Msg("operation failed")
```

### Testing

```go
package registry_test

import (
    "context"
    "testing"
    "time"

    "github.com/stretchr/testify/assert"
    "github.com/stretchr/testify/require"
)

func TestServiceRegistry_Register(t *testing.T) {
    // Arrange
    registry := NewServiceRegistry(nil)
    node := &ServiceNode{
        ID:      "test-service-1",
        Type:    "compute",
        Address: "localhost:9000",
    }

    // Act
    ctx, cancel := context.WithTimeout(context.Background(), 5*time.Second)
    defer cancel()

    err := registry.RegisterService(ctx, node)

    // Assert
    require.NoError(t, err)

    retrieved, err := registry.GetService(ctx, node.ID)
    require.NoError(t, err)
    assert.Equal(t, node.ID, retrieved.ID)
}
```

---

## Rust Services Guidelines

### When to Use Rust

Use Rust for:
- ✅ Storage engine (columnar format, compression)
- ✅ Custom stream processors
- ✅ Performance-critical components requiring memory safety
- ✅ SIMD-optimized operations
- ✅ Zero-copy data processing

### Project Structure

```
gbmm-storage-engine/
├── src/
│   ├── main.rs                  # gRPC server entry point
│   ├── lib.rs                   # Library exports
│   ├── storage/
│   │   ├── mod.rs
│   │   ├── columnar.rs          # Columnar format
│   │   ├── compression.rs       # LZ4/ZSTD compression
│   │   └── index.rs             # Bloom filters, bitmaps
│   ├── grpc/
│   │   ├── mod.rs
│   │   └── service.rs           # gRPC service implementation
│   └── error.rs                 # Error types
├── proto/
│   └── storage.proto            # gRPC definitions
├── dotnet/
│   └── StorageEngine.Client/    # .NET client library
├── benches/                     # Benchmarks
├── tests/                       # Integration tests
├── Cargo.toml
└── README.md
```

### Code Standards

```rust
use anyhow::{Context, Result};
use serde::{Deserialize, Serialize};

/// ColumnBatch represents a batch of columnar data
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct ColumnBatch {
    pub name: String,
    pub data_type: DataType,
    pub values: Vec<u8>,
    pub null_bitmap: Option<Vec<u8>>,
}

impl ColumnBatch {
    /// Creates a new column batch with the given parameters
    pub fn new(name: String, data_type: DataType) -> Self {
        Self {
            name,
            data_type,
            values: Vec::new(),
            null_bitmap: None,
        }
    }

    /// Compresses the column data using the specified algorithm
    pub fn compress(&mut self, algorithm: CompressionAlgorithm) -> Result<()> {
        let compressed = match algorithm {
            CompressionAlgorithm::Lz4 => {
                lz4::block::compress(&self.values, None, false)
                    .context("LZ4 compression failed")?
            }
            CompressionAlgorithm::Zstd => {
                zstd::encode_all(&self.values[..], 3)
                    .context("ZSTD compression failed")?
            }
        };

        self.values = compressed;
        Ok(())
    }
}

// Use builder pattern for complex constructors
pub struct ColumnBatchBuilder {
    name: String,
    data_type: DataType,
    values: Vec<u8>,
}

impl ColumnBatchBuilder {
    pub fn new(name: impl Into<String>) -> Self {
        Self {
            name: name.into(),
            data_type: DataType::Unknown,
            values: Vec::new(),
        }
    }

    pub fn data_type(mut self, data_type: DataType) -> Self {
        self.data_type = data_type;
        self
    }

    pub fn build(self) -> ColumnBatch {
        ColumnBatch {
            name: self.name,
            data_type: self.data_type,
            values: self.values,
            null_bitmap: None,
        }
    }
}
```

### Error Handling

```rust
use thiserror::Error;

#[derive(Error, Debug)]
pub enum StorageError {
    #[error("column not found: {0}")]
    ColumnNotFound(String),

    #[error("compression failed: {0}")]
    CompressionFailed(#[from] std::io::Error),

    #[error("invalid data type: expected {expected}, got {actual}")]
    InvalidDataType {
        expected: String,
        actual: String,
    },
}

// Use Result<T, E> for operations that can fail
pub fn read_column(name: &str) -> Result<ColumnBatch, StorageError> {
    // Implementation
    Ok(ColumnBatch::new(name.to_string(), DataType::Int64))
}
```

### Testing

```rust
#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_column_batch_compression() {
        let mut batch = ColumnBatch::new("test_col".to_string(), DataType::Int64);
        batch.values = vec![1, 2, 3, 4, 5];

        let result = batch.compress(CompressionAlgorithm::Lz4);
        assert!(result.is_ok());
        assert!(batch.values.len() > 0);
    }

    #[tokio::test]
    async fn test_async_operations() {
        // Async test example
    }
}
```

---

## Scala/Java Services Guidelines

### When to Use Scala/Java

Use Scala 3 / Java 17 for:
- ✅ Query engine and SQL optimization
- ✅ Apache Calcite integration
- ✅ Complex query planning and federation
- ✅ JVM ecosystem integration

### Project Structure (SBT)

```
gbmm-query-engine/
├── src/
│   ├── main/
│   │   ├── scala/
│   │   │   └── com/gbmm/query/
│   │   │       ├── QueryEngine.scala
│   │   │       ├── optimizer/
│   │   │       ├── planner/
│   │   │       └── executor/
│   │   ├── java/
│   │   │   └── com/gbmm/query/grpc/
│   │   └── resources/
│   │       └── application.conf
│   └── test/
│       └── scala/
│           └── com/gbmm/query/
├── dotnet/
│   └── QueryEngine.Client/      # .NET client library
├── build.sbt
├── project/
│   └── plugins.sbt
└── README.md
```

### Code Standards (Scala 3)

```scala
package com.gbmm.query

import cats.effect.IO
import cats.syntax.all._
import org.apache.calcite.plan.RelOptPlanner
import io.grpc.ServerBuilder

/** Query engine for SQL optimization and execution */
class QueryEngine(planner: RelOptPlanner, storage: StorageClient):

  /** Executes a SQL query and returns results */
  def executeQuery(sql: String): IO[QueryResult] =
    for
      parsed <- IO(parseSql(sql))
      optimized <- IO(optimize(parsed))
      result <- execute(optimized)
    yield result

  /** Optimizes a query plan using Apache Calcite */
  private def optimize(plan: RelNode): RelNode =
    planner.setRoot(plan)
    planner.findBestExp()

  /** Executes an optimized query plan */
  private def execute(plan: RelNode): IO[QueryResult] =
    plan match
      case scan: TableScan => executeScan(scan)
      case join: Join => executeJoin(join)
      case _ => IO.raiseError(new UnsupportedOperationException)

// Use case classes for data transfer
case class QueryResult(
  columns: List[String],
  rows: List[List[Any]],
  executionTime: Long
)

// Use sealed traits for ADTs
sealed trait QueryPlan
case class ScanPlan(table: String, columns: List[String]) extends QueryPlan
case class JoinPlan(left: QueryPlan, right: QueryPlan) extends QueryPlan
case class FilterPlan(plan: QueryPlan, predicate: String) extends QueryPlan
```

### Error Handling

```scala
import cats.effect.IO
import scala.util.{Try, Success, Failure}

// Use IO for effects and error handling
def fetchData(id: String): IO[Data] =
  IO.fromTry(Try {
    // Implementation
    Data(id, "value")
  }).handleErrorWith { error =>
    IO.raiseError(new DataFetchException(s"Failed to fetch data for $id", error))
  }

// Custom exception hierarchy
class QueryEngineException(message: String, cause: Throwable = null)
  extends Exception(message, cause)

class QueryParseException(message: String, cause: Throwable = null)
  extends QueryEngineException(message, cause)

class QueryExecutionException(message: String, cause: Throwable = null)
  extends QueryEngineException(message, cause)
```

### Testing (ScalaTest)

```scala
import org.scalatest.funsuite.AnyFunSuite
import org.scalatest.matchers.should.Matchers

class QueryEngineSpec extends AnyFunSuite with Matchers:

  test("should parse simple SELECT query") {
    val sql = "SELECT * FROM users WHERE age > 18"
    val engine = new QueryEngine(mockPlanner, mockStorage)

    val result = engine.executeQuery(sql).unsafeRunSync()

    result.columns should contain ("id")
    result.rows should not be empty
  }

  test("should optimize JOIN queries") {
    val sql = "SELECT * FROM users u JOIN orders o ON u.id = o.user_id"
    // Test implementation
  }
```

---

## Python ML Services Guidelines

### When to Use Python

Use Python 3.11+ for:
- ✅ ML platform and distributed training (Ray)
- ✅ Data science SDK and notebooks
- ✅ AutoML and model serving
- ✅ NCF (Compute Framework) API layer

### Project Structure

```
gbmm-ml-platform/
├── gbmm_ml/
│   ├── __init__.py
│   ├── training/
│   │   ├── __init__.py
│   │   ├── distributed.py       # Ray Train integration
│   │   └── automl.py            # AutoML capabilities
│   ├── serving/
│   │   ├── __init__.py
│   │   └── model_server.py      # Ray Serve
│   ├── monitoring/
│   │   └── drift_detection.py
│   └── grpc/
│       └── service.py           # gRPC service
├── proto/
│   └── ml_platform.proto
├── dotnet/
│   └── MLPlatform.Client/       # .NET client
├── tests/
│   ├── test_training.py
│   └── test_serving.py
├── setup.py
├── pyproject.toml
└── README.md
```

### Code Standards

```python
from typing import Optional, List, Dict, Any
from dataclasses import dataclass
import ray
from ray import train
from ray.train import ScalingConfig
import torch

@dataclass
class TrainingConfig:
    """Configuration for distributed training"""
    model_type: str
    batch_size: int
    learning_rate: float
    num_epochs: int
    num_workers: int = 4
    use_gpu: bool = True

class DistributedTrainer:
    """Distributed model training using Ray Train"""

    def __init__(self, config: TrainingConfig):
        self.config = config
        self.ray_initialized = False

    def train(self, dataset: ray.data.Dataset) -> Dict[str, Any]:
        """
        Train a model using distributed Ray Train

        Args:
            dataset: Ray dataset for training

        Returns:
            Dictionary containing training metrics

        Raises:
            TrainingError: If training fails
        """
        if not self.ray_initialized:
            ray.init(ignore_reinit_error=True)
            self.ray_initialized = True

        scaling_config = ScalingConfig(
            num_workers=self.config.num_workers,
            use_gpu=self.config.use_gpu
        )

        trainer = train.TorchTrainer(
            train_loop_per_worker=self._train_loop,
            scaling_config=scaling_config,
            datasets={"train": dataset}
        )

        result = trainer.fit()
        return result.metrics

    def _train_loop(self, config: Dict[str, Any]) -> None:
        """Training loop executed on each worker"""
        model = self._create_model()
        optimizer = torch.optim.Adam(model.parameters(), lr=self.config.learning_rate)

        for epoch in range(self.config.num_epochs):
            # Training logic
            loss = self._train_epoch(model, optimizer)
            train.report({"loss": loss, "epoch": epoch})
```

### Error Handling

```python
class MLPlatformError(Exception):
    """Base exception for ML Platform"""
    pass

class TrainingError(MLPlatformError):
    """Raised when training fails"""
    pass

class ModelNotFoundError(MLPlatformError):
    """Raised when model cannot be found"""
    pass

# Use try-except with specific exceptions
def load_model(model_id: str) -> torch.nn.Module:
    try:
        model = torch.load(f"models/{model_id}.pt")
        return model
    except FileNotFoundError:
        raise ModelNotFoundError(f"Model {model_id} not found")
    except Exception as e:
        raise MLPlatformError(f"Failed to load model: {e}")
```

### Testing (pytest)

```python
import pytest
import ray
from gbmm_ml.training import DistributedTrainer, TrainingConfig

@pytest.fixture
def ray_context():
    """Initialize Ray for testing"""
    ray.init(num_cpus=2, num_gpus=0)
    yield
    ray.shutdown()

def test_distributed_training(ray_context):
    """Test distributed training pipeline"""
    config = TrainingConfig(
        model_type="linear",
        batch_size=32,
        learning_rate=0.001,
        num_epochs=10,
        num_workers=2,
        use_gpu=False
    )

    # Create synthetic dataset
    dataset = ray.data.range(1000)

    trainer = DistributedTrainer(config)
    metrics = trainer.train(dataset)

    assert "loss" in metrics
    assert metrics["loss"] < 1.0  # Expect some learning

@pytest.mark.asyncio
async def test_model_serving():
    """Test model serving with Ray Serve"""
    # Test implementation
    pass
```

---

## Inter-Service Communication

### gRPC Protocol Buffers

All services MUST use Protocol Buffers for service definitions:

```protobuf
// storage.proto
syntax = "proto3";

package gbmm.storage.v1;

import "google/protobuf/timestamp.proto";

service StorageService {
  rpc IngestData(IngestRequest) returns (IngestResponse);
  rpc QueryData(QueryRequest) returns (stream QueryResponse);
  rpc GetMetadata(MetadataRequest) returns (MetadataResponse);
}

message IngestRequest {
  string database = 1;
  string table = 2;
  bytes data = 3;
  CompressionType compression = 4;
}

message IngestResponse {
  string batch_id = 1;
  int64 rows_ingested = 2;
  int64 bytes_written = 3;
  google.protobuf.Timestamp timestamp = 4;
}

enum CompressionType {
  COMPRESSION_NONE = 0;
  COMPRESSION_LZ4 = 1;
  COMPRESSION_ZSTD = 2;
}
```

### .NET Client Libraries

Every non-.NET service MUST provide a .NET client library:

```csharp
// StorageEngine.Client/StorageEngineClient.cs
using Grpc.Net.Client;
using Gbmm.Storage.V1;

namespace GBMM.StorageEngine.Client;

public class StorageEngineClient : IDisposable
{
    private readonly GrpcChannel _channel;
    private readonly StorageService.StorageServiceClient _client;
    private readonly ILogger<StorageEngineClient> _logger;

    public StorageEngineClient(string address, ILogger<StorageEngineClient> logger)
    {
        _channel = GrpcChannel.ForAddress(address);
        _client = new StorageService.StorageServiceClient(_channel);
        _logger = logger;
    }

    public async Task<IngestResponse> IngestDataAsync(
        string database,
        string table,
        byte[] data,
        CompressionType compression = CompressionType.Lz4)
    {
        try
        {
            var request = new IngestRequest
            {
                Database = database,
                Table = table,
                Data = Google.Protobuf.ByteString.CopyFrom(data),
                Compression = compression
            };

            var response = await _client.IngestDataAsync(request);

            _logger.LogInformation(
                "Ingested {Rows} rows to {Database}.{Table}, batch {BatchId}",
                response.RowsIngested,
                database,
                table,
                response.BatchId);

            return response;
        }
        catch (Grpc.Core.RpcException ex)
        {
            _logger.LogError(ex, "gRPC call failed: {Status}", ex.Status);
            throw;
        }
    }

    public void Dispose()
    {
        _channel.Dispose();
    }
}
```

---

## Shared Observability

### OpenTelemetry Integration

ALL services MUST implement OpenTelemetry tracing:

**Go:**
```go
import (
    "go.opentelemetry.io/otel"
    "go.opentelemetry.io/otel/trace"
)

func (s *Service) ProcessRequest(ctx context.Context, req *Request) (*Response, error) {
    tracer := otel.Tracer("ncs-service")
    ctx, span := tracer.Start(ctx, "ProcessRequest")
    defer span.End()

    span.SetAttributes(
        attribute.String("request.id", req.ID),
        attribute.Int("request.size", len(req.Data)),
    )

    // Implementation

    return response, nil
}
```

**Rust:**
```rust
use opentelemetry::trace::{Tracer, TracerProvider};

pub async fn process_batch(batch: &ColumnBatch) -> Result<()> {
    let tracer = global::tracer("storage-engine");
    let span = tracer.start("process_batch");
    let _guard = span.enter();

    // Implementation

    Ok(())
}
```

**Python:**
```python
from opentelemetry import trace
from opentelemetry.trace import Status, StatusCode

tracer = trace.get_tracer("ml-platform")

def train_model(config: TrainingConfig) -> ModelMetrics:
    with tracer.start_as_current_span("train_model") as span:
        span.set_attribute("model.type", config.model_type)
        span.set_attribute("num.workers", config.num_workers)

        try:
            # Implementation
            metrics = distributed_train()
            span.set_status(Status(StatusCode.OK))
            return metrics
        except Exception as e:
            span.set_status(Status(StatusCode.ERROR, str(e)))
            raise
```

---

## Summary

This document establishes coding standards for the **GBMM Hybrid Polyglot Platform**. Key principles:

### Universal Requirements (All Languages)

1. **Production-Ready Code Only** - No placeholders, TODOs, or mock implementations
2. **gRPC for Inter-Service Communication** - Protocol Buffers for all service definitions
3. **.NET Client Libraries** - Every service must provide .NET integration
4. **OpenTelemetry Tracing** - Distributed tracing in all services
5. **Structured Logging** - Context-rich logging with correlation IDs
6. **Comprehensive Testing** - Unit, integration, and performance tests

### Language-Specific Guidelines

| Language | Use Cases | Key Requirements |
|----------|-----------|------------------|
| **.NET 8** | Core services, APIs, orchestration | GBMM.Common, GBMM.Security, GBMM.Observability libraries |
| **Go 1.21+** | Infrastructure (NCS, NRM, NMO, NSL) | zerolog, hashicorp/raft, gRPC, context propagation |
| **Rust** | Storage engine, stream processors | anyhow/thiserror errors, tokio async, SIMD optimization |
| **Scala 3/Java 17** | Query engine, SQL optimization | Apache Calcite, cats-effect, ScalaTest |
| **Python 3.11+** | ML platform, compute framework | Ray, PyTorch, type hints, pytest |

### Reference Implementations

- **.NET Services:** `/home/tshepo/projects/GBMM/gbmm/auth-service/`
- **Complete Roadmap:** `/home/tshepo/projects/GBMM/docs/planning/100_PERCENT_ROADMAP.md`
- **Development Workflow:** `/home/tshepo/projects/GBMM/docs/development/DEVELOPMENT_WORKFLOW.md`

### Getting Help

- For .NET questions: Review auth-service implementation
- For Go/Rust/Scala/Python questions: Review 100% Roadmap implementation examples
- For architecture questions: Consult ARCHITECTURE_EVOLUTION.md

---

**Document Version:** 2.0.0
**Last Updated:** December 11, 2025
**Applies To:** Phases 1-5 of 100% Roadmap (30-month timeline)
