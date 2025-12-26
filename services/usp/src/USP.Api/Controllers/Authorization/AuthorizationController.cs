using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using USP.Core.Models.DTOs.Authorization;
using USP.Core.Models.Entities;
using USP.Core.Services.Authorization;
using USP.Infrastructure.Data;

namespace USP.Api.Controllers.Authorization;

/// <summary>
/// Authorization operations including ABAC, HCL policies, and approval workflows
/// </summary>
[ApiController]
[Route("api/authz")]
[Produces("application/json")]
public class AuthorizationController : ControllerBase
{
    private readonly IAbacEngine _abacEngine;
    private readonly IHclPolicyEvaluator _hclEvaluator;
    private readonly IAuthorizationFlowService _flowService;
    private readonly IColumnSecurityEngine _columnSecurityEngine;
    private readonly IContextEvaluator _contextEvaluator;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AuthorizationController> _logger;

    public AuthorizationController(
        IAbacEngine abacEngine,
        IHclPolicyEvaluator hclEvaluator,
        IAuthorizationFlowService flowService,
        IColumnSecurityEngine columnSecurityEngine,
        IContextEvaluator contextEvaluator,
        ApplicationDbContext context,
        ILogger<AuthorizationController> logger)
    {
        _abacEngine = abacEngine;
        _hclEvaluator = hclEvaluator;
        _flowService = flowService;
        _columnSecurityEngine = columnSecurityEngine;
        _contextEvaluator = contextEvaluator;
        _context = context;
        _logger = logger;
    }

    #region ABAC Endpoints

