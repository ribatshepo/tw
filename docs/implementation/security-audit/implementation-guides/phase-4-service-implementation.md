# Phase 4: Service Implementation - Weeks 4-12 Implementation Guide

**Phase:** 4 of 6
**Duration:** Weeks 4-12 (9 weeks)
**Focus:** Core Service Feature Development
**Team:** Full Engineering Team (Backend + ML + Data + DevOps)
**Deliverable:** All 5 services fully implemented per specifications

---

## Overview

Phase 4 is the main implementation phase where all core service features are built according to the comprehensive specifications in `docs/specs/`. This phase takes 9 weeks with parallel workstreams across all 5 services.

**Dependencies:** Phases 1-3 must be complete (all security, observability, and documentation foundations in place).

**Note:** This guide provides high-level roadmap. Detailed implementation follows specifications in:
- `docs/specs/unified-compute-coordination-platform.md` (UCCP + NCCS)
- `docs/specs/data-platform.md` (UDPS)
- `docs/specs/security.md` (USP)
- `docs/specs/streaming.md` (Stream Compute)

---

## Implementation Strategy

### Parallel Development Tracks

```
Week 4-12: All services developed in parallel

┌─────────────┬─────────────┬─────────────┬─────────────┬─────────────┐
│    UCCP     │    NCCS     │    UDPS     │     USP     │   Stream    │
│   (Go/Rust) │   (.NET 8)  │ (Scala/Java)│   (.NET 8)  │   (Rust)    │
├─────────────┼─────────────┼─────────────┼─────────────┼─────────────┤
│ Week 4-5    │ Week 4-5    │ Week 4-5    │ Week 4-5    │ Week 4-5    │
│ Raft        │ REST API    │ Columnar    │ Auth        │ SIMD        │
│ Consensus   │ Gateway     │ Storage     │ Complete    │ Engine      │
│             │             │             │             │             │
│ Week 6-7    │ Week 6-7    │ Week 6-7    │ Week 6-7    │ Week 6-7    │
│ Service     │ SignalR     │ SQL Query   │ Secrets     │ Flink       │
│ Discovery   │ Real-time   │ Engine      │ Rotation    │ Integration │
│             │             │             │             │             │
│ Week 8-9    │ Week 8-9    │ Week 8-9    │ Week 8-9    │ Week 8-9    │
│ Task        │ NuGet SDK   │ Data        │ PAM &       │ CEP         │
│ Scheduling  │ Package     │ Lineage     │ Audit       │ Processing  │
│             │             │             │             │             │
│ Week 10-11  │ Week 10-11  │ Week 10-11  │ Week 10-11  │ Week 10-11  │
│ ML Ops      │ Polly       │ ACID        │ Compliance  │ Stateful    │
│ Training    │ Resilience  │ Transactions│ Evidence    │ Joins       │
│             │             │             │             │             │
│ Week 12     │ Week 12     │ Week 12     │ Week 12     │ Week 12     │
│ Integration │ Integration │ Integration │ Integration │ Integration │
└─────────────┴─────────────┴─────────────┴─────────────┴─────────────┘
```

### Team Allocation

- **UCCP Team** (3 engineers): Go + Rust + Python
- **NCCS Team** (2 engineers): C# + .NET
- **UDPS Team** (3 engineers): Scala + Java
- **USP Team** (2 engineers): C# + .NET + Security
- **Stream Team** (2 engineers): Rust + Scala (Flink)
- **DevOps** (2 engineers): Supporting all teams
- **QA** (2 engineers): Writing integration tests

**Total:** 16 engineers

---

## Weeks 4-5: Core Foundations (2 weeks)

### Objective
Implement foundational infrastructure for each service.

---

### UCCP: Raft Consensus & Cluster Management

**Specification:** `docs/specs/unified-compute-coordination-platform.md` (lines 100-250)

#### Week 4 Tasks

**1. Implement Raft Consensus (5 days)**

