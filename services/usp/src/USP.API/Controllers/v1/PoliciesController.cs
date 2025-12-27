using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using USP.Core.Domain.Entities.Security;
using USP.Core.Domain.Enums;
using USP.Infrastructure.Persistence;

namespace USP.API.Controllers.v1;

/// <summary>
/// Policy management endpoints for RBAC, ABAC, and HCL policies
/// </summary>
[ApiController]
[Route("api/v1/policies")]
[Authorize]
[Produces("application/json")]
public class PoliciesController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<PoliciesController> _logger;

    public PoliciesController(
        ApplicationDbContext context,
        ILogger<PoliciesController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Get all policies
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PoliciesListResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPolicies(
        [FromQuery] PolicyType? type = null,
        [FromQuery] bool? isActive = null)
    {
        try
        {
            var query = _context.Policies
                .Where(p => p.DeletedAt == null);

            if (type.HasValue)
            {
                query = query.Where(p => p.Type == type.Value);
            }

            if (isActive.HasValue)
            {
                query = query.Where(p => p.IsActive == isActive.Value);
            }

            var policies = await query
                .OrderByDescending(p => p.Priority)
                .ThenBy(p => p.Name)
                .Select(p => new PolicyInfo
                {
                    Id = p.Id,
                    Name = p.Name,
                    Description = p.Description,
                    Type = p.Type.ToString(),
                    Effect = p.Effect,
                    Priority = p.Priority,
                    IsActive = p.IsActive,
                    IsSystemPolicy = p.IsSystemPolicy,
                    Version = p.Version,
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt
                })
                .ToListAsync();

            return Ok(new PoliciesListResponse
            {
                Policies = policies
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting policies");
            return StatusCode(500, new { error = "Failed to get policies" });
        }
    }

    /// <summary>
    /// Get a specific policy by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(PolicyDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPolicy(string id)
    {
        try
        {
            var policy = await _context.Policies
                .FirstOrDefaultAsync(p => p.Id == id && p.DeletedAt == null);

            if (policy == null)
            {
                return NotFound(new { error = "Policy not found" });
            }

            return Ok(new PolicyDetailResponse
            {
                Id = policy.Id,
                Name = policy.Name,
                Description = policy.Description,
                Type = policy.Type.ToString(),
                Content = policy.Content,
                Effect = policy.Effect,
                Priority = policy.Priority,
                IsActive = policy.IsActive,
                IsSystemPolicy = policy.IsSystemPolicy,
                Version = policy.Version,
                Metadata = policy.Metadata,
                CreatedAt = policy.CreatedAt,
                CreatedBy = policy.CreatedBy,
                UpdatedAt = policy.UpdatedAt,
                UpdatedBy = policy.UpdatedBy
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting policy {PolicyId}", id);
            return StatusCode(500, new { error = "Failed to get policy" });
        }
    }

    /// <summary>
    /// Create an ABAC (Attribute-Based Access Control) policy
    /// </summary>
    [HttpPost("abac")]
    [ProducesResponseType(typeof(PolicyDetailResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateABACPolicy([FromBody] CreateABACPolicyRequest request)
    {
        try
        {
            var policy = new Policy
            {
                Name = request.Name,
                Description = request.Description,
                Type = PolicyType.ABAC,
                Content = request.Content, // JSON content with subject/resource/action/conditions
                Effect = request.Effect ?? "allow",
                Priority = request.Priority ?? 0,
                IsActive = request.IsActive ?? true,
                IsSystemPolicy = false,
                Version = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Policies.Add(policy);
            await _context.SaveChangesAsync();

            _logger.LogInformation("ABAC policy created: {PolicyId} - {PolicyName}", policy.Id, policy.Name);

            return CreatedAtAction(
                nameof(GetPolicy),
                new { id = policy.Id },
                new PolicyDetailResponse
                {
                    Id = policy.Id,
                    Name = policy.Name,
                    Description = policy.Description,
                    Type = policy.Type.ToString(),
                    Content = policy.Content,
                    Effect = policy.Effect,
                    Priority = policy.Priority,
                    IsActive = policy.IsActive,
                    IsSystemPolicy = policy.IsSystemPolicy,
                    Version = policy.Version,
                    Metadata = policy.Metadata,
                    CreatedAt = policy.CreatedAt,
                    CreatedBy = policy.CreatedBy,
                    UpdatedAt = policy.UpdatedAt,
                    UpdatedBy = policy.UpdatedBy
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating ABAC policy");
            return StatusCode(500, new { error = "Failed to create ABAC policy" });
        }
    }

    /// <summary>
    /// Create an HCL (HashiCorp Configuration Language) policy
    /// </summary>
    [HttpPost("hcl")]
    [ProducesResponseType(typeof(PolicyDetailResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateHCLPolicy([FromBody] CreateHCLPolicyRequest request)
    {
        try
        {
            var policy = new Policy
            {
                Name = request.Name,
                Description = request.Description,
                Type = PolicyType.HCL,
                Content = request.Content, // HCL policy content (Vault-compatible syntax)
                Effect = request.Effect ?? "allow",
                Priority = request.Priority ?? 0,
                IsActive = request.IsActive ?? true,
                IsSystemPolicy = false,
                Version = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Policies.Add(policy);
            await _context.SaveChangesAsync();

            _logger.LogInformation("HCL policy created: {PolicyId} - {PolicyName}", policy.Id, policy.Name);

            return CreatedAtAction(
                nameof(GetPolicy),
                new { id = policy.Id },
                new PolicyDetailResponse
                {
                    Id = policy.Id,
                    Name = policy.Name,
                    Description = policy.Description,
                    Type = policy.Type.ToString(),
                    Content = policy.Content,
                    Effect = policy.Effect,
                    Priority = policy.Priority,
                    IsActive = policy.IsActive,
                    IsSystemPolicy = policy.IsSystemPolicy,
                    Version = policy.Version,
                    Metadata = policy.Metadata,
                    CreatedAt = policy.CreatedAt,
                    CreatedBy = policy.CreatedBy,
                    UpdatedAt = policy.UpdatedAt,
                    UpdatedBy = policy.UpdatedBy
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating HCL policy");
            return StatusCode(500, new { error = "Failed to create HCL policy" });
        }
    }

    /// <summary>
    /// Update an existing policy
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(PolicyDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdatePolicy(string id, [FromBody] UpdatePolicyRequest request)
    {
        try
        {
            var policy = await _context.Policies
                .FirstOrDefaultAsync(p => p.Id == id && p.DeletedAt == null);

            if (policy == null)
            {
                return NotFound(new { error = "Policy not found" });
            }

            if (policy.IsSystemPolicy)
            {
                return BadRequest(new { error = "Cannot modify system policies" });
            }

            if (!string.IsNullOrEmpty(request.Name))
            {
                policy.Name = request.Name;
            }

            if (request.Description != null)
            {
                policy.Description = request.Description;
            }

            if (request.Content != null)
            {
                policy.Content = request.Content;
                policy.Version++; // Increment version on content change
            }

            if (request.Effect != null)
            {
                policy.Effect = request.Effect;
            }

            if (request.Priority.HasValue)
            {
                policy.Priority = request.Priority.Value;
            }

            if (request.IsActive.HasValue)
            {
                policy.IsActive = request.IsActive.Value;
            }

            policy.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Policy updated: {PolicyId} - {PolicyName}", policy.Id, policy.Name);

            return Ok(new PolicyDetailResponse
            {
                Id = policy.Id,
                Name = policy.Name,
                Description = policy.Description,
                Type = policy.Type.ToString(),
                Content = policy.Content,
                Effect = policy.Effect,
                Priority = policy.Priority,
                IsActive = policy.IsActive,
                IsSystemPolicy = policy.IsSystemPolicy,
                Version = policy.Version,
                Metadata = policy.Metadata,
                CreatedAt = policy.CreatedAt,
                CreatedBy = policy.CreatedBy,
                UpdatedAt = policy.UpdatedAt,
                UpdatedBy = policy.UpdatedBy
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating policy {PolicyId}", id);
            return StatusCode(500, new { error = "Failed to update policy" });
        }
    }

    /// <summary>
    /// Delete a policy (soft delete)
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeletePolicy(string id)
    {
        try
        {
            var policy = await _context.Policies
                .FirstOrDefaultAsync(p => p.Id == id && p.DeletedAt == null);

            if (policy == null)
            {
                return NotFound(new { error = "Policy not found" });
            }

            if (policy.IsSystemPolicy)
            {
                return BadRequest(new { error = "Cannot delete system policies" });
            }

            // Soft delete
            policy.DeletedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Policy deleted: {PolicyId} - {PolicyName}", policy.Id, policy.Name);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting policy {PolicyId}", id);
            return StatusCode(500, new { error = "Failed to delete policy" });
        }
    }

    /// <summary>
    /// Activate a policy
    /// </summary>
    [HttpPost("{id}/activate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ActivatePolicy(string id)
    {
        try
        {
            var policy = await _context.Policies
                .FirstOrDefaultAsync(p => p.Id == id && p.DeletedAt == null);

            if (policy == null)
            {
                return NotFound(new { error = "Policy not found" });
            }

            policy.IsActive = true;
            policy.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Policy activated: {PolicyId}", id);

            return Ok(new { message = "Policy activated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error activating policy {PolicyId}", id);
            return StatusCode(500, new { error = "Failed to activate policy" });
        }
    }

    /// <summary>
    /// Deactivate a policy
    /// </summary>
    [HttpPost("{id}/deactivate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeactivatePolicy(string id)
    {
        try
        {
            var policy = await _context.Policies
                .FirstOrDefaultAsync(p => p.Id == id && p.DeletedAt == null);

            if (policy == null)
            {
                return NotFound(new { error = "Policy not found" });
            }

            policy.IsActive = false;
            policy.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Policy deactivated: {PolicyId}", id);

            return Ok(new { message = "Policy deactivated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deactivating policy {PolicyId}", id);
            return StatusCode(500, new { error = "Failed to deactivate policy" });
        }
    }
}

// DTOs

public class PoliciesListResponse
{
    public required List<PolicyInfo> Policies { get; set; }
}

public class PolicyInfo
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public required string Type { get; set; }
    public required string Effect { get; set; }
    public int Priority { get; set; }
    public bool IsActive { get; set; }
    public bool IsSystemPolicy { get; set; }
    public int Version { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class PolicyDetailResponse
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public required string Type { get; set; }
    public string? Content { get; set; }
    public required string Effect { get; set; }
    public int Priority { get; set; }
    public bool IsActive { get; set; }
    public bool IsSystemPolicy { get; set; }
    public int Version { get; set; }
    public string? Metadata { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

public class CreateABACPolicyRequest
{
    public required string Name { get; set; }
    public string? Description { get; set; }
    public required string Content { get; set; } // JSON content
    public string? Effect { get; set; } // "allow" or "deny"
    public int? Priority { get; set; }
    public bool? IsActive { get; set; }
}

public class CreateHCLPolicyRequest
{
    public required string Name { get; set; }
    public string? Description { get; set; }
    public required string Content { get; set; } // HCL content
    public string? Effect { get; set; } // "allow" or "deny"
    public int? Priority { get; set; }
    public bool? IsActive { get; set; }
}

public class UpdatePolicyRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Content { get; set; }
    public string? Effect { get; set; }
    public int? Priority { get; set; }
    public bool? IsActive { get; set; }
}
