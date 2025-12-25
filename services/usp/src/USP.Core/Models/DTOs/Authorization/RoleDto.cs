namespace USP.Core.Models.DTOs.Authorization;

/// <summary>
/// Role data transfer object
/// </summary>
public class RoleDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsBuiltIn { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public List<PermissionDto> Permissions { get; set; } = new();
}

/// <summary>
/// Permission data transfer object
/// </summary>
public class PermissionDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Resource { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
}

/// <summary>
/// Create role request
/// </summary>
public class CreateRoleRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<string> Permissions { get; set; } = new();
}

/// <summary>
/// Update role request
/// </summary>
public class UpdateRoleRequest
{
    public string? Description { get; set; }
}

/// <summary>
/// Assign permissions request
/// </summary>
public class AssignPermissionsRequest
{
    public List<string> Permissions { get; set; } = new();
}

/// <summary>
/// Assign role to user request
/// </summary>
public class AssignRoleRequest
{
    public string RoleName { get; set; } = string.Empty;
    public Guid? NamespaceId { get; set; }
}
