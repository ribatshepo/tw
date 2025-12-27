using Microsoft.AspNetCore.Authorization;

namespace USP.Infrastructure.Authorization;

/// <summary>
/// Authorization attribute that requires a specific permission.
/// Can be applied to controllers or individual actions.
///
/// Usage:
/// [RequirePermission("secrets:write")]
/// public IActionResult CreateSecret() { ... }
///
/// Or with separate resource and action:
/// [RequirePermission("secrets", "write")]
/// public IActionResult CreateSecret() { ... }
/// </summary>
public class RequirePermissionAttribute : AuthorizeAttribute
{
    /// <summary>
    /// The resource being accessed
    /// </summary>
    public string Resource { get; }

    /// <summary>
    /// The action being performed
    /// </summary>
    public string Action { get; }

    /// <summary>
    /// Full permission string
    /// </summary>
    public string Permission => $"{Resource}:{Action}";

    /// <summary>
    /// Creates a permission requirement from a permission string in format "resource:action"
    /// </summary>
    /// <param name="permission">Permission string in format "resource:action" (e.g., "secrets:write")</param>
    public RequirePermissionAttribute(string permission)
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

        Resource = parts[0];
        Action = parts[1];

        // Set the policy name - this will be matched by the authorization policy
        Policy = $"Permission:{permission}";
    }

    /// <summary>
    /// Creates a permission requirement with separate resource and action
    /// </summary>
    /// <param name="resource">The resource being accessed (e.g., "secrets")</param>
    /// <param name="action">The action being performed (e.g., "write")</param>
    public RequirePermissionAttribute(string resource, string action)
    {
        if (string.IsNullOrEmpty(resource))
        {
            throw new ArgumentException("Resource cannot be null or empty", nameof(resource));
        }

        if (string.IsNullOrEmpty(action))
        {
            throw new ArgumentException("Action cannot be null or empty", nameof(action));
        }

        Resource = resource;
        Action = action;

        // Set the policy name
        Policy = $"Permission:{resource}:{action}";
    }
}
