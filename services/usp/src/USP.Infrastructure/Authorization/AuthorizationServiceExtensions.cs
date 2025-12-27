using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace USP.Infrastructure.Authorization;

/// <summary>
/// Extension methods for configuring USP authorization services
/// </summary>
public static class AuthorizationServiceExtensions
{
    /// <summary>
    /// Adds USP permission-based authorization to the service collection.
    /// This enables the [RequirePermission] attribute on controllers.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddPermissionBasedAuthorization(this IServiceCollection services)
    {
        // Register the custom authorization policy provider
        // This allows dynamic policy creation for permissions
        services.TryAddSingleton<IAuthorizationPolicyProvider, PermissionAuthorizationPolicyProvider>();

        // Register the permission authorization handler
        services.TryAddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

        return services;
    }
}