```go
// internal/raft/node.go
package raft

import (
    "github.com/hashicorp/raft"
    "github.com/hashicorp/raft-boltdb"
)

type RaftNode struct {
    raft      *raft.Raft
    fsm       *FSM
    transport *raft.NetworkTransport
    config    *raft.Config
}

func NewRaftNode(cfg *Config) (*RaftNode, error) {
    // Raft configuration
    raftConfig := raft.DefaultConfig()
    raftConfig.LocalID = raft.ServerID(cfg.NodeID)
    raftConfig.HeartbeatTimeout = 1000 * time.Millisecond
    raftConfig.ElectionTimeout = 1000 * time.Millisecond
    raftConfig.LeaderLeaseTimeout = 500 * time.Millisecond

    // FSM (Finite State Machine)
    fsm := NewFSM()

    // Log store
    logStore, err := raftboltdb.NewBoltStore(cfg.DataDir + "/raft-log.db")
    if err != nil {
        return nil, err
    }

    // Stable store
    stableStore, err := raftboltdb.NewBoltStore(cfg.DataDir + "/raft-stable.db")
    if err != nil {
        return nil, err
    }

    // Snapshot store
    snapshotStore, err := raft.NewFileSnapshotStore(cfg.DataDir, 3, os.Stderr)
    if err != nil {
        return nil, err
    }

    // TCP transport
    addr, err := net.ResolveTCPAddr("tcp", cfg.RaftAddr)
    if err != nil {
        return nil, err
    }

    transport, err := raft.NewTCPTransport(addr.String(), addr, 3, 10*time.Second, os.Stderr)
    if err != nil {
        return nil, err
    }

    // Create Raft instance
    r, err := raft.NewRaft(raftConfig, fsm, logStore, stableStore, snapshotStore, transport)
    if err != nil {
        return nil, err
    }

    return &RaftNode{
        raft:      r,
        fsm:       fsm,
        transport: transport,
        config:    raftConfig,
    }, nil
}

func (rn *RaftNode) Bootstrap(peers []string) error {
    var servers []raft.Server
    for _, peer := range peers {
        servers = append(servers, raft.Server{
            ID:      raft.ServerID(peer),
            Address: raft.ServerAddress(peer),
        })
    }

    configuration := raft.Configuration{Servers: servers}
    return rn.raft.BootstrapCluster(configuration).Error()
}

func (rn *RaftNode) Apply(cmd []byte, timeout time.Duration) error {
    future := rn.raft.Apply(cmd, timeout)
    return future.Error()
}

func (rn *RaftNode) IsLeader() bool {
    return rn.raft.State() == raft.Leader
}
```

**2. Implement Finite State Machine (2 days)**

```go
// internal/raft/fsm.go
type FSM struct {
    mu       sync.RWMutex
    services map[string]*ServiceRegistration
    tasks    map[string]*Task
}

func (fsm *FSM) Apply(log *raft.Log) interface{} {
    fsm.mu.Lock()
    defer fsm.mu.Unlock()

    var cmd Command
    if err := json.Unmarshal(log.Data, &cmd); err != nil {
        return err
    }

    switch cmd.Type {
    case "register_service":
        var svc ServiceRegistration
        json.Unmarshal(cmd.Data, &svc)
        fsm.services[svc.ServiceID] = &svc
        return nil

    case "deregister_service":
        var serviceID string
        json.Unmarshal(cmd.Data, &serviceID)
        delete(fsm.services, serviceID)
        return nil

    case "submit_task":
        var task Task
        json.Unmarshal(cmd.Data, &task)
        fsm.tasks[task.TaskID] = &task
        return nil

    default:
        return fmt.Errorf("unknown command type: %s", cmd.Type)
    }
}

func (fsm *FSM) Snapshot() (raft.FSMSnapshot, error) {
    fsm.mu.RLock()
    defer fsm.mu.RUnlock()

    return &FSMSnapshot{
        services: copyMap(fsm.services),
        tasks:    copyMap(fsm.tasks),
    }, nil
}

func (fsm *FSM) Restore(snapshot io.ReadCloser) error {
    var data FSMSnapshot
    if err := json.NewDecoder(snapshot).Decode(&data); err != nil {
        return err
    }

    fsm.mu.Lock()
    fsm.services = data.services
    fsm.tasks = data.tasks
    fsm.mu.Unlock()

    return nil
}
```

