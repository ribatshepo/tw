using Prometheus;
using USP.Core.Domain.Enums;

namespace USP.Infrastructure.Metrics;

/// <summary>
/// Prometheus metrics for authorization and audit operations.
/// Tracks authorization decisions, policy evaluations, and audit events.
/// </summary>
public static class SecurityMetrics
{
    // Authorization Metrics

    /// <summary>
    /// Total number of authorization checks performed.
    /// Labels: result (allowed, denied, error)
    /// </summary>
    public static readonly Counter AuthorizationChecksTotal = Prometheus.Metrics.CreateCounter(
        "usp_authz_checks_total",
        "Total number of authorization checks",
        new CounterConfiguration
        {
            LabelNames = new[] { "result" }
        });

    /// <summary>
    /// Total number of policy evaluations by type.
    /// Labels: type (rbac, abac, hcl), result (allowed, denied, no_match)
    /// </summary>
    public static readonly Counter PolicyEvaluationsTotal = Prometheus.Metrics.CreateCounter(
        "usp_policy_evaluations_total",
        "Total number of policy evaluations by type",
        new CounterConfiguration
        {
            LabelNames = new[] { "type", "result" }
        });

    /// <summary>
    /// Histogram of authorization check duration in seconds.
    /// </summary>
    public static readonly Histogram AuthorizationCheckDuration = Prometheus.Metrics.CreateHistogram(
        "usp_authz_check_duration_seconds",
        "Duration of authorization checks in seconds",
        new HistogramConfiguration
        {
            Buckets = new[] { 0.001, 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1.0 }
        });

    // Audit Metrics

    /// <summary>
    /// Total number of audit events logged.
    /// Labels: event_type, success
    /// </summary>
    public static readonly Counter AuditEventsTotal = Prometheus.Metrics.CreateCounter(
        "usp_audit_events_total",
        "Total number of audit events logged",
        new CounterConfiguration
        {
            LabelNames = new[] { "event_type", "success" }
        });

    /// <summary>
    /// Total number of failed audit event writes.
    /// </summary>
    public static readonly Counter AuditEventFailuresTotal = Prometheus.Metrics.CreateCounter(
        "usp_audit_event_failures_total",
        "Total number of failed audit event writes");

    /// <summary>
    /// Histogram of audit event write duration in seconds.
    /// </summary>
    public static readonly Histogram AuditEventWriteDuration = Prometheus.Metrics.CreateHistogram(
        "usp_audit_event_write_duration_seconds",
        "Duration of audit event writes in seconds",
        new HistogramConfiguration
        {
            Buckets = new[] { 0.001, 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1.0 }
        });

    /// <summary>
    /// Current number of audit logs in the database.
    /// </summary>
    public static readonly Gauge AuditLogsCount = Prometheus.Metrics.CreateGauge(
        "usp_audit_logs_count",
        "Current total number of audit logs in the database");

    // Authentication Metrics

    /// <summary>
    /// Total number of login attempts.
    /// Labels: result (success, failure, mfa_required), method (password, oauth, saml)
    /// </summary>
    public static readonly Counter LoginAttemptsTotal = Prometheus.Metrics.CreateCounter(
        "usp_login_attempts_total",
        "Total number of login attempts",
        new CounterConfiguration
        {
            LabelNames = new[] { "result", "method" }
        });

    /// <summary>
    /// Total number of MFA verifications.
    /// Labels: method (totp, email, sms, backup_code), result (success, failure)
    /// </summary>
    public static readonly Counter MfaVerificationsTotal = Prometheus.Metrics.CreateCounter(
        "usp_mfa_verifications_total",
        "Total number of MFA verifications",
        new CounterConfiguration
        {
            LabelNames = new[] { "method", "result" }
        });

    /// <summary>
    /// Current number of active sessions.
    /// </summary>
    public static readonly Gauge ActiveSessionsCount = Prometheus.Metrics.CreateGauge(
        "usp_active_sessions",
        "Current number of active sessions");

    /// <summary>
    /// Total number of JWT tokens issued.
    /// Labels: type (access, refresh)
    /// </summary>
    public static readonly Counter TokensIssuedTotal = Prometheus.Metrics.CreateCounter(
        "usp_tokens_issued_total",
        "Total number of JWT tokens issued",
        new CounterConfiguration
        {
            LabelNames = new[] { "type" }
        });

    // Secrets Management Metrics

    /// <summary>
    /// Total number of secret operations.
    /// Labels: operation (read, write, delete, list), engine (kv, transit, pki)
    /// </summary>
    public static readonly Counter SecretOperationsTotal = Prometheus.Metrics.CreateCounter(
        "usp_secret_operations_total",
        "Total number of secret operations",
        new CounterConfiguration
        {
            LabelNames = new[] { "operation", "engine" }
        });

