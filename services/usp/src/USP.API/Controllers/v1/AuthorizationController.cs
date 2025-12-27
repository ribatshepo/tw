using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using USP.Core.Domain.Enums;
using USP.Core.Interfaces.Services.Authorization;

namespace USP.API.Controllers.v1;

/// <summary>
/// Authorization and access control endpoints
/// </summary>
[ApiController]
[Route("api/v1/authz")]
[Produces("application/json")]
public class AuthorizationController : ControllerBase
{
    private readonly IAuthorizationService _authzService;
    private readonly ILogger<AuthorizationController> _logger;

    public AuthorizationController(
        IAuthorizationService authzService,
        ILogger<AuthorizationController> logger)
    {
        _authzService = authzService;
        _logger = logger;
    }

    /// <summary>
    /// Check if a user is authorized to perform an action on a resource
    /// </summary>
    [HttpPost("check")]
    [Authorize]
    [ProducesResponseType(typeof(AuthorizationCheckResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CheckAuthorization([FromBody] AuthorizationCheckRequest request)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { error = "User not authenticated" });
            }

            var context = new AuthorizationContext
            {
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                UserAgent = HttpContext.Request.Headers["User-Agent"].ToString(),
                Timestamp = DateTime.UtcNow,
                Attributes = request.Context ?? new Dictionary<string, object>()
            };

            var result = await _authzService.CheckAuthorizationAsync(
                userId,
                request.Resource,
                request.Action,
                context);

            _logger.LogInformation(
                "Authorization check for user {UserId}, resource: {Resource}, action: {Action}, result: {IsAuthorized}",
                userId, request.Resource, request.Action, result.IsAuthorized);

            return Ok(new AuthorizationCheckResponse
            {
                IsAuthorized = result.IsAuthorized,
                Resource = result.Resource,
                Action = result.Action,
                Reason = result.Reason,
                PolicyId = result.PolicyId,
                PolicyType = result.PolicyType?.ToString()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking authorization");
            return StatusCode(500, new { error = "Authorization check failed" });
        }
    }

    /// <summary>
    /// Check authorization for multiple resource-action pairs in a single request
    /// </summary>
    [HttpPost("check-batch")]
    [Authorize]
    [ProducesResponseType(typeof(IEnumerable<AuthorizationCheckResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CheckBatchAuthorization([FromBody] BatchAuthorizationCheckRequest request)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { error = "User not authenticated" });
            }

            var context = new AuthorizationContext
            {
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                UserAgent = HttpContext.Request.Headers["User-Agent"].ToString(),
                Timestamp = DateTime.UtcNow,
                Attributes = request.Context ?? new Dictionary<string, object>()
            };

            var resourceActionPairs = request.Requests.Select(r => new ResourceActionPair
            {
                Resource = r.Resource,
                Action = r.Action
            });

            var results = await _authzService.CheckBatchAuthorizationAsync(
                userId,
                resourceActionPairs,
                context);

            var responses = results.Select(r => new AuthorizationCheckResponse
            {
                IsAuthorized = r.IsAuthorized,
                Resource = r.Resource,
                Action = r.Action,
                Reason = r.Reason,
                PolicyId = r.PolicyId,
                PolicyType = r.PolicyType?.ToString()
            });

            return Ok(responses);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking batch authorization");
            return StatusCode(500, new { error = "Batch authorization check failed" });
        }
    }

