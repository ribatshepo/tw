using USP.Core.Models.DTOs.Secrets;

namespace USP.Core.Services.Secrets;

/// <summary>
/// Service for scanning repositories and code for exposed secrets
/// </summary>
public interface ISecretScannerService
{
    /// <summary>
    /// Scan repository for secrets
    /// </summary>
    Task<SecretScanResultDto> ScanRepositoryAsync(Guid userId, CreateSecretScanRequest request);

    /// <summary>
    /// Scan commit for secrets (git hook integration)
    /// </summary>
    Task<SecretScanResultDto> ScanCommitAsync(Guid userId, string commitHash, string repositoryUrl);

    /// <summary>
    /// Scan file content for secrets
    /// </summary>
    Task<List<SecretFindingDto>> ScanFileContentAsync(string content, string filePath);

    /// <summary>
    /// Get scan result by ID
    /// </summary>
    Task<SecretScanResultDto?> GetScanResultAsync(Guid scanId, Guid userId);

    /// <summary>
    /// Get all scan results for user
    /// </summary>
    Task<List<SecretScanResultDto>> GetScanResultsAsync(Guid userId, int? limit = 50);

    /// <summary>
    /// Get secret findings by scan ID
    /// </summary>
    Task<List<SecretFindingDto>> GetFindingsAsync(Guid scanId, Guid userId);

    /// <summary>
    /// Mark finding as false positive
    /// </summary>
    Task<bool> MarkAsFalsePositiveAsync(Guid findingId, Guid userId, MarkFalsePositiveRequest request);

    /// <summary>
    /// Remediate secret finding
    /// </summary>
    Task<bool> RemediateFindingAsync(Guid findingId, Guid userId, RemediateFindingRequest request);

    /// <summary>
    /// Create custom scan rule
    /// </summary>
    Task<SecretScannerRuleDto> CreateScanRuleAsync(Guid userId, CreateSecretScannerRuleRequest request);

    /// <summary>
    /// Get all scan rules
    /// </summary>
    Task<List<SecretScannerRuleDto>> GetScanRulesAsync(Guid userId);

    /// <summary>
    /// Update scan rule
    /// </summary>
    Task<bool> UpdateScanRuleAsync(Guid ruleId, Guid userId, CreateSecretScannerRuleRequest request);

    /// <summary>
    /// Delete scan rule
    /// </summary>
    Task<bool> DeleteScanRuleAsync(Guid ruleId, Guid userId);

    /// <summary>
    /// Get built-in scan rules (AWS keys, GitHub tokens, etc.)
    /// </summary>
    Task<List<SecretScannerRuleDto>> GetBuiltInRulesAsync();

    /// <summary>
    /// Process automated remediation workflow
    /// </summary>
    Task<int> ProcessAutomatedRemediationAsync();
}