    /// <summary>
    /// Total number of secrets in the vault.
    /// </summary>
    public static readonly Gauge SecretsCount = Prometheus.Metrics.CreateGauge(
        "usp_secrets_total",
        "Total number of secrets in the vault");

    /// <summary>
    /// Histogram of secret operation duration in seconds.
    /// </summary>
    public static readonly Histogram SecretOperationDuration = Prometheus.Metrics.CreateHistogram(
        "usp_secret_operation_duration_seconds",
        "Duration of secret operations in seconds",
        new HistogramConfiguration
        {
            LabelNames = new[] { "operation" },
            Buckets = new[] { 0.001, 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1.0 }
        });

    // Vault Seal Metrics

    /// <summary>
    /// Vault seal status (0 = sealed, 1 = unsealed).
    /// </summary>
    public static readonly Gauge VaultSealStatus = Prometheus.Metrics.CreateGauge(
        "usp_seal_status",
        "Vault seal status (0=sealed, 1=unsealed)");

    /// <summary>
    /// Total number of vault initialization operations.
    /// </summary>
    public static readonly Counter VaultInitializationsTotal = Prometheus.Metrics.CreateCounter(
        "usp_vault_initializations_total",
        "Total number of vault initialization operations");

    /// <summary>
    /// Total number of unseal operations.
    /// Labels: result (success, failure)
    /// </summary>
    public static readonly Counter UnsealOperationsTotal = Prometheus.Metrics.CreateCounter(
        "usp_unseal_operations_total",
        "Total number of unseal operations",
        new CounterConfiguration
        {
            LabelNames = new[] { "result" }
        });

    /// <summary>
    /// Total number of seal operations.
    /// </summary>
    public static readonly Counter SealOperationsTotal = Prometheus.Metrics.CreateCounter(
        "usp_seal_operations_total",
        "Total number of seal operations");

    // Helper Methods

    /// <summary>
    /// Records an authorization check result.
    /// </summary>
    public static void RecordAuthorizationCheck(bool isAuthorized)
    {
        var result = isAuthorized ? "allowed" : "denied";
        AuthorizationChecksTotal.WithLabels(result).Inc();
    }

    /// <summary>
    /// Records an authorization check error.
    /// </summary>
    public static void RecordAuthorizationError()
    {
        AuthorizationChecksTotal.WithLabels("error").Inc();
    }

    /// <summary>
    /// Records a policy evaluation result.
    /// </summary>
    public static void RecordPolicyEvaluation(string policyType, string result)
    {
        PolicyEvaluationsTotal.WithLabels(policyType.ToLowerInvariant(), result).Inc();
    }

    /// <summary>
    /// Records an audit event.
    /// </summary>
    public static void RecordAuditEvent(AuditEventType eventType, bool success)
    {
        AuditEventsTotal.WithLabels(eventType.ToString(), success.ToString().ToLowerInvariant()).Inc();
    }

    /// <summary>
    /// Records an audit event write failure.
    /// </summary>
    public static void RecordAuditEventFailure()
    {
        AuditEventFailuresTotal.Inc();
    }

    /// <summary>
    /// Records a login attempt.
    /// </summary>
    public static void RecordLoginAttempt(string result, string method = "password")
    {
        LoginAttemptsTotal.WithLabels(result, method).Inc();
    }

    /// <summary>
    /// Records an MFA verification.
    /// </summary>
    public static void RecordMfaVerification(string method, bool success)
    {
        var result = success ? "success" : "failure";
        MfaVerificationsTotal.WithLabels(method, result).Inc();
    }

    /// <summary>
    /// Records a token issuance.
    /// </summary>
    public static void RecordTokenIssued(string type)
    {
        TokensIssuedTotal.WithLabels(type).Inc();
    }

    /// <summary>
    /// Records a secret operation.
    /// </summary>
    public static void RecordSecretOperation(string operation, string engine = "kv")
    {
        SecretOperationsTotal.WithLabels(operation, engine).Inc();
    }

    /// <summary>
    /// Updates the vault seal status metric.
    /// </summary>
    public static void UpdateSealStatus(bool isSealed)
    {
        VaultSealStatus.Set(isSealed ? 0 : 1);
    }

    /// <summary>
    /// Records a vault initialization.
    /// </summary>
    public static void RecordVaultInitialization()
    {
        VaultInitializationsTotal.Inc();
    }

    /// <summary>
    /// Records an unseal operation.
    /// </summary>
    public static void RecordUnsealOperation(bool success)
    {
        var result = success ? "success" : "failure";
        UnsealOperationsTotal.WithLabels(result).Inc();
    }

    /// <summary>
    /// Records a seal operation.
    /// </summary>
    public static void RecordSealOperation()
    {
        SealOperationsTotal.Inc();
    }
}