**3. Write Raft Tests (1 day)**

```go
// internal/raft/raft_test.go
func TestRaftConsensus(t *testing.T) {
    // Create 3-node cluster
    nodes := make([]*RaftNode, 3)
    for i := 0; i < 3; i++ {
        node, err := NewRaftNode(&Config{
            NodeID:   fmt.Sprintf("node%d", i),
            RaftAddr: fmt.Sprintf("localhost:%d", 50061+i),
            DataDir:  fmt.Sprintf("/tmp/raft-test-%d", i),
        })
        require.NoError(t, err)
        nodes[i] = node
    }

    // Bootstrap cluster
    peers := []string{"localhost:50061", "localhost:50062", "localhost:50063"}
    err := nodes[0].Bootstrap(peers)
    require.NoError(t, err)

    // Wait for leader election
    time.Sleep(3 * time.Second)

    // Find leader
    var leader *RaftNode
    for _, node := range nodes {
        if node.IsLeader() {
            leader = node
            break
        }
    }
    require.NotNil(t, leader)

    // Apply command on leader
    cmd := Command{
        Type: "register_service",
        Data: json.RawMessage(`{"serviceId":"test-service","address":"localhost:8080"}`),
    }
    cmdBytes, _ := json.Marshal(cmd)

    err = leader.Apply(cmdBytes, 5*time.Second)
    require.NoError(t, err)

    // Verify replicated to all nodes
    time.Sleep(1 * time.Second)
    for _, node := range nodes {
        services := node.fsm.GetServices()
        assert.Contains(t, services, "test-service")
    }
}
```

#### Week 5 Tasks

**Service Discovery Implementation**

- Service registry with health checking
- gRPC service for registration/deregistration
- Gossip protocol for failure detection
- DNS-based service discovery

**Tests:** Unit + integration tests for service discovery

---

### NCCS: REST API Gateway

**Specification:** `docs/specs/unified-compute-coordination-platform.md` (lines 1100-1300)

#### Week 4-5 Tasks

**1. ASP.NET Core API Gateway (5 days)**

```csharp
// Controllers/TasksController.cs
[ApiController]
[Route("api/v1/tasks")]
public class TasksController : ControllerBase
{
    private readonly IUCCPClient _uccpClient;
    private readonly ILogger<TasksController> _logger;

    [HttpPost]
    [RequirePermission("tasks", "submit")]
    public async Task<IActionResult> SubmitTask([FromBody] SubmitTaskRequest request)
    {
        // Forward to UCCP via gRPC
        var grpcRequest = new SubmitTaskGrpcRequest
        {
            TaskType = request.TaskType,
            ResourceRequirements = request.Resources,
            Data = request.Data
        };

        var response = await _uccpClient.SubmitTaskAsync(grpcRequest);

        return CreatedAtAction(
            nameof(GetTask),
            new { id = response.TaskId },
            new { taskId = response.TaskId }
        );
    }

    [HttpGet("{id}")]
    [RequirePermission("tasks", "read")]
    public async Task<IActionResult> GetTask(string id)
    {
        var task = await _uccpClient.GetTaskAsync(new GetTaskRequest { TaskId = id });

        return Ok(new
        {
            taskId = task.TaskId,
            status = task.Status,
            result = task.Result,
            createdAt = task.CreatedAt,
            completedAt = task.CompletedAt
        });
    }
}
```

**2. gRPC Client to UCCP (3 days)**

