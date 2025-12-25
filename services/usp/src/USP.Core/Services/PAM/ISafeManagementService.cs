using USP.Core.Models.DTOs.PAM;

namespace USP.Core.Services.PAM;

/// <summary>
/// Service for managing privileged safes and accounts
/// </summary>
public interface ISafeManagementService
{
    // Safe Management

    /// <summary>
    /// Create a new privileged safe
    /// </summary>
    Task<PrivilegedSafeDto> CreateSafeAsync(Guid ownerId, CreateSafeRequest request);

    /// <summary>
    /// Get safe by ID
    /// </summary>
    Task<PrivilegedSafeDto?> GetSafeByIdAsync(Guid id, Guid userId);

    /// <summary>
    /// Get all safes accessible by a user
    /// </summary>
    Task<List<PrivilegedSafeDto>> GetSafesAsync(Guid userId, string? safeType = null);

    /// <summary>
    /// Update safe
    /// </summary>
    Task<bool> UpdateSafeAsync(Guid id, Guid userId, UpdateSafeRequest request);

    /// <summary>
    /// Delete safe (and all accounts in it)
    /// </summary>
    Task<bool> DeleteSafeAsync(Guid id, Guid userId);

    // Account Management

    /// <summary>
    /// Add a privileged account to a safe
    /// </summary>
    Task<PrivilegedAccountDto> AddAccountAsync(Guid safeId, Guid userId, CreatePrivilegedAccountRequest request);

    /// <summary>
    /// Get account by ID
    /// </summary>
    Task<PrivilegedAccountDto?> GetAccountByIdAsync(Guid accountId, Guid userId);

    /// <summary>
    /// Get all accounts in a safe
    /// </summary>
    Task<List<PrivilegedAccountDto>> GetAccountsAsync(Guid safeId, Guid userId);

    /// <summary>
    /// Update privileged account
    /// </summary>
    Task<bool> UpdateAccountAsync(Guid accountId, Guid userId, UpdatePrivilegedAccountRequest request);

    /// <summary>
    /// Delete privileged account
    /// </summary>
    Task<bool> DeleteAccountAsync(Guid accountId, Guid userId);

    /// <summary>
    /// Reveal account password (requires appropriate permissions)
    /// </summary>
    Task<RevealPasswordResponse> RevealPasswordAsync(Guid accountId, Guid userId, string? reason = null);

    /// <summary>
    /// Check if user has access to a safe
    /// </summary>
    Task<bool> HasSafeAccessAsync(Guid safeId, Guid userId, string permission = "read");

    /// <summary>
    /// Check if user has access to an account
    /// </summary>
    Task<bool> HasAccountAccessAsync(Guid accountId, Guid userId, string permission = "read");

    /// <summary>
    /// Get safe statistics
    /// </summary>
    Task<SafeStatisticsDto> GetStatisticsAsync(Guid userId);

    /// <summary>
    /// Search accounts across all accessible safes
    /// </summary>
    Task<List<PrivilegedAccountDto>> SearchAccountsAsync(Guid userId, string searchTerm, string? platform = null);
}
