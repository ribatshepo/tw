using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace USP.Infrastructure.Authorization;

/// <summary>
/// Custom authorization policy provider that dynamically creates policies for permissions.
/// This allows [RequirePermission("resource:action")] to work without pre-registering policies.
/// </summary>
public class PermissionAuthorizationPolicyProvider : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _fallbackPolicyProvider;
    private const string PermissionPolicyPrefix = "Permission:";

    public PermissionAuthorizationPolicyProvider(IOptions<AuthorizationOptions> options)
    {
        // Create a fallback provider for default policies
        _fallbackPolicyProvider = new DefaultAuthorizationPolicyProvider(options);
    }

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync()
    {
        return _fallbackPolicyProvider.GetDefaultPolicyAsync();
    }

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync()
    {
        return _fallbackPolicyProvider.GetFallbackPolicyAsync();
    }

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        // Check if this is a permission-based policy
        if (policyName.StartsWith(PermissionPolicyPrefix, StringComparison.OrdinalIgnoreCase))
        {
            // Extract the permission from the policy name
            // Format: "Permission:resource:action" or "Permission:resource:action"
            var permission = policyName.Substring(PermissionPolicyPrefix.Length);

            // Create a policy with the permission requirement
            var policy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(PermissionAuthorizationRequirement.FromPermission(permission))
                .Build();

            return Task.FromResult<AuthorizationPolicy?>(policy);
        }

        // Fall back to the default provider for non-permission policies
        return _fallbackPolicyProvider.GetPolicyAsync(policyName);
    }
}
