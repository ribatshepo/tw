using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using USP.Core.Models.DTOs.Authorization;
using USP.Core.Models.Entities;
using USP.Core.Services.Authorization;
using USP.Infrastructure.Data;

namespace USP.Infrastructure.Services.Authorization;

/// <summary>
/// Service for managing flow-based authorization workflows with multi-step approvals
/// </summary>
public class AuthorizationFlowService : IAuthorizationFlowService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AuthorizationFlowService> _logger;

    public AuthorizationFlowService(
        ApplicationDbContext context,
        ILogger<AuthorizationFlowService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<AuthorizationFlowResponse> InitiateFlowAsync(AuthorizationFlowRequest request)
    {
        try
        {
            _logger.LogInformation("Initiating authorization flow {FlowName} for user {UserId}, action {Action}, resource {Resource}",
                request.FlowName, request.UserId, request.Action, request.Resource);

            // Find the flow definition
            var flow = await _context.Set<AuthorizationFlow>()
                .FirstOrDefaultAsync(f => f.FlowName == request.FlowName && f.IsActive);

            if (flow == null)
            {
                _logger.LogWarning("Authorization flow {FlowName} not found", request.FlowName);
                return new AuthorizationFlowResponse
                {
                    Authorized = false,
                    FlowStatus = "error",
                    DenyReason = $"Flow '{request.FlowName}' not found or inactive"
                };
            }

            // Check if flow applies to this resource type and action
            if (!string.IsNullOrEmpty(flow.ResourceType) && flow.ResourceType != "*" && flow.ResourceType != request.Resource)
            {
                return new AuthorizationFlowResponse
                {
                    Authorized = false,
                    FlowStatus = "not_applicable",
                    DenyReason = $"Flow does not apply to resource type '{request.Resource}'"
                };
            }

            if (!string.IsNullOrEmpty(flow.Action) && flow.Action != "*" && flow.Action != request.Action)
            {
                return new AuthorizationFlowResponse
                {
                    Authorized = false,
                    FlowStatus = "not_applicable",
                    DenyReason = $"Flow does not apply to action '{request.Action}'"
                };
            }

            // Create flow instance
            var flowInstance = new AuthorizationFlowInstance
            {
                Id = Guid.NewGuid(),
                FlowId = flow.Id,
                RequesterId = request.UserId,
                ResourceType = request.Resource,
                ResourceId = request.Resource,
                Action = request.Action,
                Status = "pending",
                Context = JsonSerializer.Serialize(request.Context ?? new Dictionary<string, object>()),
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(24) // Default 24 hour expiry
            };

            _context.Set<AuthorizationFlowInstance>().Add(flowInstance);

            // Parse approver roles
            var approverRoles = JsonSerializer.Deserialize<List<string>>(flow.ApproverRoles) ?? new List<string>();

            // Get users in approver roles
            var approvers = await _context.Users
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .Where(u => u.UserRoles.Any(ur => approverRoles.Contains(ur.Role.Name ?? string.Empty)))
                .ToListAsync();

            if (approvers.Count == 0)
            {
                _logger.LogWarning("No approvers found for roles: {Roles}", string.Join(", ", approverRoles));
                flowInstance.Status = "denied";
                flowInstance.CompletedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                return new AuthorizationFlowResponse
                {
                    Authorized = false,
                    FlowStatus = "denied",
                    DenyReason = "No approvers available for this flow"
                };
            }

            // Create approval records for each approver
            foreach (var approver in approvers.Take(flow.RequiredApprovals))
            {
                var approval = new FlowApproval
                {
                    Id = Guid.NewGuid(),
                    FlowInstanceId = flowInstance.Id,
                    ApproverId = approver.Id,
                    Status = "pending",
                    CreatedAt = DateTime.UtcNow
                };

                _context.Set<FlowApproval>().Add(approval);
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Authorization flow instance {FlowInstanceId} created with {ApproverCount} required approvals",
                flowInstance.Id, flow.RequiredApprovals);

            return await GetFlowStatusAsync(flowInstance.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initiating authorization flow {FlowName}", request.FlowName);
            return new AuthorizationFlowResponse
            {
                Authorized = false,
                FlowStatus = "error",
                DenyReason = $"Error initiating flow: {ex.Message}"
            };
        }
    }

    public async Task<AuthorizationFlowResponse> ApproveAsync(Guid flowInstanceId, Guid approverId, string comment = "")
    {
        try
        {
            _logger.LogInformation("Processing approval for flow instance {FlowInstanceId} by approver {ApproverId}",
                flowInstanceId, approverId);

            var flowInstance = await _context.Set<AuthorizationFlowInstance>()
                .Include(fi => fi.Flow)
                .Include(fi => fi.Approvals)
                .FirstOrDefaultAsync(fi => fi.Id == flowInstanceId);

            if (flowInstance == null)
            {
                return new AuthorizationFlowResponse
                {
                    Authorized = false,
                    FlowStatus = "error",
                    DenyReason = "Flow instance not found"
                };
            }

            // Check if flow is still pending
            if (flowInstance.Status != "pending")
            {
                return new AuthorizationFlowResponse
                {
                    Authorized = flowInstance.Status == "approved",
                    FlowStatus = flowInstance.Status,
                    DenyReason = $"Flow is already {flowInstance.Status}"
                };
            }

            // Check if flow has expired
            if (flowInstance.ExpiresAt.HasValue && flowInstance.ExpiresAt.Value < DateTime.UtcNow)
            {
                flowInstance.Status = "expired";
                flowInstance.CompletedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                return new AuthorizationFlowResponse
                {
                    Authorized = false,
                    FlowStatus = "expired",
                    DenyReason = "Flow has expired"
                };
            }

            // Find the approval for this approver
            var approval = flowInstance.Approvals.FirstOrDefault(a => a.ApproverId == approverId);
            if (approval == null)
            {
                return new AuthorizationFlowResponse
                {
                    Authorized = false,
                    FlowStatus = "error",
                    DenyReason = "User is not an approver for this flow"
                };
            }

            // Update approval
            approval.Status = "approved";
            approval.ApprovedAt = DateTime.UtcNow;
            approval.Comment = comment;

            // Check if all required approvals are met
            var approvedCount = flowInstance.Approvals.Count(a => a.Status == "approved");
            if (approvedCount >= flowInstance.Flow.RequiredApprovals)
            {
                flowInstance.Status = "approved";
                flowInstance.CompletedAt = DateTime.UtcNow;
                _logger.LogInformation("Flow instance {FlowInstanceId} approved with {ApprovedCount} approvals",
                    flowInstanceId, approvedCount);
            }

            await _context.SaveChangesAsync();

            return await GetFlowStatusAsync(flowInstanceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving flow instance {FlowInstanceId}", flowInstanceId);
            return new AuthorizationFlowResponse
            {
                Authorized = false,
                FlowStatus = "error",
                DenyReason = $"Error processing approval: {ex.Message}"
            };
        }
    }

    public async Task<AuthorizationFlowResponse> DenyAsync(Guid flowInstanceId, Guid approverId, string comment = "")
    {
        try
        {
            _logger.LogInformation("Processing denial for flow instance {FlowInstanceId} by approver {ApproverId}",
                flowInstanceId, approverId);

            var flowInstance = await _context.Set<AuthorizationFlowInstance>()
                .Include(fi => fi.Flow)
                .Include(fi => fi.Approvals)
                .FirstOrDefaultAsync(fi => fi.Id == flowInstanceId);

            if (flowInstance == null)
            {
                return new AuthorizationFlowResponse
                {
                    Authorized = false,
                    FlowStatus = "error",
                    DenyReason = "Flow instance not found"
                };
            }

            if (flowInstance.Status != "pending")
            {
                return new AuthorizationFlowResponse
                {
                    Authorized = false,
                    FlowStatus = flowInstance.Status,
                    DenyReason = $"Flow is already {flowInstance.Status}"
                };
            }

            var approval = flowInstance.Approvals.FirstOrDefault(a => a.ApproverId == approverId);
            if (approval == null)
            {
                return new AuthorizationFlowResponse
                {
                    Authorized = false,
                    FlowStatus = "error",
                    DenyReason = "User is not an approver for this flow"
                };
            }

            // Update approval and flow instance
            approval.Status = "denied";
            approval.ApprovedAt = DateTime.UtcNow;
            approval.Comment = comment;

            flowInstance.Status = "denied";
            flowInstance.CompletedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Flow instance {FlowInstanceId} denied by approver {ApproverId}",
                flowInstanceId, approverId);

            return await GetFlowStatusAsync(flowInstanceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error denying flow instance {FlowInstanceId}", flowInstanceId);
            return new AuthorizationFlowResponse
            {
                Authorized = false,
                FlowStatus = "error",
                DenyReason = $"Error processing denial: {ex.Message}"
            };
        }
    }

    public async Task<AuthorizationFlowResponse> GetFlowStatusAsync(Guid flowInstanceId)
    {
        try
        {
            var flowInstance = await _context.Set<AuthorizationFlowInstance>()
                .Include(fi => fi.Flow)
                .Include(fi => fi.Approvals)
                    .ThenInclude(a => a.Approver)
                .FirstOrDefaultAsync(fi => fi.Id == flowInstanceId);

            if (flowInstance == null)
            {
                return new AuthorizationFlowResponse
                {
                    Authorized = false,
                    FlowStatus = "error",
                    DenyReason = "Flow instance not found"
                };
            }

            var response = new AuthorizationFlowResponse
            {
                Authorized = flowInstance.Status == "approved",
                FlowStatus = flowInstance.Status
            };

            // Get pending approvers
            response.PendingApprovers = flowInstance.Approvals
                .Where(a => a.Status == "pending")
                .Select(a => a.Approver.UserName ?? "Unknown")
                .ToList();

            // Get approval steps
            response.ApprovalSteps = flowInstance.Approvals
                .Select(a => new ApprovalStep
                {
                    Id = a.Id,
                    ApproverId = a.ApproverId,
                    Status = a.Status,
                    ApprovedAt = a.ApprovedAt,
                    Comment = a.Comment
                })
                .ToList();

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting flow status for {FlowInstanceId}", flowInstanceId);
            return new AuthorizationFlowResponse
            {
                Authorized = false,
                FlowStatus = "error",
                DenyReason = $"Error retrieving status: {ex.Message}"
            };
        }
    }

    public async Task<List<AuthorizationFlowResponse>> GetPendingApprovalsAsync(Guid userId)
    {
        try
        {
            var pendingApprovals = await _context.Set<FlowApproval>()
                .Include(a => a.FlowInstance)
                    .ThenInclude(fi => fi.Flow)
                .Include(a => a.FlowInstance)
                    .ThenInclude(fi => fi.Requester)
                .Where(a => a.ApproverId == userId && a.Status == "pending")
                .Where(a => a.FlowInstance.Status == "pending")
                .Where(a => !a.FlowInstance.ExpiresAt.HasValue || a.FlowInstance.ExpiresAt.Value > DateTime.UtcNow)
                .ToListAsync();

            var responses = new List<AuthorizationFlowResponse>();

            foreach (var approval in pendingApprovals)
            {
                var response = await GetFlowStatusAsync(approval.FlowInstanceId);
                responses.Add(response);
            }

            return responses;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pending approvals for user {UserId}", userId);
            return new List<AuthorizationFlowResponse>();
        }
    }

    public async Task<bool> CancelFlowAsync(Guid flowInstanceId, Guid requesterId)
    {
        try
        {
            var flowInstance = await _context.Set<AuthorizationFlowInstance>()
                .FirstOrDefaultAsync(fi => fi.Id == flowInstanceId);

            if (flowInstance == null)
            {
                return false;
            }

            // Only requester can cancel their own flow
            if (flowInstance.RequesterId != requesterId)
            {
                _logger.LogWarning("User {UserId} attempted to cancel flow {FlowInstanceId} but is not the requester",
                    requesterId, flowInstanceId);
                return false;
            }

            if (flowInstance.Status != "pending")
            {
                _logger.LogWarning("Cannot cancel flow {FlowInstanceId} with status {Status}",
                    flowInstanceId, flowInstance.Status);
                return false;
            }

            flowInstance.Status = "cancelled";
            flowInstance.CompletedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Flow instance {FlowInstanceId} cancelled by requester {RequesterId}",
                flowInstanceId, requesterId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling flow instance {FlowInstanceId}", flowInstanceId);
            return false;
        }
    }
}
