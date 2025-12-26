namespace USP.Core.Models.Entities;

/// <summary>
/// Many-to-many relationship between SCIM users and groups
/// </summary>
public class ScimGroupMembership
{
    public Guid Id { get; set; }
    public Guid ScimGroupId { get; set; }
    public Guid ScimUserId { get; set; }
    public string MemberType { get; set; } = "User";
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual ScimGroup ScimGroup { get; set; } = null!;
    public virtual ScimUser ScimUser { get; set; } = null!;
}
