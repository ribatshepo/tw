using Prometheus;

namespace USP.Api.Metrics;

/// <summary>
/// Centralized Prometheus metrics for USP - Unified Security Platform.
/// Provides 30+ custom metrics for authentication, authorization, secrets, PAM, and system monitoring.
/// </summary>
public static class PrometheusMetrics
{
    // ========================================
    // AUTHENTICATION METRICS (8 metrics)
    // ========================================

    /// <summary>
    /// Total number of login attempts.
    /// Labels: result (success/failure), method (password/oauth/saml/ldap/webauthn/passwordless)
    /// </summary>
    public static readonly Counter LoginAttempts = Prometheus.Metrics.CreateCounter(
        "usp_login_attempts_total",
        "Total number of login attempts",
        new CounterConfiguration
        {
            LabelNames = new[] { "result", "method" }
        });

    /// <summary>
    /// Total number of MFA verifications.
    /// Labels: method (email/totp/sms/push), result (success/failure)
    /// </summary>
    public static readonly Counter MfaVerifications = Prometheus.Metrics.CreateCounter(
        "usp_mfa_verifications_total",
        "Total number of MFA verification attempts",
        new CounterConfiguration
        {
            LabelNames = new[] { "method", "result" }
        });

    /// <summary>
    /// Number of currently active user sessions.
    /// </summary>
    public static readonly Gauge ActiveSessions = Prometheus.Metrics.CreateGauge(
        "usp_active_sessions",
        "Number of currently active user sessions");

    /// <summary>
    /// Duration of authentication operations in seconds.
    /// Buckets optimized for authentication flows (50ms to 5s).
    /// </summary>
    public static readonly Histogram AuthenticationDuration = Prometheus.Metrics.CreateHistogram(
        "usp_authentication_duration_seconds",
        "Duration of authentication operations in seconds",
        new HistogramConfiguration
        {
            Buckets = new[] { 0.05, 0.1, 0.25, 0.5, 1.0, 2.5, 5.0 }
        });

    /// <summary>
    /// Total number of password reset requests.
    /// </summary>
    public static readonly Counter PasswordResetRequests = Prometheus.Metrics.CreateCounter(
        "usp_password_reset_requests_total",
        "Total number of password reset requests");

    /// <summary>
    /// Total number of account lockouts due to failed login attempts.
    /// </summary>
    public static readonly Counter AccountLockouts = Prometheus.Metrics.CreateCounter(
        "usp_account_lockouts_total",
        "Total number of account lockouts");

    /// <summary>
    /// Total number of OAuth authorization flows.
    /// Labels: provider (google/github/microsoft/custom)
    /// </summary>
    public static readonly Counter OAuthAuthorizations = Prometheus.Metrics.CreateCounter(
        "usp_oauth_authorizations_total",
        "Total number of OAuth authorization flows",
        new CounterConfiguration
        {
            LabelNames = new[] { "provider" }
        });

    /// <summary>
    /// Total number of WebAuthn ceremonies (registration/authentication).
    /// Labels: type (registration/authentication), result (success/failure)
    /// </summary>
    public static readonly Counter WebAuthnCeremonies = Prometheus.Metrics.CreateCounter(
        "usp_webauthn_ceremonies_total",
        "Total number of WebAuthn/FIDO2 ceremonies",
        new CounterConfiguration
        {
            LabelNames = new[] { "type", "result" }
        });

    // ========================================
    // AUTHORIZATION METRICS (6 metrics)
    // ========================================

    /// <summary>
    /// Total number of authorization checks.
    /// Labels: result (allowed/denied)
    /// </summary>
    public static readonly Counter AuthorizationChecks = Prometheus.Metrics.CreateCounter(
        "usp_authz_checks_total",
        "Total number of authorization checks",
        new CounterConfiguration
        {
            LabelNames = new[] { "result" }
        });

    /// <summary>
    /// Duration of authorization operations in seconds.
    /// Buckets optimized for authz checks (1ms to 1s).
    /// </summary>
    public static readonly Histogram AuthorizationDuration = Prometheus.Metrics.CreateHistogram(
        "usp_authz_duration_seconds",
        "Duration of authorization operations in seconds",
        new HistogramConfiguration
        {
            Buckets = new[] { 0.001, 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1.0 }
        });

    /// <summary>
    /// Total number of policy evaluations.
    /// Labels: type (rbac/abac/hcl), result (allow/deny)
    /// </summary>
    public static readonly Counter PolicyEvaluations = Prometheus.Metrics.CreateCounter(
        "usp_policy_evaluations_total",
        "Total number of policy evaluations",
        new CounterConfiguration
        {
            LabelNames = new[] { "type", "result" }
        });

    /// <summary>
    /// Total number of role assignments.
    /// </summary>
    public static readonly Counter RoleAssignments = Prometheus.Metrics.CreateCounter(
        "usp_role_assignments_total",
        "Total number of role assignments");

