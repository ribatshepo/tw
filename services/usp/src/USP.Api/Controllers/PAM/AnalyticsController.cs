using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using USP.Api.Extensions;
using USP.Core.Models.DTOs.PAM;
using USP.Core.Services.PAM;

namespace USP.Api.Controllers.PAM;

/// <summary>
/// Controller for privileged access analytics and risk management
/// </summary>
[ApiController]
[Route("api/pam/analytics")]
[Authorize]
public class AnalyticsController : ControllerBase
{
    private readonly IAccessAnalyticsEngine _analyticsEngine;
    private readonly ISessionRecordingService _sessionService;
    private readonly ILogger<AnalyticsController> _logger;

    public AnalyticsController(
        IAccessAnalyticsEngine analyticsEngine,
        ISessionRecordingService sessionService,
        ILogger<AnalyticsController> logger)
    {
        _analyticsEngine = analyticsEngine;
        _sessionService = sessionService;
        _logger = logger;
    }

    /// <summary>
    /// Get dormant privileged accounts
    /// </summary>
    [HttpGet("dormant")]
    [ProducesResponseType(typeof(List<DormantAccountDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<DormantAccountDto>>> GetDormantAccounts([FromQuery] int dormantDays = 90)
    {
        try
        {
            var userId = User.GetUserId();
            var dormantAccounts = await _analyticsEngine.DetectDormantAccountsAsync(userId, dormantDays);

            _logger.LogInformation(
                "User {UserId} retrieved {Count} dormant accounts",
                userId,
                dormantAccounts.Count);

            return Ok(dormantAccounts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving dormant accounts");
            return StatusCode(500, "An error occurred while retrieving dormant accounts");
        }
    }

    /// <summary>
    /// Get over-privileged accounts
    /// </summary>
    [HttpGet("over-privileged")]
    [ProducesResponseType(typeof(List<OverPrivilegedAccountDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<OverPrivilegedAccountDto>>> GetOverPrivilegedAccounts()
    {
        try
        {
            var userId = User.GetUserId();
            var overPrivilegedAccounts = await _analyticsEngine.DetectOverPrivilegedAccountsAsync(userId);

            _logger.LogInformation(
                "User {UserId} retrieved {Count} over-privileged accounts",
                userId,
                overPrivilegedAccounts.Count);

            return Ok(overPrivilegedAccounts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving over-privileged accounts");
            return StatusCode(500, "An error occurred while retrieving over-privileged accounts");
        }
    }

    /// <summary>
    /// Analyze usage pattern for an account
    /// </summary>
    [HttpGet("accounts/{accountId}/usage")]
    [ProducesResponseType(typeof(AccountUsagePatternDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<AccountUsagePatternDto>> GetAccountUsagePattern(
        Guid accountId,
        [FromQuery] int daysToAnalyze = 30)
    {
        try
        {
            var userId = User.GetUserId();
            var usagePattern = await _analyticsEngine.AnalyzeAccountUsageAsync(accountId, userId, daysToAnalyze);

            _logger.LogInformation(
                "User {UserId} retrieved usage pattern for account {AccountId}",
                userId,
                accountId);

            return Ok(usagePattern);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation for account {AccountId}", accountId);
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving usage pattern for account {AccountId}", accountId);
            return StatusCode(500, "An error occurred while retrieving usage pattern");
        }
    }

    /// <summary>
    /// Get detected access anomalies
    /// </summary>
    [HttpGet("anomalies")]
    [ProducesResponseType(typeof(List<AccessAnomalyDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<AccessAnomalyDto>>> GetAccessAnomalies()
    {
        try
        {
            var userId = User.GetUserId();
            var anomalies = await _analyticsEngine.DetectAccessAnomaliesAsync(userId);

            _logger.LogInformation(
                "User {UserId} retrieved {Count} access anomalies",
                userId,
                anomalies.Count);

            return Ok(anomalies);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving access anomalies");
            return StatusCode(500, "An error occurred while retrieving access anomalies");
        }
    }

    /// <summary>
    /// Get compliance dashboard
    /// </summary>
    [HttpGet("compliance")]
    [ProducesResponseType(typeof(ComplianceDashboardDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ComplianceDashboardDto>> GetComplianceDashboard()
    {
        try
        {
            var userId = User.GetUserId();
            var dashboard = await _analyticsEngine.GetComplianceDashboardAsync(userId);

            _logger.LogInformation(
                "User {UserId} retrieved compliance dashboard (score: {Score:F1}%)",
                userId,
                dashboard.ComplianceScore);

            return Ok(dashboard);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving compliance dashboard");
            return StatusCode(500, "An error occurred while retrieving compliance dashboard");
        }
    }

    /// <summary>
    /// Calculate risk score for an account
    /// </summary>
    [HttpGet("accounts/{accountId}/risk-score")]
    [ProducesResponseType(typeof(AccountRiskScoreDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<AccountRiskScoreDto>> GetAccountRiskScore(Guid accountId)
    {
        try
        {
            var userId = User.GetUserId();
            var riskScore = await _analyticsEngine.CalculateAccountRiskScoreAsync(accountId, userId);

            _logger.LogInformation(
                "User {UserId} retrieved risk score for account {AccountId}: {Score}",
                userId,
                accountId,
                riskScore.TotalRiskScore);

            return Ok(riskScore);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation for account {AccountId}", accountId);
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating risk score for account {AccountId}", accountId);
            return StatusCode(500, "An error occurred while calculating risk score");
        }
    }

    /// <summary>
    /// Get high-risk accounts
    /// </summary>
    [HttpGet("high-risk")]
    [ProducesResponseType(typeof(List<AccountRiskScoreDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<AccountRiskScoreDto>>> GetHighRiskAccounts([FromQuery] int threshold = 70)
    {
        try
        {
            var userId = User.GetUserId();
            var highRiskAccounts = await _analyticsEngine.GetHighRiskAccountsAsync(userId, threshold);

            _logger.LogInformation(
                "User {UserId} retrieved {Count} high-risk accounts (threshold: {Threshold})",
                userId,
                highRiskAccounts.Count,
                threshold);

            return Ok(highRiskAccounts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving high-risk accounts");
            return StatusCode(500, "An error occurred while retrieving high-risk accounts");
        }
    }

    /// <summary>
    /// Get checkout policy violations
    /// </summary>
    [HttpGet("policy-violations")]
    [ProducesResponseType(typeof(List<CheckoutPolicyViolationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<CheckoutPolicyViolationDto>>> GetPolicyViolations()
    {
        try
        {
            var userId = User.GetUserId();
            var violations = await _analyticsEngine.DetectCheckoutPolicyViolationsAsync(userId);

            _logger.LogInformation(
                "User {UserId} retrieved {Count} policy violations",
                userId,
                violations.Count);

            return Ok(violations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving policy violations");
            return StatusCode(500, "An error occurred while retrieving policy violations");
        }
    }

    /// <summary>
    /// Get analytics summary
    /// </summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(AccessAnalyticsSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<AccessAnalyticsSummaryDto>> GetAnalyticsSummary()
    {
        try
        {
            var userId = User.GetUserId();
            var summary = await _analyticsEngine.GetAnalyticsSummaryAsync(userId);

            _logger.LogInformation(
                "User {UserId} retrieved analytics summary",
                userId);

            return Ok(summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving analytics summary");
            return StatusCode(500, "An error occurred while retrieving analytics summary");
        }
    }

    /// <summary>
    /// Get live session monitoring data
    /// </summary>
    [HttpGet("sessions/{sessionId}/live")]
    [ProducesResponseType(typeof(SessionRecordingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<SessionRecordingDto>> GetLiveSession(Guid sessionId)
    {
        try
        {
            var userId = User.GetUserId();
            var session = await _sessionService.GetSessionByIdAsync(sessionId, userId);

            if (session == null)
                return NotFound("Session not found or access denied");

            _logger.LogInformation(
                "User {UserId} accessed live session {SessionId}",
                userId,
                sessionId);

            return Ok(session);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving live session {SessionId}", sessionId);
            return StatusCode(500, "An error occurred while retrieving live session");
        }
    }

    /// <summary>
    /// Terminate a session (admin operation)
    /// </summary>
    [HttpPost("sessions/{sessionId}/terminate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> TerminateSession(Guid sessionId, [FromBody] TerminateSessionRequest request)
    {
        try
        {
            var userId = User.GetUserId();

            if (string.IsNullOrWhiteSpace(request.Reason))
                return BadRequest("Termination reason is required");

            var success = await _sessionService.TerminateSessionAsync(sessionId, userId, request.Reason);

            if (!success)
                return NotFound("Session not found or already terminated");

            _logger.LogWarning(
                "User {UserId} terminated session {SessionId}: {Reason}",
                userId,
                sessionId,
                request.Reason);

            return Ok(new { message = "Session terminated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error terminating session {SessionId}", sessionId);
            return StatusCode(500, "An error occurred while terminating session");
        }
    }

    /// <summary>
    /// Get session commands (command history)
    /// </summary>
    [HttpGet("sessions/{sessionId}/commands")]
    [ProducesResponseType(typeof(List<SessionCommandDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<SessionCommandDto>>> GetSessionCommands(
        Guid sessionId,
        [FromQuery] int? limit = 100)
    {
        try
        {
            var userId = User.GetUserId();
            var commands = await _sessionService.GetSessionCommandsAsync(sessionId, userId, limit);

            _logger.LogInformation(
                "User {UserId} retrieved {Count} commands for session {SessionId}",
                userId,
                commands.Count,
                sessionId);

            return Ok(commands);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving commands for session {SessionId}", sessionId);
            return StatusCode(500, "An error occurred while retrieving session commands");
        }
    }
}

public class TerminateSessionRequest
{
    public string Reason { get; set; } = string.Empty;
}
