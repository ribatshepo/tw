using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using USP.Core.Models.DTOs.Webhook;
using USP.Core.Services.Webhook;

namespace USP.Api.Controllers.Webhook;

/// <summary>
/// Controller for webhook management and event delivery
/// </summary>
[ApiController]
[Route("api/v1/[controller]s")]
[Authorize]
public class WebhookController : ControllerBase
{
    private readonly IWebhookService _webhookService;
    private readonly ILogger<WebhookController> _logger;

    public WebhookController(
        IWebhookService webhookService,
        ILogger<WebhookController> logger)
    {
        _webhookService = webhookService;
        _logger = logger;
    }

    /// <summary>
    /// Create a new webhook
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(WebhookDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<WebhookDto>> Create([FromBody] CreateWebhookRequest request)
    {
        try
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Unauthorized(new { error = "User ID not found in token" });

            var webhook = await _webhookService.CreateWebhookAsync(userId, request);

            return CreatedAtAction(nameof(GetById), new { id = webhook.Id }, webhook);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating webhook");
            return StatusCode(500, new { error = "Failed to create webhook" });
        }
    }

    /// <summary>
    /// Get all webhooks for the current user
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(WebhooksResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<WebhooksResponse>> GetAll([FromQuery] bool? activeOnly = null)
    {
        try
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Unauthorized(new { error = "User ID not found in token" });

            var webhooks = await _webhookService.GetWebhooksAsync(userId, activeOnly);

            var response = new WebhooksResponse
            {
                Webhooks = webhooks,
                TotalCount = webhooks.Count
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving webhooks");
            return StatusCode(500, new { error = "Failed to retrieve webhooks" });
        }
    }

    /// <summary>
    /// Get webhook by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(WebhookDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WebhookDto>> GetById(Guid id)
    {
        try
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Unauthorized(new { error = "User ID not found in token" });

            var webhook = await _webhookService.GetWebhookByIdAsync(id, userId);

            if (webhook == null)
                return NotFound(new { error = "Webhook not found" });

            return Ok(webhook);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving webhook {WebhookId}", id);
            return StatusCode(500, new { error = "Failed to retrieve webhook" });
        }
    }

    /// <summary>
    /// Update webhook
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Update(Guid id, [FromBody] UpdateWebhookRequest request)
    {
        try
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Unauthorized(new { error = "User ID not found in token" });

            var success = await _webhookService.UpdateWebhookAsync(id, userId, request);

            if (!success)
                return NotFound(new { error = "Webhook not found" });

            return Ok(new { message = "Webhook updated successfully" });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating webhook {WebhookId}", id);
            return StatusCode(500, new { error = "Failed to update webhook" });
        }
    }

    /// <summary>
    /// Delete webhook
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Delete(Guid id)
    {
        try
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Unauthorized(new { error = "User ID not found in token" });

            var success = await _webhookService.DeleteWebhookAsync(id, userId);

            if (!success)
                return NotFound(new { error = "Webhook not found" });

            return Ok(new { message = "Webhook deleted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting webhook {WebhookId}", id);
            return StatusCode(500, new { error = "Failed to delete webhook" });
        }
    }

    /// <summary>
    /// Test webhook with custom payload
    /// </summary>
    [HttpPost("{id:guid}/test")]
    [ProducesResponseType(typeof(WebhookDeliveryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WebhookDeliveryDto>> Test(Guid id, [FromBody] TestWebhookRequest request)
    {
        try
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Unauthorized(new { error = "User ID not found in token" });

            var delivery = await _webhookService.TestWebhookAsync(id, userId, request);

            return Ok(delivery);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing webhook {WebhookId}", id);
            return StatusCode(500, new { error = "Failed to test webhook" });
        }
    }

    /// <summary>
    /// Get webhook deliveries
    /// </summary>
    [HttpGet("{id:guid}/deliveries")]
    [ProducesResponseType(typeof(WebhookDeliveriesResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<WebhookDeliveriesResponse>> GetDeliveries(
        Guid id,
        [FromQuery] string? eventType = null,
        [FromQuery] string? status = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Unauthorized(new { error = "User ID not found in token" });

            var request = new WebhookDeliveryFilterRequest
            {
                WebhookId = id,
                EventType = eventType,
                Status = status,
                StartDate = startDate,
                EndDate = endDate,
                Page = page,
                PageSize = pageSize
            };

            var (deliveries, totalCount) = await _webhookService.GetDeliveriesAsync(request, userId);

            var response = new WebhookDeliveriesResponse
            {
                Deliveries = deliveries,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving webhook deliveries");
            return StatusCode(500, new { error = "Failed to retrieve deliveries" });
        }
    }

    /// <summary>
    /// Redeliver a failed webhook delivery
    /// </summary>
    [HttpPost("{id:guid}/deliveries/{deliveryId:guid}/redeliver")]
    [ProducesResponseType(typeof(WebhookDeliveryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WebhookDeliveryDto>> Redeliver(Guid id, Guid deliveryId)
    {
        try
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Unauthorized(new { error = "User ID not found in token" });

            var delivery = await _webhookService.RedeliverAsync(id, deliveryId, userId);

            if (delivery == null)
                return NotFound(new { error = "Webhook or delivery not found" });

            return Ok(delivery);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error redelivering webhook {WebhookId} delivery {DeliveryId}",
                id, deliveryId);
            return StatusCode(500, new { error = "Failed to redeliver webhook" });
        }
    }

    /// <summary>
    /// Reset circuit breaker for a webhook
    /// </summary>
    [HttpPost("{id:guid}/reset-circuit-breaker")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> ResetCircuitBreaker(Guid id)
    {
        try
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Unauthorized(new { error = "User ID not found in token" });

            var success = await _webhookService.ResetCircuitBreakerAsync(id, userId);

            if (!success)
                return NotFound(new { error = "Webhook not found" });

            return Ok(new { message = "Circuit breaker reset successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting circuit breaker for webhook {WebhookId}", id);
            return StatusCode(500, new { error = "Failed to reset circuit breaker" });
        }
    }

    /// <summary>
    /// Get available webhook events
    /// </summary>
    [HttpGet("events")]
    [ProducesResponseType(typeof(WebhookEventsResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<WebhookEventsResponse>> GetAvailableEvents()
    {
        try
        {
            var events = await _webhookService.GetAvailableEventsAsync();

            var response = new WebhookEventsResponse
            {
                Events = events,
                TotalCount = events.Count
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving available events");
            return StatusCode(500, new { error = "Failed to retrieve events" });
        }
    }

    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
    }
}

// Response DTOs
public class WebhooksResponse
{
    public List<WebhookDto> Webhooks { get; set; } = new();
    public int TotalCount { get; set; }
}

public class WebhookDeliveriesResponse
{
    public List<WebhookDeliveryDto> Deliveries { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}

public class WebhookEventsResponse
{
    public List<string> Events { get; set; } = new();
    public int TotalCount { get; set; }
}
