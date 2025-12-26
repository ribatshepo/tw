namespace USP.Core.Models.DTOs.Workspace;

/// <summary>
/// Workspace data transfer object
/// </summary>
public class WorkspaceDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid OwnerId { get; set; }
    public string? OwnerEmail { get; set; }
    public Guid? ParentWorkspaceId { get; set; }
    public string Status { get; set; } = "active";
    public string SubscriptionTier { get; set; } = "free";
    public string? CustomDomain { get; set; }
    public bool RequireMfa { get; set; }
    public int MinPasswordLength { get; set; }
    public int SessionTimeoutMinutes { get; set; }
    public bool IsBillable { get; set; }
    public long MonthlyCostCents { get; set; }
    public int MemberCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