    /// <summary>
    /// Get all permissions for the current user
    /// </summary>
    [HttpGet("permissions")]
    [Authorize]
    [ProducesResponseType(typeof(UserPermissionsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetUserPermissions()
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { error = "User not authenticated" });
            }

            var permissions = await _authzService.GetUserPermissionsAsync(userId);

            return Ok(new UserPermissionsResponse
            {
                UserId = userId,
                Permissions = permissions.ToList()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user permissions");
            return StatusCode(500, new { error = "Failed to get user permissions" });
        }
    }

    /// <summary>
    /// Simulate policy evaluation (for testing policies before deployment)
    /// </summary>
    [HttpPost("simulate")]
    [Authorize]
    [ProducesResponseType(typeof(PolicySimulationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SimulatePolicyEvaluation([FromBody] PolicySimulationRequest request)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { error = "User not authenticated" });
            }

            var context = new AuthorizationContext
            {
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                UserAgent = HttpContext.Request.Headers["User-Agent"].ToString(),
                Timestamp = DateTime.UtcNow,
                Attributes = request.Context ?? new Dictionary<string, object>()
            };

            var result = await _authzService.SimulatePolicyEvaluationAsync(
                userId,
                request.Resource,
                request.Action,
                context);

            return Ok(new PolicySimulationResponse
            {
                IsAuthorized = result.IsAuthorized,
                UserRoles = result.UserRoles,
                UserPermissions = result.UserPermissions,
                EvaluatedPolicies = result.EvaluatedPolicies.Select(p => new EvaluatedPolicyInfo
                {
                    PolicyId = p.PolicyId,
                    PolicyName = p.PolicyName,
                    PolicyType = p.PolicyType.ToString(),
                    Matched = p.Matched,
                    Effect = p.Effect,
                    Priority = p.Priority,
                    MatchReason = p.MatchReason
                }).ToList(),
                Explanation = result.Explanation
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error simulating policy evaluation");
            return StatusCode(500, new { error = "Policy simulation failed" });
        }
    }

    /// <summary>
    /// Get all policies applicable to the current user
    /// </summary>
    [HttpGet("policies")]
    [Authorize]
    [ProducesResponseType(typeof(UserPoliciesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetApplicablePolicies([FromQuery] PolicyType? policyType = null)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { error = "User not authenticated" });
            }

            var policies = await _authzService.GetApplicablePoliciesAsync(userId, policyType);

            return Ok(new UserPoliciesResponse
            {
                UserId = userId,
                Policies = policies.Select(p => new PolicyInfo
                {
                    Id = p.Id,
                    Name = p.Name,
                    Type = p.Type.ToString(),
                    Effect = p.Effect,
                    Priority = p.Priority,
                    IsActive = p.IsActive,
                    Description = p.Description
                }).ToList()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting applicable policies");
            return StatusCode(500, new { error = "Failed to get applicable policies" });
        }
    }
}

// DTOs

public class AuthorizationCheckRequest
{
    public required string Resource { get; set; }
    public required string Action { get; set; }
    public Dictionary<string, object>? Context { get; set; }
}

public class BatchAuthorizationCheckRequest
{
    public required List<AuthorizationCheckRequest> Requests { get; set; }
    public Dictionary<string, object>? Context { get; set; }
}

public class AuthorizationCheckResponse
{
    public required bool IsAuthorized { get; set; }
    public required string Resource { get; set; }
    public required string Action { get; set; }
    public string? Reason { get; set; }
    public string? PolicyId { get; set; }
    public string? PolicyType { get; set; }
}

public class UserPermissionsResponse
{
    public required string UserId { get; set; }
    public required List<string> Permissions { get; set; }
}

public class PolicySimulationRequest
{
    public required string Resource { get; set; }
    public required string Action { get; set; }
    public Dictionary<string, object>? Context { get; set; }
}

public class PolicySimulationResponse
{
    public required bool IsAuthorized { get; set; }
    public List<string> UserRoles { get; set; } = new();
    public List<string> UserPermissions { get; set; } = new();
    public List<EvaluatedPolicyInfo> EvaluatedPolicies { get; set; } = new();
    public string? Explanation { get; set; }
}

public class EvaluatedPolicyInfo
{
    public required string PolicyId { get; set; }
    public required string PolicyName { get; set; }
    public required string PolicyType { get; set; }
    public required bool Matched { get; set; }
    public required string Effect { get; set; }
    public int Priority { get; set; }
    public string? MatchReason { get; set; }
}

public class UserPoliciesResponse
{
    public required string UserId { get; set; }
    public required List<PolicyInfo> Policies { get; set; }
}

public class PolicyInfo
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public required string Type { get; set; }
    public required string Effect { get; set; }
    public int Priority { get; set; }
    public bool IsActive { get; set; }
    public string? Description { get; set; }
}
