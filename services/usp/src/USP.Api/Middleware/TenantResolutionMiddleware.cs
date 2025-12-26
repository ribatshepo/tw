using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using USP.Infrastructure.Data;

namespace USP.Api.Middleware;

/// <summary>
/// Resolves the current workspace/tenant from various sources and stores it in HttpContext
/// CRITICAL: This middleware must run early in the pipeline to ensure all subsequent
/// operations are scoped to the correct tenant
/// </summary>
public class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantResolutionMiddleware> _logger;
    private readonly IConfiguration _configuration;

    public TenantResolutionMiddleware(
        RequestDelegate next,
        ILogger<TenantResolutionMiddleware> logger,
        IConfiguration configuration)
    {
        _next = next;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task InvokeAsync(HttpContext context, ApplicationDbContext dbContext)
    {
        Guid? workspaceId = null;
        string? resolutionMethod = null;

        try
        {
            // Skip tenant resolution for certain paths (health checks, swagger, etc.)
            if (ShouldSkipTenantResolution(context.Request.Path))
            {
                await _next(context);
                return;
            }

            // 1. Try to resolve from JWT claim (most common for authenticated requests)
            if (context.User.Identity?.IsAuthenticated == true)
            {
                var workspaceIdClaim = context.User.FindFirst("workspace_id")?.Value;
                if (!string.IsNullOrEmpty(workspaceIdClaim) && Guid.TryParse(workspaceIdClaim, out var claimWorkspaceId))
                {
                    workspaceId = claimWorkspaceId;
                    resolutionMethod = "jwt_claim";
                    _logger.LogDebug("Workspace resolved from JWT claim: {WorkspaceId}", workspaceId);
                }
            }

            // 2. Try to resolve from custom header (useful for API integrations)
            if (!workspaceId.HasValue)
            {
                var workspaceHeader = context.Request.Headers["X-Workspace-Id"].FirstOrDefault();
                if (!string.IsNullOrEmpty(workspaceHeader) && Guid.TryParse(workspaceHeader, out var headerWorkspaceId))
                {
                    // Verify user has access to this workspace
                    if (context.User.Identity?.IsAuthenticated == true)
                    {
                        var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                        if (!string.IsNullOrEmpty(userIdClaim) && Guid.TryParse(userIdClaim, out var userId))
                        {
                            var hasAccess = await VerifyWorkspaceAccessAsync(dbContext, userId, headerWorkspaceId);
                            if (hasAccess)
                            {
                                workspaceId = headerWorkspaceId;
                                resolutionMethod = "header";
                                _logger.LogDebug("Workspace resolved from X-Workspace-Id header: {WorkspaceId}", workspaceId);
                            }
                            else
                            {
                                _logger.LogWarning("User {UserId} attempted to access workspace {WorkspaceId} without permission",
                                    userId, headerWorkspaceId);
                            }
                        }
                    }
                }
            }

            // 3. Try to resolve from subdomain (e.g., acme.usp.example.com)
            if (!workspaceId.HasValue)
            {
                var host = context.Request.Host.Host;
                if (!string.IsNullOrEmpty(host))
                {
                    var subdomain = ExtractSubdomain(host);
                    if (!string.IsNullOrEmpty(subdomain) && !IsReservedSubdomain(subdomain))
                    {
                        // Disable query filter temporarily to look up workspace
                        var workspace = await dbContext.Set<USP.Core.Models.Entities.Workspace>()
                            .IgnoreQueryFilters()
                            .Where(w => w.Slug == subdomain && w.Status == "active")
                            .FirstOrDefaultAsync();

                        if (workspace != null)
                        {
                            workspaceId = workspace.Id;
                            resolutionMethod = "subdomain";
                            _logger.LogDebug("Workspace resolved from subdomain '{Subdomain}': {WorkspaceId}",
                                subdomain, workspaceId);
                        }
                    }
                }
            }

            // 4. Try to resolve from custom domain
            if (!workspaceId.HasValue)
            {
                var host = context.Request.Host.Host;
                if (!string.IsNullOrEmpty(host) && !IsSystemDomain(host))
                {
                    // Disable query filter temporarily to look up workspace
                    var workspace = await dbContext.Set<USP.Core.Models.Entities.Workspace>()
                        .IgnoreQueryFilters()
                        .Where(w => w.CustomDomain == host && w.Status == "active")
                        .FirstOrDefaultAsync();

                    if (workspace != null)
                    {
                        workspaceId = workspace.Id;
                        resolutionMethod = "custom_domain";
                        _logger.LogDebug("Workspace resolved from custom domain '{Domain}': {WorkspaceId}",
                            host, workspaceId);
                    }
                }
            }

            // 5. Try to resolve from API key (if API key authentication is used)
            if (!workspaceId.HasValue)
            {
                var apiKeyWorkspace = context.Items["ApiKeyWorkspaceId"] as Guid?;
                if (apiKeyWorkspace.HasValue)
                {
                    workspaceId = apiKeyWorkspace;
                    resolutionMethod = "api_key";
                    _logger.LogDebug("Workspace resolved from API key: {WorkspaceId}", workspaceId);
                }
            }

            // Store workspace ID in HttpContext.Items for downstream use
            if (workspaceId.HasValue)
            {
                context.Items["WorkspaceId"] = workspaceId.Value;
                context.Items["WorkspaceResolutionMethod"] = resolutionMethod;

                // Add to response headers for debugging (in development only)
                if (context.RequestServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment())
                {
                    context.Response.Headers.Append("X-Resolved-Workspace-Id", workspaceId.Value.ToString());
                    context.Response.Headers.Append("X-Resolution-Method", resolutionMethod ?? "unknown");
                }

                _logger.LogInformation(
                    "Tenant resolved: WorkspaceId={WorkspaceId}, Method={Method}, User={User}",
                    workspaceId,
                    resolutionMethod,
                    context.User.Identity?.Name ?? "anonymous");
            }
            else
            {
                // No workspace could be resolved - this might be OK for certain endpoints
                _logger.LogDebug("No workspace resolved for request: {Path}", context.Request.Path);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving workspace for request");
            // Don't fail the request, but log the error
        }

        await _next(context);
    }

    private async Task<bool> VerifyWorkspaceAccessAsync(ApplicationDbContext dbContext, Guid userId, Guid workspaceId)
    {
        try
        {
            var hasAccess = await dbContext.Set<USP.Core.Models.Entities.WorkspaceMember>()
                .IgnoreQueryFilters()
                .AnyAsync(wm =>
                    wm.WorkspaceId == workspaceId &&
                    wm.UserId == userId &&
                    wm.IsActive);

            return hasAccess;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying workspace access for user {UserId} to workspace {WorkspaceId}",
                userId, workspaceId);
            return false;
        }
    }

    private bool ShouldSkipTenantResolution(PathString path)
    {
        var pathValue = path.Value?.ToLowerInvariant() ?? string.Empty;

        // Skip tenant resolution for these paths
        return pathValue.StartsWith("/health") ||
               pathValue.StartsWith("/swagger") ||
               pathValue.StartsWith("/metrics") ||
               pathValue == "/" ||
               pathValue.StartsWith("/.well-known");
    }

    private string? ExtractSubdomain(string host)
    {
        if (string.IsNullOrEmpty(host))
            return null;

        // Remove port if present
        var hostWithoutPort = host.Split(':')[0];

        var parts = hostWithoutPort.Split('.');

        // Need at least 3 parts for a subdomain (e.g., subdomain.domain.com)
        if (parts.Length < 3)
            return null;

        // First part is the subdomain
        return parts[0];
    }

    private bool IsReservedSubdomain(string subdomain)
    {
        var reserved = new[] { "www", "api", "admin", "app", "dashboard", "console", "localhost" };
        return reserved.Contains(subdomain.ToLowerInvariant());
    }

    private bool IsSystemDomain(string host)
    {
        var systemDomains = _configuration.GetSection("TenantSettings:SystemDomains")
            .Get<string[]>() ?? new[] { "localhost", "127.0.0.1" };

        return systemDomains.Any(d => host.Contains(d, StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>
/// Extension methods for tenant resolution middleware
/// </summary>
public static class TenantResolutionMiddlewareExtensions
{
    public static IApplicationBuilder UseTenantResolution(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<TenantResolutionMiddleware>();
    }

    /// <summary>
    /// Get the current workspace ID from HttpContext
    /// </summary>
    public static Guid? GetWorkspaceId(this HttpContext context)
    {
        return context.Items["WorkspaceId"] as Guid?;
    }

    /// <summary>
    /// Get the current workspace ID or throw if not found
    /// </summary>
    public static Guid GetRequiredWorkspaceId(this HttpContext context)
    {
        var workspaceId = context.GetWorkspaceId();
        if (!workspaceId.HasValue)
        {
            throw new InvalidOperationException("Workspace ID not found in request context");
        }
        return workspaceId.Value;
    }
}