    /// <summary>
    /// Total number of permission denials.
    /// Labels: reason (missing_role/policy_denied/expired_token/invalid_scope)
    /// </summary>
    public static readonly Counter PermissionDenials = Prometheus.Metrics.CreateCounter(
        "usp_permission_denials_total",
        "Total number of permission denials",
        new CounterConfiguration
        {
            LabelNames = new[] { "reason" }
        });

    /// <summary>
    /// Total number of ABAC (Attribute-Based Access Control) policy evaluations.
    /// Labels: result (allow/deny)
    /// </summary>
    public static readonly Counter AbacEvaluations = Prometheus.Metrics.CreateCounter(
        "usp_abac_evaluations_total",
        "Total number of ABAC policy evaluations",
        new CounterConfiguration
        {
            LabelNames = new[] { "result" }
        });

    // ========================================
    // SECRETS METRICS (8 metrics)
    // ========================================

    /// <summary>
    /// Total number of secret operations.
    /// Labels: operation (create/read/update/delete/list), engine (kv/transit/pki/database/ssh)
    /// </summary>
    public static readonly Counter SecretOperations = Prometheus.Metrics.CreateCounter(
        "usp_secret_operations_total",
        "Total number of secret operations",
        new CounterConfiguration
        {
            LabelNames = new[] { "operation", "engine" }
        });

    /// <summary>
    /// Total number of secret access operations.
    /// Labels: path (secret path - should be hashed or aggregated for cardinality)
    /// </summary>
    public static readonly Counter SecretAccess = Prometheus.Metrics.CreateCounter(
        "usp_secret_access_total",
        "Total number of secret access operations",
        new CounterConfiguration
        {
            LabelNames = new[] { "path" }
        });

    /// <summary>
    /// Total number of transit engine operations (encrypt/decrypt/sign/verify).
    /// Labels: operation (encrypt/decrypt/sign/verify), key (key name)
    /// </summary>
    public static readonly Counter TransitOperations = Prometheus.Metrics.CreateCounter(
        "usp_transit_operations_total",
        "Total number of transit engine operations",
        new CounterConfiguration
        {
            LabelNames = new[] { "operation", "key" }
        });

    /// <summary>
    /// Total number of certificates issued by PKI engine.
    /// </summary>
    public static readonly Counter CertificatesIssued = Prometheus.Metrics.CreateCounter(
        "usp_certificates_issued_total",
        "Total number of certificates issued");

    /// <summary>
    /// Total number of certificates revoked.
    /// </summary>
    public static readonly Counter CertificatesRevoked = Prometheus.Metrics.CreateCounter(
        "usp_certificates_revoked_total",
        "Total number of certificates revoked");

    /// <summary>
    /// Total number of secret rotations.
    /// Labels: type (password/api_key/certificate/ssh_key)
    /// </summary>
    public static readonly Counter SecretRotations = Prometheus.Metrics.CreateCounter(
        "usp_secret_rotations_total",
        "Total number of secret rotations",
        new CounterConfiguration
        {
            LabelNames = new[] { "type" }
        });

    /// <summary>
    /// Total number of secret versions created.
    /// </summary>
    public static readonly Counter SecretVersionsCreated = Prometheus.Metrics.CreateCounter(
        "usp_secret_versions_created_total",
        "Total number of secret versions created");

    /// <summary>
    /// Total number of database credentials issued.
    /// Labels: database (mysql/postgresql/mongodb/mssql)
    /// </summary>
    public static readonly Counter DatabaseCredentialsIssued = Prometheus.Metrics.CreateCounter(
        "usp_database_credentials_issued_total",
        "Total number of database credentials issued",
        new CounterConfiguration
        {
            LabelNames = new[] { "database" }
        });

    // ========================================
    // PAM METRICS (8 metrics)
    // ========================================

    /// <summary>
    /// Total number of PAM account checkouts.
    /// Labels: safe (safe name), account (account name - sanitized)
    /// </summary>
    public static readonly Counter PamCheckouts = Prometheus.Metrics.CreateCounter(
        "usp_pam_checkouts_total",
        "Total number of PAM account checkouts",
        new CounterConfiguration
        {
            LabelNames = new[] { "safe", "account" }
        });

    /// <summary>
    /// Number of currently checked out PAM accounts.
    /// </summary>
    public static readonly Gauge PamCheckedOut = Prometheus.Metrics.CreateGauge(
        "usp_pam_checked_out",
        "Number of currently checked out PAM accounts");

    /// <summary>
    /// Total number of PAM sessions (privileged access sessions).
    /// Labels: type (rdp/ssh/database/web)
    /// </summary>
    public static readonly Counter PamSessions = Prometheus.Metrics.CreateCounter(
        "usp_pam_sessions_total",
        "Total number of PAM sessions",
        new CounterConfiguration
        {
            LabelNames = new[] { "type" }
        });

    /// <summary>
    /// Duration of PAM sessions in seconds.
    /// Buckets optimized for session durations (1min to 8hrs).
    /// </summary>
    public static readonly Histogram PamSessionDuration = Prometheus.Metrics.CreateHistogram(
        "usp_pam_session_duration_seconds",
        "Duration of PAM sessions in seconds",
        new HistogramConfiguration
        {
            Buckets = new[] { 60.0, 300.0, 600.0, 1800.0, 3600.0, 7200.0, 14400.0, 28800.0 }
        });