```csharp
// Services/UCCPClient.cs
public class UCCPClient : IUCCPClient
{
    private readonly TaskService.TaskServiceClient _client;

    public UCCPClient(IConfiguration config)
    {
        var channel = GrpcChannel.ForAddress(config["UCCP:Address"], new GrpcChannelOptions
        {
            Credentials = ChannelCredentials.SecureSsl,
            HttpHandler = new SocketsHttpHandler
            {
                PooledConnectionIdleTimeout = Timeout.InfiniteTimeSpan,
                KeepAlivePingDelay = TimeSpan.FromSeconds(60),
                KeepAlivePingTimeout = TimeSpan.FromSeconds(30),
                EnableMultipleHttp2Connections = true
            }
        });

        _client = new TaskService.TaskServiceClient(channel);
    }

    public async Task<SubmitTaskResponse> SubmitTaskAsync(SubmitTaskGrpcRequest request)
    {
        var response = await _client.SubmitTaskAsync(request, deadline: DateTime.UtcNow.AddSeconds(30));
        return response;
    }
}
```

**3. OpenAPI/Swagger Configuration (2 days)**

---

### UDPS: Columnar Storage Engine

**Specification:** `docs/specs/data-platform.md` (lines 50-300)

#### Week 4-5 Tasks

**1. Parquet Storage Implementation (5 days)**

```scala
// storage/ColumnarStorage.scala
class ColumnarStorage(config: StorageConfig) {

  def write(data: Dataset[Row], path: String): Unit = {
    data.write
      .mode(SaveMode.Append)
      .option("compression", "snappy")
      .option("parquet.block.size", 128 * 1024 * 1024) // 128MB
      .parquet(path)
  }

  def read(path: String): Dataset[Row] = {
    spark.read
      .option("mergeSchema", "true")
      .parquet(path)
  }

  def readWithPredicate(path: String, predicate: String): Dataset[Row] = {
    spark.read
      .parquet(path)
      .where(predicate) // Pushdown predicate
  }
}
```

**2. Compression Codec Support (3 days)**

- Snappy (default, balanced)
- Gzip (high compression)
- LZ4 (fast compression)
- Zstandard (best compression ratio)

**3. Storage Tests (2 days)**

---

### USP: Complete Authentication System

**Specification:** `docs/specs/security.md` (lines 251-525)

#### Week 4-5 Tasks

**1. Multi-Factor Authentication (MFA) (5 days)**

```csharp
// Services/MfaService.cs
public class MfaService : IMfaService
{
    private readonly ITotpService _totpService;
    private readonly IEmailService _emailService;
    private readonly IWebAuthnService _webAuthnService;

    public async Task<MfaSetupResponse> SetupTotpAsync(string userId)
    {
        // Generate TOTP secret
        var secret = _totpService.GenerateSecret();
        var qrCode = _totpService.GenerateQrCode(secret, userId);

        // Store secret encrypted
        await _userRepository.UpdateMfaSecretAsync(userId, secret);

        return new MfaSetupResponse
        {
            Secret = secret,
            QrCode = qrCode,
            BackupCodes = GenerateBackupCodes()
        };
    }

    public async Task<bool> VerifyTotpAsync(string userId, string code)
    {
        var secret = await _userRepository.GetMfaSecretAsync(userId);
        return _totpService.ValidateCode(secret, code);
    }

    public async Task<string> SendEmailCodeAsync(string userId)
    {
        var code = GenerateEmailCode();
        var user = await _userRepository.GetByIdAsync(userId);

        await _emailService.SendAsync(user.Email, "MFA Code", $"Your code: {code}");
        await _cacheService.SetAsync($"mfa:email:{userId}", code, TimeSpan.FromMinutes(5));

        return code;
    }

    public async Task<bool> VerifyEmailCodeAsync(string userId, string code)
    {
        var stored = await _cacheService.GetAsync<string>($"mfa:email:{userId}");
        return stored == code;
    }
}
```

**2. WebAuthn/FIDO2 Support (3 days)**

**3. Session Management (2 days)**

---

### Stream Compute: SIMD Engine

**Specification:** `docs/specs/streaming.md` (lines 50-200)

#### Week 4-5 Tasks

**1. SIMD-Accelerated Processing (5 days)**

