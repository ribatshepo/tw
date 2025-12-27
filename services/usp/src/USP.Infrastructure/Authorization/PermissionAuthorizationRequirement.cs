using Microsoft.AspNetCore.Authorization;

namespace USP.Infrastructure.Authorization;

/// <summary>
/// Represents an authorization requirement that checks for a specific permission.
/// Used with ASP.NET Core's policy-based authorization.
/// </summary>
public class PermissionAuthorizationRequirement : IAuthorizationRequirement
{
    /// <summary>
    /// The resource being accessed (e.g., "secrets", "users", "roles")
    /// </summary>
    public string Resource { get; }

    /// <summary>
    /// The action being performed (e.g., "read", "write", "delete")
    /// </summary>
    public string Action { get; }

    /// <summary>
    /// Full permission string in format "resource:action"
    /// </summary>
    public string Permission => $"{Resource}:{Action}";

    public PermissionAuthorizationRequirement(string resource, string action)
    {
        Resource = resource ?? throw new ArgumentNullException(nameof(resource));
        Action = action ?? throw new ArgumentNullException(nameof(action));
    }

    /// <summary>
    /// Creates a requirement from a permission string in format "resource:action"
    /// </summary>
    public static PermissionAuthorizationRequirement FromPermission(string permission)
    {
        if (string.IsNullOrEmpty(permission))
        {
            throw new ArgumentException("Permission cannot be null or empty", nameof(permission));
        }

        var parts = permission.Split(':', 2);
        if (parts.Length != 2)
        {
            throw new ArgumentException(
                $"Permission '{permission}' is not in the correct format. Expected 'resource:action'",
                nameof(permission));
        }

        return new PermissionAuthorizationRequirement(parts[0], parts[1]);
    }
}
