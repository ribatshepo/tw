using Grpc.Core;
using USP.Core.Services.Authorization;
using USP.Core.Models.DTOs.Authorization;
using USP.Grpc.Authorization;

namespace USP.Api.Grpc;

/// <summary>
/// gRPC Authorization Service implementation
/// </summary>
public class GrpcAuthorizationService : AuthorizationService.AuthorizationServiceBase
{
    private readonly IRoleService _roleService;
    private readonly IAbacEngine _abacEngine;
    private readonly ILogger<GrpcAuthorizationService> _logger;

    public GrpcAuthorizationService(
        IRoleService roleService,
        IAbacEngine abacEngine,
        ILogger<GrpcAuthorizationService> logger)
    {
        _roleService = roleService;
        _abacEngine = abacEngine;
        _logger = logger;
    }

    public override async Task<CheckPermissionResponse> CheckPermission(
        CheckPermissionRequest request,
        ServerCallContext context)
    {
        try
        {
            if (!Guid.TryParse(request.UserId, out var userId))
            {
                return new CheckPermissionResponse
                {
                    Allowed = false,
                    Reason = "Invalid user ID"
                };
            }

            var hasPermission = await _roleService.UserHasPermissionAsync(userId, request.Permission);

            var response = new CheckPermissionResponse
            {
                Allowed = hasPermission,
                Reason = hasPermission ? "Permission granted" : "Permission denied"
            };

            if (!hasPermission)
            {
                response.RequiredPermissions.Add(request.Permission);
            }

            _logger.LogDebug(
                "Permission check for user {UserId}, permission {Permission}: {Result}",
                userId, request.Permission, hasPermission);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking permission for user {UserId}", request.UserId);
            throw new RpcException(new Status(StatusCode.Internal, "Failed to check permission"));
        }
    }

    public override async Task<CheckPermissionsResponse> CheckPermissions(
        CheckPermissionsRequest request,
        ServerCallContext context)
    {
        try
        {
            if (!Guid.TryParse(request.UserId, out var userId))
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid user ID"));
            }

            var response = new CheckPermissionsResponse
            {
                AllAllowed = true
            };

            foreach (var permission in request.Permissions)
            {
                var hasPermission = await _roleService.UserHasPermissionAsync(userId, permission);
                response.Results[permission] = hasPermission;

                if (!hasPermission)
                {
                    response.AllAllowed = false;
                }
            }

            _logger.LogDebug(
                "Bulk permission check for user {UserId}: {AllAllowed}",
                userId, response.AllAllowed);

            return response;
        }
        catch (RpcException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking permissions for user {UserId}", request.UserId);
            throw new RpcException(new Status(StatusCode.Internal, "Failed to check permissions"));
        }
    }

    public override async Task<GetUserPermissionsResponse> GetUserPermissions(
        GetUserPermissionsRequest request,
        ServerCallContext context)
    {
        try
        {
            if (!Guid.TryParse(request.UserId, out var userId))
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid user ID"));
            }

            // Get all roles for the user
            var roleNames = await _roleService.GetUserRolesAsync(userId);

            var allPermissions = new HashSet<string>();
            var permissionDetails = new List<PermissionInfo>();

            // Get permissions for each role
            foreach (var roleName in roleNames)
            {
                var roleDto = await _roleService.GetRoleByNameAsync(roleName);
                if (roleDto != null)
                {
                    foreach (var perm in roleDto.Permissions)
                    {
                        if (allPermissions.Add(perm.Name))
                        {
                            permissionDetails.Add(new PermissionInfo
                            {
                                Name = perm.Name,
                                Resource = perm.Resource,
                                Action = perm.Action,
                                Description = perm.Description ?? string.Empty
                            });
                        }
                    }
                }
            }

            var response = new GetUserPermissionsResponse();
            response.Permissions.AddRange(permissionDetails);

            _logger.LogDebug(
                "Retrieved {Count} permissions for user {UserId}",
                permissionDetails.Count, userId);

            return response;
        }
        catch (RpcException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting permissions for user {UserId}", request.UserId);
            throw new RpcException(new Status(StatusCode.Internal, "Failed to get user permissions"));
        }
    }

    public override async Task<GetUserRolesResponse> GetUserRoles(
        GetUserRolesRequest request,
        ServerCallContext context)
    {
        try
        {
            if (!Guid.TryParse(request.UserId, out var userId))
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid user ID"));
            }

            var roleNames = await _roleService.GetUserRolesAsync(userId);

            var response = new GetUserRolesResponse();

            foreach (var roleName in roleNames)
            {
                var roleDto = await _roleService.GetRoleByNameAsync(roleName);
                if (roleDto != null)
                {
                    var roleInfo = new RoleInfo
                    {
                        Id = roleDto.Id.ToString(),
                        Name = roleDto.Name,
                        Description = roleDto.Description ?? string.Empty
                    };

                    foreach (var perm in roleDto.Permissions)
                    {
                        roleInfo.Permissions.Add(new PermissionInfo
                        {
                            Name = perm.Name,
                            Resource = perm.Resource,
                            Action = perm.Action,
                            Description = perm.Description ?? string.Empty
                        });
                    }

                    response.Roles.Add(roleInfo);
                }
            }

            _logger.LogDebug(
                "Retrieved {Count} roles for user {UserId}",
                response.Roles.Count, userId);

            return response;
        }
        catch (RpcException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting roles for user {UserId}", request.UserId);
            throw new RpcException(new Status(StatusCode.Internal, "Failed to get user roles"));
        }
    }

    public override async Task<EvaluatePolicyResponse> EvaluatePolicy(
        EvaluatePolicyRequest request,
        ServerCallContext context)
    {
        try
        {
            if (!Guid.TryParse(request.UserId, out var userId))
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid user ID"));
            }

            var abacRequest = new AbacEvaluationRequest
            {
                SubjectId = userId,
                ResourceType = request.ResourceType,
                Action = request.Action,
                Context = request.Attributes.ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value)
            };

            if (!string.IsNullOrEmpty(request.ResourceId))
            {
                abacRequest.ResourceId = request.ResourceId;
                if (abacRequest.Context == null)
                {
                    abacRequest.Context = new Dictionary<string, object>();
                }
                abacRequest.Context["resource_id"] = request.ResourceId;
            }

            var abacResult = await _abacEngine.EvaluateAsync(abacRequest);

            var response = new EvaluatePolicyResponse
            {
                Allowed = abacResult.Allowed,
                Decision = abacResult.Decision,
                Reason = abacResult.Reasons.FirstOrDefault() ?? string.Empty
            };

            response.MatchedPolicies.AddRange(abacResult.AppliedPolicies);

            _logger.LogDebug(
                "ABAC policy evaluation for user {UserId}, action {Action}, resource {ResourceType}: {Decision}",
                userId, request.Action, request.ResourceType, response.Decision);

            return response;
        }
        catch (RpcException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating policy for user {UserId}", request.UserId);
            throw new RpcException(new Status(StatusCode.Internal, "Failed to evaluate policy"));
        }
    }
}
