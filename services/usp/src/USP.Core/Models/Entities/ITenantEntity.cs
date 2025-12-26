namespace USP.Core.Models.Entities;

/// <summary>
/// Interface for entities that are scoped to a workspace/tenant
/// </summary>
public interface ITenantEntity
{
    /// <summary>
    /// The workspace (tenant) ID that owns this entity
    /// </summary>
    Guid WorkspaceId { get; set; }
}
