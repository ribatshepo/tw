using Microsoft.AspNetCore.Builder;

namespace USP.Infrastructure.Middleware;

/// <summary>
/// Extension methods for registering audit middleware
/// </summary>
public static class AuditMiddlewareExtensions
{
    /// <summary>
    /// Adds automatic audit logging middleware to the application pipeline.
    /// This middleware logs all HTTP requests to the audit service.
    ///
    /// IMPORTANT: This middleware should be added AFTER authentication middleware
    /// so that user identity is available for audit logging.
    /// </summary>
    /// <param name="app">The application builder</param>
    /// <returns>The application builder for chaining</returns>
    public static IApplicationBuilder UseAuditLogging(this IApplicationBuilder app)
    {
        return app.UseMiddleware<AuditMiddleware>();
    }
}
