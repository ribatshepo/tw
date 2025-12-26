namespace USP.Core.Services.Integrations;

/// <summary>
/// Interface for CI/CD platform integrations
/// </summary>
public interface ICicdIntegration
{
    /// <summary>
    /// Inject secret into CI/CD pipeline
    /// </summary>
    Task InjectSecretAsync(string projectId, string secretName, string secretValue, string? scope = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate dynamic secret for CI/CD pipeline
    /// </summary>
    Task<string> GenerateDynamicSecretAsync(string projectId, string secretType, int ttlSeconds = 3600, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revoke secret from CI/CD pipeline
    /// </summary>
    Task RevokeSecretAsync(string projectId, string secretName, CancellationToken cancellationToken = default);

    /// <summary>
    /// List secrets in project
    /// </summary>
    Task<List<string>> ListSecretsAsync(string projectId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get integration name
    /// </summary>
    string GetIntegrationName();
}
