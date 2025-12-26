namespace USP.Core.Models.Entities;

/// <summary>
/// Column-level security rule entity
/// Defines access control, masking, and redaction rules for database columns
/// </summary>
public class ColumnSecurityRule
{
    public Guid Id { get; set; }

    public string TableName { get; set; } = string.Empty;

    public string ColumnName { get; set; } = string.Empty;

    public string Operation { get; set; } = "read"; // read, write, update, delete

    public string RestrictionType { get; set; } = "allow"; // allow, deny, mask, redact, tokenize

    public string? MaskingPattern { get; set; }

    public string[]? AllowedRoles { get; set; }

    public string[]? DeniedRoles { get; set; }

    public string? Condition { get; set; }

    public int Priority { get; set; } = 0;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid CreatedBy { get; set; }
}
