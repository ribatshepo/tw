using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using USP.Core.Models.DTOs.Compliance;
using USP.Core.Services.Compliance;

namespace USP.Api.Controllers.Compliance;

/// <summary>
/// Controller for compliance framework management and automated reporting
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class ComplianceController : ControllerBase
{
    private readonly IComplianceEngine _complianceEngine;
    private readonly IComplianceAutomationService? _automationService;
    private readonly ILogger<ComplianceController> _logger;

    public ComplianceController(
        IComplianceEngine complianceEngine,
        ILogger<ComplianceController> logger,
        IComplianceAutomationService? automationService = null)
    {
        _complianceEngine = complianceEngine;
        _logger = logger;
        _automationService = automationService;
    }

    /// <summary>
    /// Generate compliance report for a specific framework
    /// </summary>
    [HttpPost("reports")]
    [ProducesResponseType(typeof(ComplianceReportDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ComplianceReportDto>> GenerateReport([FromBody] GenerateComplianceReportRequest request)
    {
        try
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Unauthorized(new { error = "User ID not found in token" });

            var report = await _complianceEngine.GenerateReportAsync(request, userId);

            return CreatedAtAction(nameof(GetReportById), new { id = report.Id }, report);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating compliance report for framework {Framework}", request.Framework);
            return StatusCode(500, new { error = "Failed to generate compliance report" });
        }
    }

    /// <summary>
    /// Get list of compliance reports with optional filtering
    /// </summary>
    [HttpGet("reports")]
    [ProducesResponseType(typeof(ComplianceReportsResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ComplianceReportsResponse>> GetReports(
        [FromQuery] string? framework = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            var reports = await _complianceEngine.GetReportsAsync(framework, page, pageSize);

            var response = new ComplianceReportsResponse
            {
                Reports = reports,
                Page = page,
                PageSize = pageSize,
                TotalCount = reports.Count
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving compliance reports");
            return StatusCode(500, new { error = "Failed to retrieve reports" });
        }
    }

    /// <summary>
    /// Get compliance report by ID
    /// </summary>
    [HttpGet("reports/{id:guid}")]
    [ProducesResponseType(typeof(ComplianceReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ComplianceReportDto>> GetReportById(Guid id)
    {
        try
        {
            var report = await _complianceEngine.GetReportByIdAsync(id);

            if (report == null)
                return NotFound(new { error = "Compliance report not found" });

            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving compliance report {ReportId}", id);
            return StatusCode(500, new { error = "Failed to retrieve report" });
        }
    }

    /// <summary>
    /// Download compliance report file
    /// </summary>
    [HttpGet("reports/{id:guid}/download")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadReport(Guid id)
    {
        try
        {
            var result = await _complianceEngine.DownloadReportAsync(id);

            if (result == null)
                return NotFound(new { error = "Report file not found" });

            return File(result.Value.FileData, result.Value.ContentType, result.Value.FileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading compliance report {ReportId}", id);
            return StatusCode(500, new { error = "Failed to download report" });
        }
    }

    /// <summary>
    /// Get current compliance status for a framework
    /// </summary>
    [HttpGet("status/{framework}")]
    [ProducesResponseType(typeof(ComplianceStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ComplianceStatusDto>> GetComplianceStatus(string framework)
    {
        try
        {
            var status = await _complianceEngine.GetComplianceStatusAsync(framework);
            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving compliance status for {Framework}", framework);
            return StatusCode(500, new { error = "Failed to retrieve compliance status" });
        }
    }

    /// <summary>
    /// Get all supported compliance frameworks
    /// </summary>
    [HttpGet("frameworks")]
    [ProducesResponseType(typeof(FrameworksResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<FrameworksResponse>> GetSupportedFrameworks()
    {
        try
        {
            var frameworks = await _complianceEngine.GetSupportedFrameworksAsync();

            var response = new FrameworksResponse
            {
                Frameworks = frameworks,
                Count = frameworks.Count
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving supported frameworks");
            return StatusCode(500, new { error = "Failed to retrieve frameworks" });
        }
    }

    /// <summary>
    /// Get control assessments for a framework
    /// </summary>
    [HttpGet("controls/{framework}")]
    [ProducesResponseType(typeof(ControlAssessmentsResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ControlAssessmentsResponse>> GetControlAssessments(string framework)
    {
        try
        {
            var assessments = await _complianceEngine.GetControlAssessmentsAsync(framework);

            var response = new ControlAssessmentsResponse
            {
                Framework = framework,
                Assessments = assessments,
                TotalControls = assessments.Count
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving control assessments for {Framework}", framework);
            return StatusCode(500, new { error = "Failed to retrieve control assessments" });
        }
    }

    /// <summary>
    /// Update control assessment
    /// </summary>
    [HttpPut("reports/{reportId:guid}/controls/{controlId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> UpdateControlAssessment(
        Guid reportId,
        string controlId,
        [FromBody] UpdateControlAssessmentRequest request)
    {
        try
        {
            var success = await _complianceEngine.UpdateControlAssessmentAsync(
                reportId,
                controlId,
                request.Status,
                request.Implementation,
                request.Evidence,
                request.Gaps);

            if (!success)
                return NotFound(new { error = "Control not found in report" });

            return Ok(new { message = "Control assessment updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating control assessment {ControlId} in report {ReportId}",
                controlId, reportId);
            return StatusCode(500, new { error = "Failed to update control assessment" });
        }
    }

    /// <summary>
    /// Get critical compliance gaps across all frameworks
    /// </summary>
    [HttpGet("gaps/critical")]
    [ProducesResponseType(typeof(CriticalGapsResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<CriticalGapsResponse>> GetCriticalGaps()
    {
        try
        {
            var gaps = await _complianceEngine.GetCriticalGapsAsync();

            var response = new CriticalGapsResponse
            {
                Frameworks = gaps,
                TotalFrameworks = gaps.Count,
                Message = gaps.Count > 0
                    ? "Critical compliance gaps detected"
                    : "No critical gaps detected"
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving critical compliance gaps");
            return StatusCode(500, new { error = "Failed to retrieve critical gaps" });
        }
    }

    /// <summary>
    /// Schedule automated compliance report generation
    /// </summary>
    [HttpPost("reports/schedule")]
    [Authorize(Roles = "admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> ScheduleAutomatedReport([FromBody] ScheduleReportRequest request)
    {
        try
        {
            var success = await _complianceEngine.ScheduleAutomatedReportAsync(
                request.Framework,
                request.Schedule,
                request.Format);

            if (!success)
                return BadRequest(new { error = "Failed to schedule automated report" });

            return Ok(new
            {
                message = $"Automated compliance report scheduled for {request.Framework}",
                schedule = request.Schedule,
                format = request.Format
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scheduling automated compliance report");
            return StatusCode(500, new { error = "Failed to schedule report" });
        }
    }

    // ============================================================================
    // Automated Compliance Verification Endpoints
    // ============================================================================

    /// <summary>
    /// Verify a specific compliance control (automated)
    /// </summary>
    [HttpPost("automation/verify")]
    [ProducesResponseType(typeof(ControlVerificationResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> VerifyControl([FromBody] VerifyControlRequestDto request)
    {
        if (_automationService == null)
            return StatusCode(503, new { error = "Compliance automation service not available" });

        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var userId = GetUserId();
            var result = await _automationService.VerifyControlAsync(request.ControlId, userId);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Verify all controls for a specific framework (automated)
    /// </summary>
    [HttpPost("automation/frameworks/{frameworkName}/verify")]
    [Authorize(Roles = "admin")]
    [ProducesResponseType(typeof(List<ControlVerificationResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> VerifyFramework(string frameworkName)
    {
        if (_automationService == null)
            return StatusCode(503, new { error = "Compliance automation service not available" });

        var userId = GetUserId();
        var results = await _automationService.VerifyFrameworkAsync(frameworkName, userId);
        return Ok(results);
    }

    /// <summary>
    /// Collect evidence for a control
    /// </summary>
    [HttpGet("automation/controls/{controlId:guid}/evidence")]
    [ProducesResponseType(typeof(ControlEvidenceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CollectEvidence(Guid controlId)
    {
        if (_automationService == null)
            return StatusCode(503, new { error = "Compliance automation service not available" });

        try
        {
            var evidence = await _automationService.CollectEvidenceAsync(controlId);
            return Ok(evidence);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Generate automated compliance report
    /// </summary>
    [HttpPost("automation/reports/generate")]
    [Authorize(Roles = "admin")]
    [ProducesResponseType(typeof(AutomatedComplianceReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GenerateAutomatedReport([FromBody] GenerateReportRequestDto request)
    {
        if (_automationService == null)
            return StatusCode(503, new { error = "Compliance automation service not available" });

        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var report = await _automationService.GenerateAutomatedReportAsync(
            request.FrameworkName,
            request.StartDate,
            request.EndDate,
            request.Format);

        return Ok(report);
    }

    /// <summary>
    /// Run continuous compliance check for all frameworks
    /// </summary>
    [HttpPost("automation/run-checks")]
    [Authorize(Roles = "admin")]
    [ProducesResponseType(typeof(ComplianceCheckSummaryDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> RunContinuousCheck()
    {
        if (_automationService == null)
            return StatusCode(503, new { error = "Compliance automation service not available" });

        var summary = await _automationService.RunContinuousComplianceCheckAsync();
        return Ok(summary);
    }

    /// <summary>
    /// Create remediation task for a compliance issue
    /// </summary>
    [HttpPost("automation/remediation-tasks")]
    [ProducesResponseType(typeof(RemediationTaskDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateRemediationTask([FromBody] CreateRemediationTaskDto request)
    {
        if (_automationService == null)
            return StatusCode(503, new { error = "Compliance automation service not available" });

        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var task = await _automationService.CreateRemediationTaskAsync(request);
            return CreatedAtAction(
                nameof(GetRemediationTasks),
                new { controlId = task.ControlId },
                task);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Update remediation task status
    /// </summary>
    [HttpPatch("automation/remediation-tasks/{taskId:guid}")]
    [ProducesResponseType(typeof(RemediationTaskDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateRemediationTaskStatus(
        Guid taskId,
        [FromBody] UpdateRemediationTaskStatusDto request)
    {
        if (_automationService == null)
            return StatusCode(503, new { error = "Compliance automation service not available" });

        try
        {
            var userId = GetUserId();
            var task = await _automationService.UpdateRemediationTaskStatusAsync(
                taskId,
                request.Status,
                userId,
                request.Notes);
            return Ok(task);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get remediation tasks for a control
    /// </summary>
    [HttpGet("automation/controls/{controlId:guid}/remediation-tasks")]
    [ProducesResponseType(typeof(List<RemediationTaskDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRemediationTasks(Guid controlId, [FromQuery] string? status = null)
    {
        if (_automationService == null)
            return StatusCode(503, new { error = "Compliance automation service not available" });

        var tasks = await _automationService.GetRemediationTasksAsync(controlId, status);
        return Ok(tasks);
    }

    /// <summary>
    /// Get verification history for a control
    /// </summary>
    [HttpGet("automation/controls/{controlId:guid}/verifications")]
    [ProducesResponseType(typeof(List<ControlVerificationResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetVerificationHistory(Guid controlId, [FromQuery] int limit = 10)
    {
        if (_automationService == null)
            return StatusCode(503, new { error = "Compliance automation service not available" });

        var history = await _automationService.GetVerificationHistoryAsync(controlId, limit);
        return Ok(history);
    }

    /// <summary>
    /// Get compliance dashboard summary
    /// </summary>
    [HttpGet("automation/dashboard")]
    [ProducesResponseType(typeof(ComplianceDashboardDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDashboard([FromQuery] string? frameworkName = null)
    {
        if (_automationService == null)
            return StatusCode(503, new { error = "Compliance automation service not available" });

        var dashboard = await _automationService.GetComplianceDashboardAsync(frameworkName);
        return Ok(dashboard);
    }

    /// <summary>
    /// Create or update verification schedule
    /// </summary>
    [HttpPost("automation/schedules")]
    [Authorize(Roles = "admin")]
    [ProducesResponseType(typeof(VerificationScheduleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateOrUpdateSchedule([FromBody] CreateVerificationScheduleDto request)
    {
        if (_automationService == null)
            return StatusCode(503, new { error = "Compliance automation service not available" });

        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var schedule = await _automationService.CreateOrUpdateScheduleAsync(request);
            return Ok(schedule);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get verification schedule for a control
    /// </summary>
    [HttpGet("automation/schedules/{controlId:guid}")]
    [ProducesResponseType(typeof(VerificationScheduleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSchedule(Guid controlId)
    {
        if (_automationService == null)
            return StatusCode(503, new { error = "Compliance automation service not available" });

        var schedule = await _automationService.GetScheduleAsync(controlId);
        if (schedule == null)
            return NotFound(new { error = "Schedule not found for the specified control" });

        return Ok(schedule);
    }

    /// <summary>
    /// Delete verification schedule
    /// </summary>
    [HttpDelete("automation/schedules/{controlId:guid}")]
    [Authorize(Roles = "admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteSchedule(Guid controlId)
    {
        if (_automationService == null)
            return StatusCode(503, new { error = "Compliance automation service not available" });

        await _automationService.DeleteScheduleAsync(controlId);
        return NoContent();
    }

    /// <summary>
    /// Get compliance trend data over time
    /// </summary>
    [HttpGet("automation/frameworks/{frameworkName}/trend")]
    [ProducesResponseType(typeof(ComplianceTrendDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetComplianceTrend(string frameworkName, [FromQuery] int days = 30)
    {
        if (_automationService == null)
            return StatusCode(503, new { error = "Compliance automation service not available" });

        var trend = await _automationService.GetComplianceTrendAsync(frameworkName, days);
        return Ok(trend);
    }

    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
    }
}

// Request DTOs
public class UpdateControlAssessmentRequest
{
    public string Status { get; set; } = string.Empty;
    public string? Implementation { get; set; }
    public string? Evidence { get; set; }
    public string? Gaps { get; set; }
}

public class ScheduleReportRequest
{
    public string Framework { get; set; } = string.Empty;
    public string Schedule { get; set; } = string.Empty; // Cron expression
    public string Format { get; set; } = "PDF";
}

// Response DTOs
public class ComplianceReportsResponse
{
    public List<ComplianceReportDto> Reports { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
}

public class FrameworksResponse
{
    public List<string> Frameworks { get; set; } = new();
    public int Count { get; set; }
}

public class ControlAssessmentsResponse
{
    public string Framework { get; set; } = string.Empty;
    public List<ControlAssessmentDto> Assessments { get; set; } = new();
    public int TotalControls { get; set; }
}

public class CriticalGapsResponse
{
    public List<ComplianceStatusDto> Frameworks { get; set; } = new();
    public int TotalFrameworks { get; set; }
    public string Message { get; set; } = string.Empty;
}

// Automated Compliance DTOs
public class UpdateRemediationTaskStatusDto
{
    public string Status { get; set; } = string.Empty;
    public string? Notes { get; set; }
}