```rust
// src/simd/processor.rs
use std::simd::{f64x4, SimdFloat};

pub struct SIMDProcessor {
    chunk_size: usize,
}

impl SIMDProcessor {
    pub fn aggregate_sum(&self, data: &[f64]) -> f64 {
        let mut sum = 0.0f64;
        let chunks = data.chunks_exact(4);

        // SIMD vectorized loop
        let remainder = chunks.remainder();
        for chunk in chunks {
            let vec = f64x4::from_slice(chunk);
            sum += vec.reduce_sum();
        }

        // Handle remainder
        sum += remainder.iter().sum::<f64>();
        sum
    }

    pub fn transform(&self, data: &[f64]) -> Vec<f64> {
        let mut result = Vec::with_capacity(data.len());

        for chunk in data.chunks_exact(4) {
            let vec = f64x4::from_slice(chunk);
            let transformed = vec * f64x4::splat(2.0) + f64x4::splat(1.0);
            result.extend_from_slice(transformed.as_array());
        }

        result
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_simd_aggregate_sum() {
        let processor = SIMDProcessor { chunk_size: 1024 };
        let data: Vec<f64> = (0..1000).map(|x| x as f64).collect();

        let sum = processor.aggregate_sum(&data);
        assert_eq!(sum, 499500.0); // Sum of 0..999
    }
}
```

**2. Kafka Integration (3 days)**

**3. Latency Benchmarks (<1ms p99) (2 days)**

---

## Weeks 6-7: Advanced Features (2 weeks)

### UCCP: Task Scheduling with GPU Support

- Priority queue scheduler
- Resource allocation (CPU, GPU, TPU, Memory)
- Task dependencies and DAG execution
- Distributed locks for task coordination

### NCCS: SignalR Real-Time Communication

- WebSocket connections
- Real-time task updates
- Server-to-client push notifications
- Reconnection handling

### UDPS: SQL Query Engine (Apache Calcite)

- SQL parser and validator
- Logical plan optimization
- Cost-based optimizer
- Query execution engine

### USP: Credential Rotation

- Automated password rotation
- Database credential rotation
- API key rotation
- Lease-based secrets

### Stream Compute: Apache Flink Integration

- Flink job submission
- Checkpoint management
- Savepoint creation
- State backends (RocksDB)

---

## Weeks 8-9: ML & Data Features (2 weeks)

### UCCP: ML Operations

- TensorFlow model training
- PyTorch integration
- Model serving (TensorFlow Serving)
- Feature store

### NCCS: NuGet SDK Package

- C# client library
- Async/await patterns
- Polly resilience
- NuGet package publishing

### UDPS: Data Lineage Tracking

- Column-level lineage
- Transformation tracking
- Data catalog integration
- Governance policies

### USP: Privileged Access Management (PAM)

- Just-in-time access
- Session recording
- Approval workflows
- Emergency access procedures

### Stream Compute: Complex Event Processing (CEP)

- Pattern matching
- Temporal operators
- Event correlation
- Alerting

---

## Weeks 10-11: Enterprise Features (2 weeks)

### UCCP: AutoML & Hyperparameter Tuning

- Grid search
- Bayesian optimization
- Neural architecture search
- Experiment tracking

### NCCS: Advanced Resilience Patterns

- Circuit breakers (Polly)
- Retry with exponential backoff
- Bulkhead isolation
- Timeout policies

### UDPS: ACID Transactions

- MVCC (Multi-Version Concurrency Control)
- Snapshot isolation
- Transaction coordinator
- 2-phase commit

### USP: Compliance Evidence Collection

- SOC 2 audit logs
- HIPAA audit trails
- PCI-DSS evidence
- GDPR consent tracking

### Stream Compute: Stateful Stream Joins

- Window joins
- Interval joins
- State management
- Exactly-once semantics

---

## Week 12: Integration & Stabilization

### Objective
Integrate all services and resolve cross-service issues.

### Integration Tasks

**1. End-to-End Workflows (3 days)**

Test complete workflows across all services:

