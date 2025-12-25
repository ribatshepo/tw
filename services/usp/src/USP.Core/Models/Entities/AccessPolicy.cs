using System.Text.Json;

namespace USP.Core.Models.Entities;

/// <summary>
/// Access policy entity supporting ABAC, HCL, and other policy types
/// </summary>
public class AccessPolicy
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string PolicyType { get; set; } = "ABAC"; // ABAC, HCL, RBAC
    public string Policy { get; set; } = string.Empty; // HCL text or JSON
    public bool IsActive { get; set; } = true;
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Legacy ABAC fields (optional, for JSON-based ABAC policies)
    public string Effect { get; set; } = "allow";
    public JsonDocument? Subjects { get; set; }
    public JsonDocument? Resources { get; set; }
    public string[]? Actions { get; set; }
    public JsonDocument? Conditions { get; set; }
    public int Priority { get; set; }
}
