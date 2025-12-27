using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using USP.Core.Interfaces.Services.Authorization;

namespace USP.Infrastructure.Authorization;

/// <summary>
/// Authorization handler that evaluates permission requirements using the AuthorizationService.
/// Integrates with ASP.NET Core's policy-based authorization system.
/// </summary>
public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionAuthorizationRequirement>
{
    private readonly IAuthorizationService _authorizationService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<PermissionAuthorizationHandler> _logger;

    public PermissionAuthorizationHandler(
        IAuthorizationService authorizationService,
        IHttpContextAccessor httpContextAccessor,
        ILogger<PermissionAuthorizationHandler> logger)
    {
        _authorizationService = authorizationService;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionAuthorizationRequirement requirement)
    {
        try
        {
            // Get user ID from claims
            var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("Authorization failed: User ID not found in claims");
                context.Fail();
                return;
            }

            // Build authorization context from HTTP context
            var authContext = BuildAuthorizationContext();

            // Check authorization using the authorization service
            var result = await _authorizationService.CheckAuthorizationAsync(
                userId,
                requirement.Resource,
                requirement.Action,
                authContext);

            if (result.IsAuthorized)
            {
                _logger.LogInformation(
                    "Authorization succeeded for user {UserId}, permission: {Permission}, reason: {Reason}",
                    userId, requirement.Permission, result.Reason);

                context.Succeed(requirement);
            }
            else
            {
                _logger.LogWarning(
                    "Authorization failed for user {UserId}, permission: {Permission}, reason: {Reason}",
                    userId, requirement.Permission, result.Reason);

                context.Fail();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error evaluating permission requirement {Permission}",
                requirement.Permission);

            // Fail secure - deny on error
            context.Fail();
        }
    }

    /// <summary>
    /// Builds an authorization context from the current HTTP request
    /// </summary>
    private AuthorizationContext BuildAuthorizationContext()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            return new AuthorizationContext();
        }

        return new AuthorizationContext
        {
            IpAddress = httpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = httpContext.Request.Headers["User-Agent"].ToString(),
            Timestamp = DateTime.UtcNow,
            Attributes = new Dictionary<string, object>
            {
                ["method"] = httpContext.Request.Method,
                ["path"] = httpContext.Request.Path.ToString(),
                ["protocol"] = httpContext.Request.Protocol
            }
        };
    }
}