```
User Request → NCCS (REST API)
            ↓
        UCCP (Task Scheduling)
            ↓
        UDPS (Data Retrieval)
            ↓
        USP (Secret Access)
            ↓
        Stream Compute (Real-time Processing)
            ↓
        UCCP (Result Aggregation)
            ↓
        NCCS (Response to User)
```

**2. Performance Tuning (2 days)**

- gRPC connection pooling
- Database query optimization
- Cache hit rate optimization
- Resource allocation tuning

**3. Observability Validation (2 days)**

- Verify distributed traces span all services
- Confirm metrics from all endpoints
- Test log aggregation
- Validate alerts

**4. Security Hardening (1 day)**

- Penetration testing
- Vulnerability scanning
- mTLS certificate validation
- Secret rotation verification

**5. Documentation Updates (2 days)**

- API documentation complete
- Architecture diagrams updated
- Deployment guides verified
- Troubleshooting guides expanded

---

## Deliverables (End of Week 12)

### UCCP Deliverables
- [ ] Raft consensus cluster operational (3+ nodes)
- [ ] Service discovery with health checking
- [ ] Task scheduling with GPU/TPU support
- [ ] ML model training (TensorFlow, PyTorch)
- [ ] Feature store implemented
- [ ] AutoML with hyperparameter tuning
- [ ] Distributed locking functional

### NCCS Deliverables
- [ ] REST API gateway with 50+ endpoints
- [ ] SignalR real-time communication
- [ ] NuGet SDK package published
- [ ] Polly resilience patterns configured
- [ ] gRPC client to UCCP functional
- [ ] OpenAPI/Swagger documentation complete

### UDPS Deliverables
- [ ] Columnar storage (Parquet/ORC/Arrow)
- [ ] SQL query engine with optimizer
- [ ] Data lineage tracking
- [ ] ACID transactions with MVCC
- [ ] Time travel queries
- [ ] Real-time streaming ingest from Kafka

### USP Deliverables
- [ ] JWT authentication with MFA (TOTP, Email, WebAuthn)
- [ ] RBAC and ABAC authorization
- [ ] Secrets management (Vault-compatible API)
- [ ] AES-256-GCM encryption
- [ ] Credential rotation automated
- [ ] Privileged Access Management (PAM)
- [ ] Audit logging with tamper-proof storage
- [ ] Compliance evidence collection (SOC 2, HIPAA, PCI-DSS)

### Stream Compute Deliverables
- [ ] SIMD-accelerated processing (<1ms p99 latency)
- [ ] Apache Flink integration
- [ ] Complex Event Processing (CEP)
- [ ] Stateful stream joins
- [ ] Kafka integration with exactly-once semantics
- [ ] Real-time anomaly detection

---

## Testing Requirements

### Unit Tests
- [ ] Code coverage ≥80% for all services
- [ ] All critical paths tested
- [ ] Edge cases covered

### Integration Tests
- [ ] Cross-service communication tested
- [ ] Database transactions validated
- [ ] Cache behavior verified
- [ ] Error handling confirmed

### Performance Tests
- [ ] UCCP: 10,000 tasks/sec throughput
- [ ] NCCS: 50,000 requests/sec
- [ ] UDPS: 1M rows/sec query performance
- [ ] USP: 20,000 auth requests/sec
- [ ] Stream: <1ms p99 latency

### Security Tests
- [ ] Penetration testing passed
- [ ] Vulnerability scan clean
- [ ] mTLS validation working
- [ ] Secret rotation functional

---

## Handoff to Phase 5

**Phase 4 Complete Criteria:**
- All 5 services fully implemented per specifications
- End-to-end integration tested
- Performance benchmarks met
- Security hardening complete
- Documentation updated

**Phase 5 Preview:**
- Comprehensive integration testing (Week 13)
- End-to-end testing (Week 13)
- Load testing and performance tuning (Week 14)
- Chaos engineering (Week 14)

---

**Status:** Ready to Start
**Last Updated:** 2025-12-27
**Phase Owner:** Full Engineering Team