    /// <summary>
    /// Evaluate ABAC policy for authorization decision
    /// </summary>
    [HttpPost("abac/evaluate")]
    [Authorize]
    [ProducesResponseType(typeof(AbacEvaluationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AbacEvaluationResponse>> EvaluateAbac([FromBody] AbacEvaluationRequest request)
    {
        try
        {
            var result = await _abacEngine.EvaluateAsync(request);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating ABAC policy");
            return StatusCode(500, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "ABAC evaluation error",
                Detail = ex.Message
            });
        }
    }

    /// <summary>
    /// Extract attributes for ABAC evaluation
    /// </summary>
    [HttpPost("abac/attributes")]
    [Authorize]
    [ProducesResponseType(typeof(ExtractedAttributes), StatusCodes.Status200OK)]
    public async Task<ActionResult<ExtractedAttributes>> ExtractAttributes([FromBody] AttributeExtractionRequest request)
    {
        try
        {
            var attributes = await _abacEngine.ExtractAttributesAsync(request);
            return Ok(attributes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting attributes");
            return StatusCode(500, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Attribute extraction error",
                Detail = ex.Message
            });
        }
    }

    /// <summary>
    /// Check if user has access based on ABAC policies
    /// </summary>
    [HttpPost("abac/check")]
    [Authorize]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    public async Task<ActionResult<bool>> CheckAbacAccess(
        [FromQuery] Guid userId,
        [FromQuery] string action,
        [FromQuery] string resourceType,
        [FromQuery] string? resourceId = null)
    {
        try
        {
            var hasAccess = await _abacEngine.HasAccessAsync(userId, action, resourceType, resourceId);
            return Ok(hasAccess);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking ABAC access");
            return StatusCode(500, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Access check error",
                Detail = ex.Message
            });
        }
    }

    #endregion

    #region HCL Policy Endpoints

    /// <summary>
    /// Evaluate HCL policy for authorization
    /// </summary>
    [HttpPost("hcl/evaluate")]
    [Authorize]
    [ProducesResponseType(typeof(HclAuthorizationResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<HclAuthorizationResponse>> EvaluateHcl([FromBody] HclAuthorizationRequest request)
    {
        try
        {
            var result = await _hclEvaluator.EvaluateAsync(request);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating HCL policy");
            return StatusCode(500, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "HCL evaluation error",
                Detail = ex.Message
            });
        }
    }

    /// <summary>
    /// Get user's capabilities on a path
    /// </summary>
    [HttpGet("hcl/capabilities")]
    [Authorize]
    [ProducesResponseType(typeof(List<string>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<string>>> GetCapabilities(
        [FromQuery] Guid userId,
        [FromQuery] string path)
    {
        try
        {
            var capabilities = await _hclEvaluator.GetCapabilitiesAsync(userId, path);
            return Ok(capabilities);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting capabilities");
            return StatusCode(500, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Error retrieving capabilities",
                Detail = ex.Message
            });
        }
    }

    /// <summary>
    /// Validate HCL policy syntax
    /// </summary>
    [HttpPost("hcl/validate")]
    [Authorize]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public ActionResult ValidateHcl([FromBody] string hclPolicy)
    {
        try
        {
            var (isValid, errors) = _hclEvaluator.ValidatePolicy(hclPolicy);
            return Ok(new { isValid, errors });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating HCL policy");
            return StatusCode(500, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Validation error",
                Detail = ex.Message
            });
        }
    }

    #endregion

    #region Policy Management Endpoints

    /// <summary>
    /// Create a new policy
    /// </summary>
    [HttpPost("policies")]
    [Authorize]
    [ProducesResponseType(typeof(PolicyDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PolicyDto>> CreatePolicy([FromBody] CreatePolicyRequest request)
    {
        try
        {
            // Validate policy syntax
            if (request.PolicyType == "HCL")
            {
                var (isValid, errors) = _hclEvaluator.ValidatePolicy(request.Policy);
                if (!isValid)
                {
                    return BadRequest(new ProblemDetails
                    {
                        Status = StatusCodes.Status400BadRequest,
                        Title = "Invalid HCL policy",
                        Detail = string.Join(", ", errors)
                    });
                }
            }

            var userId = Guid.Parse(User.FindFirst("sub")?.Value ?? User.FindFirst("userId")?.Value ?? Guid.Empty.ToString());

            var policy = new AccessPolicy
            {
                Id = Guid.NewGuid(),
                Name = request.Name,
                Description = request.Description,
                PolicyType = request.PolicyType,
                Policy = request.Policy,
                IsActive = request.IsActive,
                CreatedBy = userId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.AccessPolicies.Add(policy);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Policy {PolicyName} created by user {UserId}", policy.Name, userId);

            var dto = new PolicyDto
            {
                Id = policy.Id,
                Name = policy.Name,
                Description = policy.Description ?? string.Empty,
                PolicyType = policy.PolicyType,
                Policy = policy.Policy,
                IsActive = policy.IsActive,
                CreatedAt = policy.CreatedAt,
                UpdatedAt = policy.UpdatedAt,
                CreatedBy = policy.CreatedBy
            };

            return CreatedAtAction(nameof(GetPolicy), new { id = policy.Id }, dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating policy");
            return StatusCode(500, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Error creating policy",
                Detail = ex.Message
            });
        }
    }

    /// <summary>
    /// Get policy by ID
    /// </summary>
    [HttpGet("policies/{id:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(PolicyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PolicyDto>> GetPolicy(Guid id)
    {
        try
        {
            var policy = await _context.AccessPolicies.FindAsync(id);

            if (policy == null)
            {
                return NotFound(new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Policy not found",
                    Detail = $"Policy {id} does not exist"
                });
            }

            var dto = new PolicyDto
            {
                Id = policy.Id,
                Name = policy.Name,
                Description = policy.Description ?? string.Empty,
                PolicyType = policy.PolicyType,
                Policy = policy.Policy,
                IsActive = policy.IsActive,
                CreatedAt = policy.CreatedAt,
                UpdatedAt = policy.UpdatedAt,
                CreatedBy = policy.CreatedBy
            };

            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving policy {PolicyId}", id);
            return StatusCode(500, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Error retrieving policy",
                Detail = ex.Message
            });
        }
    }

    /// <summary>
    /// List all policies
    /// </summary>
    [HttpGet("policies")]
    [Authorize]
    [ProducesResponseType(typeof(List<PolicyDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<PolicyDto>>> ListPolicies(
        [FromQuery] string? policyType = null,
        [FromQuery] bool? isActive = null)
    {
        try
        {
            var query = _context.AccessPolicies.AsQueryable();

            if (!string.IsNullOrEmpty(policyType))
            {
                query = query.Where(p => p.PolicyType == policyType);
            }

            if (isActive.HasValue)
            {
                query = query.Where(p => p.IsActive == isActive.Value);
            }

            var policies = await query.ToListAsync();

            var dtos = policies.Select(p => new PolicyDto
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description ?? string.Empty,
                PolicyType = p.PolicyType,
                Policy = p.Policy,
                IsActive = p.IsActive,
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt,
                CreatedBy = p.CreatedBy
            }).ToList();

            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing policies");
            return StatusCode(500, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Error listing policies",
                Detail = ex.Message
            });
        }
    }

    /// <summary>
    /// Update policy
    /// </summary>
    [HttpPut("policies/{id:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(PolicyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PolicyDto>> UpdatePolicy(Guid id, [FromBody] CreatePolicyRequest request)
    {
        try
        {
            var policy = await _context.AccessPolicies.FindAsync(id);

            if (policy == null)
            {
                return NotFound(new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Policy not found",
                    Detail = $"Policy {id} does not exist"
                });
            }

            // Validate policy syntax
            if (request.PolicyType == "HCL")
            {
                var (isValid, errors) = _hclEvaluator.ValidatePolicy(request.Policy);
                if (!isValid)
                {
                    return BadRequest(new ProblemDetails
                    {
                        Status = StatusCodes.Status400BadRequest,
                        Title = "Invalid HCL policy",
                        Detail = string.Join(", ", errors)
                    });
                }
            }

            policy.Name = request.Name;
            policy.Description = request.Description;
            policy.PolicyType = request.PolicyType;
            policy.Policy = request.Policy;
            policy.IsActive = request.IsActive;
            policy.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Policy {PolicyId} updated", id);

            var dto = new PolicyDto
            {
                Id = policy.Id,
                Name = policy.Name,
                Description = policy.Description ?? string.Empty,
                PolicyType = policy.PolicyType,
                Policy = policy.Policy,
                IsActive = policy.IsActive,
                CreatedAt = policy.CreatedAt,
                UpdatedAt = policy.UpdatedAt,
                CreatedBy = policy.CreatedBy
            };

            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating policy {PolicyId}", id);
            return StatusCode(500, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Error updating policy",
                Detail = ex.Message
            });
        }
    }

    /// <summary>
    /// Delete policy
    /// </summary>
    [HttpDelete("policies/{id:guid}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeletePolicy(Guid id)
    {
        try
        {
            var policy = await _context.AccessPolicies.FindAsync(id);

            if (policy == null)
            {
                return NotFound(new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Policy not found",
                    Detail = $"Policy {id} does not exist"
                });
            }

            _context.AccessPolicies.Remove(policy);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Policy {PolicyId} deleted", id);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting policy {PolicyId}", id);
            return StatusCode(500, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Error deleting policy",
                Detail = ex.Message
            });
        }
    }

    /// <summary>
    /// Simulate policy evaluation
    /// </summary>
    [HttpPost("policies/simulate")]
    [Authorize]
    [ProducesResponseType(typeof(PolicySimulationResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<PolicySimulationResponse>> SimulatePolicy([FromBody] PolicySimulationRequest request)
    {
        try
        {
            var result = await _abacEngine.SimulatePolicyAsync(request);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error simulating policy");
            return StatusCode(500, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Policy simulation error",
                Detail = ex.Message
            });
        }
    }

    #endregion

    #region Authorization Flow Endpoints

    /// <summary>
    /// Initiate an authorization flow
    /// </summary>
    [HttpPost("flows/initiate")]
    [Authorize]
    [ProducesResponseType(typeof(AuthorizationFlowResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<AuthorizationFlowResponse>> InitiateFlow([FromBody] AuthorizationFlowRequest request)
    {
        try
        {
            var result = await _flowService.InitiateFlowAsync(request);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initiating authorization flow");
            return StatusCode(500, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Flow initiation error",
                Detail = ex.Message
            });
        }
    }

    /// <summary>
    /// Approve a flow instance
    /// </summary>
    [HttpPost("flows/{flowInstanceId:guid}/approve")]
    [Authorize]
    [ProducesResponseType(typeof(AuthorizationFlowResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<AuthorizationFlowResponse>> ApproveFlow(
        Guid flowInstanceId,
        [FromBody] string? comment = null)
    {
        try
        {
            var userId = Guid.Parse(User.Identity?.Name ?? Guid.Empty.ToString());
            var result = await _flowService.ApproveAsync(flowInstanceId, userId, comment ?? string.Empty);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving flow {FlowInstanceId}", flowInstanceId);
            return StatusCode(500, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Flow approval error",
                Detail = ex.Message
            });
        }
    }

    /// <summary>
    /// Deny a flow instance
    /// </summary>
    [HttpPost("flows/{flowInstanceId:guid}/deny")]
    [Authorize]
    [ProducesResponseType(typeof(AuthorizationFlowResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<AuthorizationFlowResponse>> DenyFlow(
        Guid flowInstanceId,
        [FromBody] string? comment = null)
    {
        try
        {
            var userId = Guid.Parse(User.Identity?.Name ?? Guid.Empty.ToString());
            var result = await _flowService.DenyAsync(flowInstanceId, userId, comment ?? string.Empty);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error denying flow {FlowInstanceId}", flowInstanceId);
            return StatusCode(500, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Flow denial error",
                Detail = ex.Message
            });
        }
    }

    /// <summary>
    /// Get flow instance status
    /// </summary>
    [HttpGet("flows/{flowInstanceId:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(AuthorizationFlowResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<AuthorizationFlowResponse>> GetFlowStatus(Guid flowInstanceId)
    {
        try
        {
            var result = await _flowService.GetFlowStatusAsync(flowInstanceId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting flow status {FlowInstanceId}", flowInstanceId);
            return StatusCode(500, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Error retrieving flow status",
                Detail = ex.Message
            });
        }
    }

    /// <summary>
    /// Get pending approvals for current user
    /// </summary>
    [HttpGet("flows/pending")]
    [Authorize]
    [ProducesResponseType(typeof(List<AuthorizationFlowResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<AuthorizationFlowResponse>>> GetPendingApprovals()
    {
        try
        {
            var userId = Guid.Parse(User.Identity?.Name ?? Guid.Empty.ToString());
            var result = await _flowService.GetPendingApprovalsAsync(userId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pending approvals");
            return StatusCode(500, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Error retrieving pending approvals",
                Detail = ex.Message
            });
        }
    }

    /// <summary>
    /// Cancel a flow instance
    /// </summary>
    [HttpPost("flows/{flowInstanceId:guid}/cancel")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelFlow(Guid flowInstanceId)
    {
        try
        {
            var userId = Guid.Parse(User.Identity?.Name ?? Guid.Empty.ToString());
            var cancelled = await _flowService.CancelFlowAsync(flowInstanceId, userId);

            if (!cancelled)
            {
                return NotFound(new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Flow not found or cannot be cancelled"
                });
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling flow {FlowInstanceId}", flowInstanceId);
            return StatusCode(500, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Flow cancellation error",
                Detail = ex.Message
            });
        }
    }

    #endregion

    #region Batch Authorization Endpoints

    /// <summary>
    /// Perform batch authorization checks
    /// </summary>
    [HttpPost("check-batch")]
    [Authorize]
    [ProducesResponseType(typeof(List<BatchAuthorizationResult>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<BatchAuthorizationResult>>> CheckBatchAuthorization(
        [FromBody] List<BatchAuthorizationRequest> requests)
    {
        try
        {
            var results = new List<BatchAuthorizationResult>();

            foreach (var request in requests)
            {
                var abacRequest = new AbacEvaluationRequest
                {
                    SubjectId = request.UserId,
                    Action = request.Action,
                    ResourceType = request.ResourceType,
                    ResourceId = request.ResourceId,
                    Context = request.Context
                };

                var evaluation = await _abacEngine.EvaluateAsync(abacRequest);

                results.Add(new BatchAuthorizationResult
                {
                    RequestId = request.RequestId,
                    Allowed = evaluation.Allowed,
                    Decision = evaluation.Decision,
                    Reasons = evaluation.Reasons
                });
            }

            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing batch authorization");
            return StatusCode(500, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Batch authorization error",
                Detail = ex.Message
            });
        }
    }

    #endregion

    #region Column Security Endpoints

    /// <summary>
    /// Check column-level access
    /// </summary>
    [HttpPost("column-access/check")]
    [Authorize]
    [ProducesResponseType(typeof(ColumnAccessResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ColumnAccessResponse>> CheckColumnAccess([FromBody] ColumnAccessRequest request)
    {
        try
        {
            var result = await _columnSecurityEngine.CheckColumnAccessAsync(request);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking column access");
            return StatusCode(500, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Column access check error",
                Detail = ex.Message
            });
        }
    }

    /// <summary>
    /// Apply data masking to result set
    /// </summary>
    [HttpPost("column-access/mask")]
    [Authorize]
    [ProducesResponseType(typeof(Dictionary<string, object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<Dictionary<string, object>>> ApplyMasking(
        [FromQuery] Guid userId,
        [FromQuery] string tableName,
        [FromBody] Dictionary<string, object> data)
    {
        try
        {
            var result = await _columnSecurityEngine.ApplyMaskingAsync(userId, tableName, data);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying data masking");
            return StatusCode(500, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Data masking error",
                Detail = ex.Message
            });
        }
    }

    #endregion

    #region Context Evaluation Endpoints

    /// <summary>
    /// Evaluate context-based access decision
    /// </summary>
    [HttpPost("context/evaluate")]
    [Authorize]
    [ProducesResponseType(typeof(ContextEvaluationResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ContextEvaluationResponse>> EvaluateContext([FromBody] ContextEvaluationRequest request)
    {
        try
        {
            var result = await _contextEvaluator.EvaluateContextAsync(request);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating context");
            return StatusCode(500, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Context evaluation error",
                Detail = ex.Message
            });
        }
    }

    /// <summary>
    /// Calculate access risk score
    /// </summary>
    [HttpPost("context/risk-score")]
    [Authorize]
    [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
    public async Task<ActionResult<int>> CalculateRiskScore([FromBody] ContextEvaluationRequest request)
    {
        try
        {
            var riskScore = await _contextEvaluator.CalculateAccessRiskScoreAsync(request);
            return Ok(riskScore);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating risk score");
            return StatusCode(500, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Risk score calculation error",
                Detail = ex.Message
            });
        }
    }

    #endregion

    #region Policy Conflict Detection

    /// <summary>
    /// Detect conflicts in policies
    /// </summary>
    [HttpGet("policies/{id:guid}/conflicts")]
    [Authorize]
    [ProducesResponseType(typeof(List<PolicyConflict>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<PolicyConflict>>> DetectPolicyConflicts(Guid id)
    {
        try
        {
            var policy = await _context.AccessPolicies.FindAsync(id);

            if (policy == null)
            {
                return NotFound(new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Policy not found"
                });
            }

            var allPolicies = await _context.AccessPolicies
                .Where(p => p.IsActive && p.Id != id)
                .ToListAsync();

            var conflicts = DetectConflicts(policy, allPolicies);

            return Ok(conflicts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting policy conflicts");
            return StatusCode(500, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Conflict detection error",
                Detail = ex.Message
            });
        }
    }

    #endregion

    #region Private Helper Methods

    private List<PolicyConflict> DetectConflicts(Core.Models.Entities.AccessPolicy policy, List<Core.Models.Entities.AccessPolicy> otherPolicies)
    {
        var conflicts = new List<PolicyConflict>();

        if (policy.PolicyType != "ABAC")
        {
            return conflicts; // Only check ABAC policies for now
        }

        try
        {
            var policyDoc = System.Text.Json.JsonDocument.Parse(policy.Policy);
            var rules = policyDoc.RootElement.GetProperty("rules");

            foreach (var otherPolicy in otherPolicies.Where(p => p.PolicyType == "ABAC"))
            {
                try
                {
                    var otherDoc = System.Text.Json.JsonDocument.Parse(otherPolicy.Policy);
                    var otherRules = otherDoc.RootElement.GetProperty("rules");

                    // Check for overlapping rules with different effects
                    foreach (var rule in rules.EnumerateArray())
                    {
                        var action = rule.TryGetProperty("action", out var actionProp) ? actionProp.GetString() : "*";
                        var resource = rule.TryGetProperty("resource", out var resProp) ? resProp.GetString() : "*";
                        var effect = rule.TryGetProperty("effect", out var effProp) ? effProp.GetString() : "allow";

                        foreach (var otherRule in otherRules.EnumerateArray())
                        {
                            var otherAction = otherRule.TryGetProperty("action", out var otherActionProp) ? otherActionProp.GetString() : "*";
                            var otherResource = otherRule.TryGetProperty("resource", out var otherResProp) ? otherResProp.GetString() : "*";
                            var otherEffect = otherRule.TryGetProperty("effect", out var otherEffProp) ? otherEffProp.GetString() : "allow";

                            // Check for overlap
                            if ((action == otherAction || action == "*" || otherAction == "*") &&
                                (resource == otherResource || resource == "*" || otherResource == "*") &&
                                effect != otherEffect)
                            {
                                conflicts.Add(new PolicyConflict
                                {
                                    ConflictingPolicyId = otherPolicy.Id,
                                    ConflictingPolicyName = otherPolicy.Name,
                                    ConflictType = "effect_mismatch",
                                    Description = $"Policy '{policy.Name}' has effect '{effect}' while '{otherPolicy.Name}' has effect '{otherEffect}' for similar conditions",
                                    Severity = "medium",
                                    Resource = resource ?? "*",
                                    Action = action ?? "*"
                                });
                            }
                        }
                    }
                }
                catch
                {
                    // Skip policies that can't be parsed
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error parsing policy for conflict detection");
        }

        return conflicts;
    }

    #endregion
}

/// <summary>
/// Batch authorization request
/// </summary>
public class BatchAuthorizationRequest
{
    public string RequestId { get; set; } = Guid.NewGuid().ToString();
    public Guid UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public string? ResourceId { get; set; }
    public Dictionary<string, object>? Context { get; set; }
}

/// <summary>
/// Batch authorization result
/// </summary>
public class BatchAuthorizationResult
{
    public string RequestId { get; set; } = string.Empty;
    public bool Allowed { get; set; }
    public string Decision { get; set; } = string.Empty;
    public List<string> Reasons { get; set; } = new();
}

/// <summary>
/// Policy conflict detection result
/// </summary>
public class PolicyConflict
{
    public Guid ConflictingPolicyId { get; set; }
    public string ConflictingPolicyName { get; set; } = string.Empty;
    public string ConflictType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Severity { get; set; } = "low"; // low, medium, high
    public string? Resource { get; set; }
    public string? Action { get; set; }
}
