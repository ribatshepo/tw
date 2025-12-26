using USP.Core.Models.DTOs.Authorization;

namespace USP.Core.Services.Authorization;

/// <summary>
/// Column-level security engine interface for fine-grained data access control
/// </summary>
public interface IColumnSecurityEngine
{
    /// <summary>
    /// Check column-level access for a user
    /// </summary>
    Task<ColumnAccessResponse> CheckColumnAccessAsync(ColumnAccessRequest request);

    /// <summary>
    /// Apply data masking rules to result set
    /// </summary>
    Task<Dictionary<string, object>> ApplyMaskingAsync(
        Guid userId,
        string tableName,
        Dictionary<string, object> data);

    /// <summary>
    /// Get column security rules for a table
    /// </summary>
    Task<List<ColumnSecurityRule>> GetColumnRulesAsync(string tableName);

    /// <summary>
    /// Create or update column security rule
    /// </summary>
    Task<ColumnSecurityRule> CreateColumnRuleAsync(CreateColumnRuleRequest request);

    /// <summary>
    /// Delete column security rule
    /// </summary>
    Task<bool> DeleteColumnRuleAsync(Guid ruleId);

    /// <summary>
    /// Get allowed columns for a user on a table
    /// </summary>
    Task<List<string>> GetAllowedColumnsAsync(Guid userId, string tableName, string operation);
}

/// <summary>
/// Column security rule entity
/// </summary>
public class ColumnSecurityRule
{
    public Guid Id { get; set; }
    public string TableName { get; set; } = string.Empty;
    public string ColumnName { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty; // read, write
    public string RestrictionType { get; set; } = "deny"; // allow, deny, mask, redact, tokenize
    public string? MaskingPattern { get; set; } // e.g., "***", "[REDACTED]"
    public string[]? AllowedRoles { get; set; }
    public string[]? DeniedRoles { get; set; }
    public string? Condition { get; set; } // ABAC condition
    public int Priority { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Request to create column security rule
/// </summary>
public class CreateColumnRuleRequest
{
    public string TableName { get; set; } = string.Empty;
    public string ColumnName { get; set; } = string.Empty;
    public string Operation { get; set; } = "read";
    public string RestrictionType { get; set; } = "deny";
    public string? MaskingPattern { get; set; }
    public List<string>? AllowedRoles { get; set; }
    public List<string>? DeniedRoles { get; set; }
    public string? Condition { get; set; }
    public int Priority { get; set; } = 100;
}
