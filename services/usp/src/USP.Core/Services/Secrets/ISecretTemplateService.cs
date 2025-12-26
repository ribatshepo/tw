using USP.Core.Models.DTOs.Secrets;

namespace USP.Core.Services.Secrets;

/// <summary>
/// Service for managing secret templates with validation and reusability
/// </summary>
public interface ISecretTemplateService
{
    /// <summary>
    /// Create secret template
    /// </summary>
    Task<SecretTemplateDto> CreateTemplateAsync(Guid userId, CreateSecretTemplateRequest request);

    /// <summary>
    /// Get secret template by ID
    /// </summary>
    Task<SecretTemplateDto?> GetTemplateAsync(Guid templateId, Guid userId);

    /// <summary>
    /// Get all secret templates
    /// </summary>
    Task<List<SecretTemplateDto>> GetTemplatesAsync(Guid userId, string? category = null);

    /// <summary>
    /// Update secret template
    /// </summary>
    Task<bool> UpdateTemplateAsync(Guid templateId, Guid userId, CreateSecretTemplateRequest request);

    /// <summary>
    /// Delete secret template
    /// </summary>
    Task<bool> DeleteTemplateAsync(Guid templateId, Guid userId);

    /// <summary>
    /// Create secret from template
    /// </summary>
    Task<bool> CreateSecretFromTemplateAsync(Guid userId, CreateSecretFromTemplateRequest request);

    /// <summary>
    /// Validate secret data against template
    /// </summary>
    Task<SecretTemplateValidationResult> ValidateSecretDataAsync(Guid templateId, Dictionary<string, string> fieldValues);

    /// <summary>
    /// Get built-in templates (database, AWS, Azure, etc.)
    /// </summary>
    Task<List<SecretTemplateDto>> GetBuiltInTemplatesAsync();

    /// <summary>
    /// Initialize built-in templates (startup)
    /// </summary>
    Task InitializeBuiltInTemplatesAsync();
}
