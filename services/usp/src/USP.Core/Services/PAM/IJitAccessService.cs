using USP.Core.Models.DTOs.PAM;

namespace USP.Core.Services.PAM;

/// <summary>
/// Service for managing Just-In-Time (JIT) access grants
/// </summary>
public interface IJitAccessService
{
    /// <summary>
    /// Request a new JIT access grant
    /// </summary>
    Task<JitAccessDto> RequestAccessAsync(Guid userId, RequestJitAccessRequest request);

    /// <summary>
    /// Get JIT access grant by ID
    /// </summary>
    Task<JitAccessDto?> GetAccessByIdAsync(Guid accessId, Guid userId);

    /// <summary>
    /// Get active JIT access grants for a user
    /// </summary>
    Task<List<JitAccessDto>> GetActiveAccessGrantsAsync(Guid userId);

    /// <summary>
    /// Get all JIT access grants for a user
    /// </summary>
    Task<List<JitAccessDto>> GetUserAccessGrantsAsync(Guid userId, int? limit = 50);

    /// <summary>
    /// Get pending JIT access requests awaiting approval
    /// </summary>
    Task<List<JitAccessDto>> GetPendingRequestsAsync(Guid userId);

    /// <summary>
    /// Revoke a JIT access grant
    /// </summary>
    Task<bool> RevokeAccessAsync(Guid accessId, Guid userId, string reason);

    /// <summary>
    /// Approve a JIT access request (if approval required)
    /// </summary>
    Task<bool> ApproveAccessAsync(Guid accessId, Guid approverId);

    /// <summary>
    /// Deny a JIT access request
    /// </summary>
    Task<bool> DenyAccessAsync(Guid accessId, Guid approverId, string reason);

    /// <summary>
    /// Process expired JIT access grants (background job)
    /// </summary>
    Task<int> ProcessExpiredGrantsAsync();

    /// <summary>
    /// Get JIT access statistics
    /// </summary>
    Task<JitAccessStatisticsDto> GetStatisticsAsync(Guid userId);

    // Template Management

    /// <summary>
    /// Create a JIT access template
    /// </summary>
    Task<JitAccessTemplateDto> CreateTemplateAsync(Guid userId, CreateJitTemplateRequest request);

    /// <summary>
    /// Get JIT access template by ID
    /// </summary>
    Task<JitAccessTemplateDto?> GetTemplateByIdAsync(Guid templateId);

    /// <summary>
    /// Get all JIT access templates
    /// </summary>
    Task<List<JitAccessTemplateDto>> GetTemplatesAsync(Guid userId);

    /// <summary>
    /// Update a JIT access template
    /// </summary>
    Task<bool> UpdateTemplateAsync(Guid templateId, Guid userId, CreateJitTemplateRequest request);

    /// <summary>
    /// Delete a JIT access template
    /// </summary>
    Task<bool> DeleteTemplateAsync(Guid templateId, Guid userId);

    /// <summary>
    /// Activate/deactivate a JIT access template
    /// </summary>
    Task<bool> ToggleTemplateActiveAsync(Guid templateId, Guid userId, bool active);
}