    /// <summary>
    /// Total number of password rotations by connector type.
    /// Labels: connector (mysql/postgresql/windows/linux/azure/aws)
    /// </summary>
    public static readonly Counter PamPasswordRotations = Prometheus.Metrics.CreateCounter(
        "usp_pam_password_rotations_total",
        "Total number of password rotations",
        new CounterConfiguration
        {
            LabelNames = new[] { "connector" }
        });

    /// <summary>
    /// Total number of JIT (Just-In-Time) access requests.
    /// Labels: result (approved/denied/expired/revoked)
    /// </summary>
    public static readonly Counter JitAccessRequests = Prometheus.Metrics.CreateCounter(
        "usp_pam_jit_access_requests_total",
        "Total number of JIT access requests",
        new CounterConfiguration
        {
            LabelNames = new[] { "result" }
        });

    /// <summary>
    /// Total number of break-glass emergency access activations.
    /// </summary>
    public static readonly Counter BreakGlassActivations = Prometheus.Metrics.CreateCounter(
        "usp_pam_break_glass_activations_total",
        "Total number of break-glass emergency access activations");

    /// <summary>
    /// Total number of approval requests for dual control.
    /// Labels: result (approved/denied/timeout)
    /// </summary>
    public static readonly Counter ApprovalRequests = Prometheus.Metrics.CreateCounter(
        "usp_pam_approval_requests_total",
        "Total number of approval requests",
        new CounterConfiguration
        {
            LabelNames = new[] { "result" }
        });

    // ========================================
    // SYSTEM METRICS (3 metrics)
    // ========================================

    /// <summary>
    /// USP seal status (0 = sealed, 1 = unsealed).
    /// Unsealed is required for normal operations.
    /// </summary>
    public static readonly Gauge SealStatus = Prometheus.Metrics.CreateGauge(
        "usp_seal_status",
        "USP seal status (0=sealed, 1=unsealed)");

    /// <summary>
    /// USP service uptime in seconds since start.
    /// </summary>
    public static readonly Counter UptimeSeconds = Prometheus.Metrics.CreateCounter(
        "usp_uptime_seconds",
        "USP service uptime in seconds");

    /// <summary>
    /// Total number of errors by type.
    /// Labels: type (database/redis/validation/authentication/authorization/internal)
    /// </summary>
    public static readonly Counter Errors = Prometheus.Metrics.CreateCounter(
        "usp_error_total",
        "Total number of errors",
        new CounterConfiguration
        {
            LabelNames = new[] { "type" }
        });

    // ========================================
    // HELPER METHODS
    // ========================================

    /// <summary>
    /// Initializes the uptime counter background task.
    /// Should be called once during application startup.
    /// </summary>
    public static void InitializeUptimeCounter(CancellationToken cancellationToken)
    {
        Task.Run(async () =>
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
                UptimeSeconds.Inc();
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Updates the seal status gauge.
    /// Call this whenever the seal status changes.
    /// </summary>
    /// <param name="isUnsealed">True if unsealed, false if sealed</param>
    public static void UpdateSealStatus(bool isUnsealed)
    {
        SealStatus.Set(isUnsealed ? 1 : 0);
    }

    /// <summary>
    /// Records a successful login attempt.
    /// </summary>
    public static void RecordLoginSuccess(string method)
    {
        LoginAttempts.WithLabels("success", method).Inc();
    }

    /// <summary>
    /// Records a failed login attempt.
    /// </summary>
    public static void RecordLoginFailure(string method)
    {
        LoginAttempts.WithLabels("failure", method).Inc();
    }

    /// <summary>
    /// Records an MFA verification.
    /// </summary>
    public static void RecordMfaVerification(string method, bool success)
    {
        MfaVerifications.WithLabels(method, success ? "success" : "failure").Inc();
    }

    /// <summary>
    /// Updates the active sessions count.
    /// </summary>
    public static void SetActiveSessions(int count)
    {
        ActiveSessions.Set(count);
    }

    /// <summary>
    /// Records an authorization check result.
    /// </summary>
    public static void RecordAuthorizationCheck(bool allowed)
    {
        AuthorizationChecks.WithLabels(allowed ? "allowed" : "denied").Inc();
    }

    /// <summary>
    /// Records a secret operation.
    /// </summary>
    public static void RecordSecretOperation(string operation, string engine)
    {
        SecretOperations.WithLabels(operation, engine).Inc();
    }

    /// <summary>
    /// Records a PAM checkout.
    /// </summary>
    public static void RecordPamCheckout(string safe, string account)
    {
        PamCheckouts.WithLabels(safe, account).Inc();
    }

    /// <summary>
    /// Updates the current checked out PAM accounts count.
    /// </summary>
    public static void SetPamCheckedOut(int count)
    {
        PamCheckedOut.Set(count);
    }

    /// <summary>
    /// Records an error by type.
    /// </summary>
    public static void RecordError(string type)
    {
        Errors.WithLabels(type).Inc();
    }
}
