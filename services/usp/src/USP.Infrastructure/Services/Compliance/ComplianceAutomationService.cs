using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using USP.Core.Models.DTOs.Compliance;
using USP.Core.Models.Entities;
using USP.Core.Services.Compliance;
using USP.Infrastructure.Data;

namespace USP.Infrastructure.Services.Compliance;

/// <summary>
/// Automated compliance verification and reporting service
/// Performs continuous compliance monitoring and evidence collection
/// </summary>
public class ComplianceAutomationService : IComplianceAutomationService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ComplianceAutomationService> _logger;
    private readonly List<IControlVerifier> _verifiers;

    public ComplianceAutomationService(
        ApplicationDbContext context,
        ILogger<ComplianceAutomationService> logger)
    {
        _context = context;
        _logger = logger;

        // Initialize all control verifiers
        _verifiers = new List<IControlVerifier>
        {
            new AccessControlVerifier(context),
            new EncryptionVerifier(context),
            new AuditVerifier(context),
            new MonitoringVerifier(context)
        };
    }

    public async Task<ControlVerificationResultDto> VerifyControlAsync(Guid controlId, Guid? userId = null)
    {
        var control = await _context.ComplianceControls.FindAsync(controlId);
        if (control == null)
        {
            _logger.LogWarning("Control {ControlId} not found for verification", controlId);
            throw new InvalidOperationException($"Control {controlId} not found");
        }

        // Find appropriate verifier
        var verifier = _verifiers.FirstOrDefault(v => v.CanVerify(control.ControlType ?? ""));
        if (verifier == null)
        {
            _logger.LogWarning("No verifier found for control type: {ControlType}", control.ControlType);
            return new ControlVerificationResultDto
            {
                VerificationId = Guid.NewGuid(),
                ControlId = controlId,
                ControlName = control.Name,
                ControlDescription = control.Description ?? "",
                VerifiedAt = DateTime.UtcNow,
                Status = "manual_review_required",
                Score = 0,
                Evidence = new List<EvidenceItemDto>(),
                Findings = "No automated verifier available for this control type",
                Issues = new List<string> { "Manual review required" },
                Recommendations = new List<string> { "Implement manual verification process" },
                VerificationMethod = "manual",
                VerifiedBy = userId,
                DurationSeconds = 0
            };
        }

        // Perform verification
        var result = await verifier.VerifyAsync(controlId);
        result.VerifiedBy = userId;

        // Get verifier name
        if (userId.HasValue)
        {
            var user = await _context.Users.FindAsync(userId.Value);
            result.VerifierName = user?.UserName ?? "System";
        }
        else
        {
            result.VerifierName = "System";
        }

        // Persist verification
        var verification = new ComplianceControlVerification
        {
            Id = result.VerificationId,
            ControlId = controlId,
            VerifiedAt = result.VerifiedAt,
            Status = result.Status,
            Score = result.Score,
            Evidence = JsonSerializer.Serialize(result.Evidence),
            Findings = result.Findings,
            Issues = JsonSerializer.Serialize(result.Issues),
            Recommendations = JsonSerializer.Serialize(result.Recommendations),
            VerificationMethod = result.VerificationMethod,
            VerifiedBy = userId,
            DurationSeconds = result.DurationSeconds,
            NextVerificationDate = CalculateNextVerificationDate(control)
        };

        _context.ComplianceControlVerifications.Add(verification);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Control {ControlId} verified with status {Status} and score {Score}",
            controlId, result.Status, result.Score);

        return result;
    }

    public async Task<List<ControlVerificationResultDto>> VerifyFrameworkAsync(string frameworkName, Guid? userId = null)
    {
        var controls = await _context.ComplianceControls
            .Where(c => c.FrameworkName == frameworkName && c.IsActive)
            .ToListAsync();

        if (!controls.Any())
        {
            _logger.LogWarning("No active controls found for framework: {Framework}", frameworkName);
            return new List<ControlVerificationResultDto>();
        }

        var results = new List<ControlVerificationResultDto>();
        foreach (var control in controls)
        {
            try
            {
                var result = await VerifyControlAsync(control.Id, userId);
                results.Add(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to verify control {ControlId}", control.Id);
            }
        }

        _logger.LogInformation(
            "Verified {ControlCount} controls for framework {Framework}",
            results.Count, frameworkName);

        return results;
    }

    public async Task<ControlEvidenceDto> CollectEvidenceAsync(Guid controlId)
    {
        var control = await _context.ComplianceControls.FindAsync(controlId);
        if (control == null)
        {
            throw new InvalidOperationException($"Control {controlId} not found");
        }

        var verifier = _verifiers.FirstOrDefault(v => v.CanVerify(control.ControlType ?? ""));
        if (verifier == null)
        {
            return new ControlEvidenceDto
            {
                ControlId = controlId,
                CollectedAt = DateTime.UtcNow,
                Items = new List<EvidenceItemDto>(),
                TotalItems = 0,
                EvidenceTypeCounts = new Dictionary<string, int>()
            };
        }

        return await verifier.CollectEvidenceAsync(controlId);
    }

    public async Task<AutomatedComplianceReportDto> GenerateAutomatedReportAsync(
        string frameworkName,
        DateTime? startDate = null,
        DateTime? endDate = null,
        string format = "json")
    {
        var periodStart = startDate ?? DateTime.UtcNow.AddDays(-30);
        var periodEnd = endDate ?? DateTime.UtcNow;

        // Verify all controls in framework
        var results = await VerifyFrameworkAsync(frameworkName);

        // Get open remediation tasks
        var openTasks = await _context.ComplianceRemediationTasks
            .Include(t => t.Control)
            .Where(t => t.Control.FrameworkName == frameworkName && t.Status == "open")
            .Select(t => MapRemediationTask(t))
            .ToListAsync();

        var totalControls = results.Count;
        var passedControls = results.Count(r => r.Status == "pass");
        var failedControls = results.Count(r => r.Status == "fail");
        var warningControls = results.Count(r => r.Status == "warning");

        var complianceScore = totalControls > 0
            ? (double)results.Sum(r => r.Score) / totalControls
            : 0;

        var report = new AutomatedComplianceReportDto
        {
            ReportId = Guid.NewGuid(),
            FrameworkName = frameworkName,
            GeneratedAt = DateTime.UtcNow,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            Format = format,
            TotalControls = totalControls,
            PassedControls = passedControls,
            FailedControls = failedControls,
            WarningControls = warningControls,
            ComplianceScore = complianceScore,
            ControlResults = results,
            OpenRemediationTasks = openTasks,
            StatusBreakdown = new Dictionary<string, int>
            {
                ["pass"] = passedControls,
                ["fail"] = failedControls,
                ["warning"] = warningControls
            }
        };

        _logger.LogInformation(
            "Generated compliance report for {Framework} with score {Score}",
            frameworkName, complianceScore);

        return report;
    }

    public async Task<ComplianceCheckSummaryDto> RunContinuousComplianceCheckAsync()
    {
        var startTime = DateTime.UtcNow;

        // Get all active frameworks
        var frameworks = await _context.ComplianceControls
            .Where(c => c.IsActive)
            .Select(c => c.FrameworkName)
            .Distinct()
            .ToListAsync();

        var allResults = new List<ControlVerificationResultDto>();
        var frameworkScores = new Dictionary<string, int>();

        foreach (var framework in frameworks)
        {
            var results = await VerifyFrameworkAsync(framework);
            allResults.AddRange(results);

            var avgScore = results.Any()
                ? (int)results.Average(r => r.Score)
                : 0;
            frameworkScores[framework] = avgScore;
        }

        var failedResults = allResults.Where(r => r.Status == "fail").ToList();

        // Create remediation tasks for failures
        var tasksCreated = 0;
        foreach (var failure in failedResults)
        {
            if (failure.Issues.Any())
            {
                foreach (var issue in failure.Issues.Take(3)) // Limit to top 3 issues per control
                {
                    var recommendation = failure.Recommendations.FirstOrDefault() ?? "Review and remediate issue";

                    await CreateRemediationTaskAsync(new CreateRemediationTaskDto
                    {
                        VerificationId = failure.VerificationId,
                        ControlId = failure.ControlId,
                        Title = $"{failure.ControlName} - {issue}",
                        Description = failure.Findings ?? "",
                        RemediationAction = recommendation,
                        Priority = failure.Score < 40 ? "critical" : failure.Score < 60 ? "high" : "medium",
                        ImpactLevel = failure.Score < 40 ? "critical" : failure.Score < 60 ? "high" : "medium"
                    });
                    tasksCreated++;
                }
            }
        }

        var endTime = DateTime.UtcNow;

        var summary = new ComplianceCheckSummaryDto
        {
            RunStartedAt = startTime,
            RunCompletedAt = endTime,
            DurationSeconds = (int)(endTime - startTime).TotalSeconds,
            TotalControlsVerified = allResults.Count,
            PassedControls = allResults.Count(r => r.Status == "pass"),
            FailedControls = allResults.Count(r => r.Status == "fail"),
            WarningControls = allResults.Count(r => r.Status == "warning"),
            RemediationTasksCreated = tasksCreated,
            FrameworksVerified = frameworks,
            FrameworkScores = frameworkScores,
            FailedControlDetails = failedResults
        };

        _logger.LogInformation(
            "Continuous compliance check completed: {TotalControls} controls verified, {Failed} failed",
            summary.TotalControlsVerified, summary.FailedControls);

        return summary;
    }

    public async Task<RemediationTaskDto> CreateRemediationTaskAsync(CreateRemediationTaskDto request)
    {
        var control = await _context.ComplianceControls.FindAsync(request.ControlId);
        if (control == null)
        {
            throw new InvalidOperationException($"Control {request.ControlId} not found");
        }

        var task = new ComplianceRemediationTask
        {
            Id = Guid.NewGuid(),
            VerificationId = request.VerificationId,
            ControlId = request.ControlId,
            Title = request.Title,
            Description = request.Description,
            RemediationAction = request.RemediationAction,
            Priority = request.Priority,
            Status = "open",
            AssignedTo = request.AssignedTo,
            DueDate = request.DueDate,
            ImpactLevel = request.ImpactLevel,
            EstimatedEffort = request.EstimatedEffort,
            Tags = request.Tags != null ? JsonSerializer.Serialize(request.Tags) : null,
            CreatedAt = DateTime.UtcNow
        };

        _context.ComplianceRemediationTasks.Add(task);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Created remediation task {TaskId} for control {ControlId}",
            task.Id, request.ControlId);

        return MapRemediationTask(task);
    }

    public async Task<RemediationTaskDto> UpdateRemediationTaskStatusAsync(
        Guid taskId,
        string status,
        Guid userId,
        string? notes = null)
    {
        var task = await _context.ComplianceRemediationTasks.FindAsync(taskId);
        if (task == null)
        {
            throw new InvalidOperationException($"Remediation task {taskId} not found");
        }

        task.Status = status;

        if (status == "completed")
        {
            task.CompletedAt = DateTime.UtcNow;
            task.CompletedBy = userId;
            task.CompletionNotes = notes;
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Updated remediation task {TaskId} status to {Status}",
            taskId, status);

        return MapRemediationTask(task);
    }

    public async Task<List<RemediationTaskDto>> GetRemediationTasksAsync(Guid controlId, string? status = null)
    {
        var query = _context.ComplianceRemediationTasks
            .Where(t => t.ControlId == controlId);

        if (!string.IsNullOrEmpty(status))
        {
            query = query.Where(t => t.Status == status);
        }

        var tasks = await query
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

        return tasks.Select(MapRemediationTask).ToList();
    }

    public async Task<List<ControlVerificationResultDto>> GetVerificationHistoryAsync(Guid controlId, int limit = 10)
    {
        var verifications = await _context.ComplianceControlVerifications
            .Where(v => v.ControlId == controlId)
            .OrderByDescending(v => v.VerifiedAt)
            .Take(limit)
            .ToListAsync();

        return verifications.Select(MapVerification).ToList();
    }

    public async Task<ComplianceDashboardDto> GetComplianceDashboardAsync(string? frameworkName = null)
    {
        var query = _context.ComplianceControls.AsQueryable();

        if (!string.IsNullOrEmpty(frameworkName))
        {
            query = query.Where(c => c.FrameworkName == frameworkName);
        }

        var controls = await query.ToListAsync();

        // Get latest verifications
        var controlIds = controls.Select(c => c.Id).ToList();
        var latestVerifications = await _context.ComplianceControlVerifications
            .Where(v => controlIds.Contains(v.ControlId))
            .GroupBy(v => v.ControlId)
            .Select(g => g.OrderByDescending(v => v.VerifiedAt).FirstOrDefault())
            .ToListAsync();

        var passedControls = latestVerifications.Count(v => v != null && v.Status == "pass");
        var failedControls = latestVerifications.Count(v => v != null && v.Status == "fail");
        var warningControls = latestVerifications.Count(v => v != null && v.Status == "warning");
        var notVerifiedControls = controls.Count - latestVerifications.Count(v => v != null);

        var overallScore = latestVerifications.Any(v => v != null)
            ? latestVerifications.Where(v => v != null).Average(v => v!.Score)
            : 0;

        // Get remediation tasks
        var openTasks = await _context.ComplianceRemediationTasks
            .Where(t => controlIds.Contains(t.ControlId) && t.Status == "open")
            .ToListAsync();

        var criticalTasks = openTasks.Count(t => t.Priority == "critical");
        var overdueTasks = openTasks.Count(t => t.DueDate.HasValue && t.DueDate < DateTime.UtcNow);

        // Get framework scores
        var frameworkScores = new Dictionary<string, double>();
        var frameworks = controls.Select(c => c.FrameworkName).Distinct().ToList();
        foreach (var framework in frameworks)
        {
            var frameworkControlIds = controls
                .Where(c => c.FrameworkName == framework)
                .Select(c => c.Id)
                .ToList();

            var frameworkVerifications = latestVerifications
                .Where(v => v != null && frameworkControlIds.Contains(v.ControlId))
                .ToList();

            var score = frameworkVerifications.Any()
                ? frameworkVerifications.Average(v => v!.Score)
                : 0;

            frameworkScores[framework] = score;
        }

        // Get recent failures
        var recentFailures = latestVerifications
            .Where(v => v != null && v.Status == "fail")
            .OrderByDescending(v => v!.VerifiedAt)
            .Take(10)
            .Select(v => MapVerification(v!))
            .ToList();

        // Get critical tasks
        var criticalTasksList = openTasks
            .Where(t => t.Priority == "critical")
            .OrderBy(t => t.DueDate)
            .Take(10)
            .Select(MapRemediationTask)
            .ToList();

        var lastVerification = latestVerifications
            .Where(v => v != null)
            .Max(v => v?.VerifiedAt);

        return new ComplianceDashboardDto
        {
            FrameworkName = frameworkName,
            GeneratedAt = DateTime.UtcNow,
            OverallComplianceScore = overallScore,
            TotalControls = controls.Count,
            PassedControls = passedControls,
            FailedControls = failedControls,
            WarningControls = warningControls,
            NotVerifiedControls = notVerifiedControls,
            OpenRemediationTasks = openTasks.Count,
            CriticalRemediationTasks = criticalTasks,
            OverdueRemediationTasks = overdueTasks,
            LastVerificationRun = lastVerification,
            FrameworkScores = frameworkScores,
            ControlStatusBreakdown = new Dictionary<string, int>
            {
                ["pass"] = passedControls,
                ["fail"] = failedControls,
                ["warning"] = warningControls,
                ["not_verified"] = notVerifiedControls
            },
            RecentFailures = recentFailures,
            CriticalTasks = criticalTasksList
        };
    }

    public async Task<VerificationScheduleDto> CreateOrUpdateScheduleAsync(CreateVerificationScheduleDto request)
    {
        var control = await _context.ComplianceControls.FindAsync(request.ControlId);
        if (control == null)
        {
            throw new InvalidOperationException($"Control {request.ControlId} not found");
        }

        var existingSchedule = await _context.ComplianceVerificationSchedules
            .FirstOrDefaultAsync(s => s.ControlId == request.ControlId);

        ComplianceVerificationSchedule schedule;

        if (existingSchedule != null)
        {
            existingSchedule.Frequency = request.Frequency;
            existingSchedule.CronExpression = request.CronExpression;
            existingSchedule.IsEnabled = request.IsEnabled;
            existingSchedule.NotificationSettings = request.NotificationSettings != null
                ? JsonSerializer.Serialize(request.NotificationSettings)
                : null;
            existingSchedule.AutoRemediationSettings = request.AutoRemediationSettings != null
                ? JsonSerializer.Serialize(request.AutoRemediationSettings)
                : null;
            existingSchedule.UpdatedAt = DateTime.UtcNow;

            schedule = existingSchedule;
        }
        else
        {
            schedule = new ComplianceVerificationSchedule
            {
                Id = Guid.NewGuid(),
                ControlId = request.ControlId,
                Frequency = request.Frequency,
                CronExpression = request.CronExpression,
                IsEnabled = request.IsEnabled,
                NotificationSettings = request.NotificationSettings != null
                    ? JsonSerializer.Serialize(request.NotificationSettings)
                    : null,
                AutoRemediationSettings = request.AutoRemediationSettings != null
                    ? JsonSerializer.Serialize(request.AutoRemediationSettings)
                    : null,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.ComplianceVerificationSchedules.Add(schedule);
        }

        await _context.SaveChangesAsync();

        return MapSchedule(schedule, control.Name);
    }

    public async Task<VerificationScheduleDto?> GetScheduleAsync(Guid controlId)
    {
        var schedule = await _context.ComplianceVerificationSchedules
            .Include(s => s.Control)
            .FirstOrDefaultAsync(s => s.ControlId == controlId);

        return schedule != null ? MapSchedule(schedule, schedule.Control.Name) : null;
    }

    public async Task DeleteScheduleAsync(Guid controlId)
    {
        var schedule = await _context.ComplianceVerificationSchedules
            .FirstOrDefaultAsync(s => s.ControlId == controlId);

        if (schedule != null)
        {
            _context.ComplianceVerificationSchedules.Remove(schedule);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Deleted verification schedule for control {ControlId}", controlId);
        }
    }

    public async Task<ComplianceTrendDto> GetComplianceTrendAsync(string frameworkName, int days = 30)
    {
        var startDate = DateTime.UtcNow.AddDays(-days);

        var controlIds = await _context.ComplianceControls
            .Where(c => c.FrameworkName == frameworkName)
            .Select(c => c.Id)
            .ToListAsync();

        var verifications = await _context.ComplianceControlVerifications
            .Where(v => controlIds.Contains(v.ControlId) && v.VerifiedAt >= startDate)
            .OrderBy(v => v.VerifiedAt)
            .ToListAsync();

        // Group by date
        var dataPoints = verifications
            .GroupBy(v => v.VerifiedAt.Date)
            .Select(g => new ComplianceTrendDataPointDto
            {
                Date = g.Key,
                Score = g.Average(v => v.Score),
                PassedControls = g.Count(v => v.Status == "pass"),
                FailedControls = g.Count(v => v.Status == "fail"),
                TotalControls = g.Count()
            })
            .OrderBy(d => d.Date)
            .ToList();

        var avgScore = dataPoints.Any() ? dataPoints.Average(d => d.Score) : 0;
        var highestScore = dataPoints.Any() ? dataPoints.Max(d => d.Score) : 0;
        var lowestScore = dataPoints.Any() ? dataPoints.Min(d => d.Score) : 0;

        // Determine trend
        var trend = "stable";
        if (dataPoints.Count >= 2)
        {
            var firstHalfAvg = dataPoints.Take(dataPoints.Count / 2).Average(d => d.Score);
            var secondHalfAvg = dataPoints.Skip(dataPoints.Count / 2).Average(d => d.Score);

            if (secondHalfAvg > firstHalfAvg + 5)
                trend = "improving";
            else if (secondHalfAvg < firstHalfAvg - 5)
                trend = "declining";
        }

        return new ComplianceTrendDto
        {
            FrameworkName = frameworkName,
            Days = days,
            DataPoints = dataPoints,
            AverageScore = avgScore,
            HighestScore = highestScore,
            LowestScore = lowestScore,
            Trend = trend
        };
    }

    // Helper methods

    private DateTime? CalculateNextVerificationDate(ComplianceControl control)
    {
        // Default to daily verification if no frequency specified
        return DateTime.UtcNow.AddDays(1);
    }

    private RemediationTaskDto MapRemediationTask(ComplianceRemediationTask task)
    {
        return new RemediationTaskDto
        {
            TaskId = task.Id,
            VerificationId = task.VerificationId,
            ControlId = task.ControlId,
            ControlName = task.Control?.Name ?? "",
            Title = task.Title,
            Description = task.Description,
            RemediationAction = task.RemediationAction,
            Priority = task.Priority,
            Status = task.Status,
            AssignedTo = task.AssignedTo,
            AssignedToName = task.AssignedUser?.UserName,
            DueDate = task.DueDate,
            CreatedAt = task.CreatedAt,
            CompletedAt = task.CompletedAt,
            CompletedBy = task.CompletedBy,
            CompletedByName = task.CompletedByUser?.UserName,
            CompletionNotes = task.CompletionNotes,
            ImpactLevel = task.ImpactLevel,
            EstimatedEffort = task.EstimatedEffort,
            Tags = task.Tags != null
                ? JsonSerializer.Deserialize<List<string>>(task.Tags) ?? new List<string>()
                : new List<string>()
        };
    }

    private ControlVerificationResultDto MapVerification(ComplianceControlVerification verification)
    {
        return new ControlVerificationResultDto
        {
            VerificationId = verification.Id,
            ControlId = verification.ControlId,
            ControlName = verification.Control?.Name ?? "",
            ControlDescription = verification.Control?.Description ?? "",
            VerifiedAt = verification.VerifiedAt,
            Status = verification.Status,
            Score = verification.Score,
            Evidence = verification.Evidence != null
                ? JsonSerializer.Deserialize<List<EvidenceItemDto>>(verification.Evidence) ?? new List<EvidenceItemDto>()
                : new List<EvidenceItemDto>(),
            Findings = verification.Findings,
            Issues = verification.Issues != null
                ? JsonSerializer.Deserialize<List<string>>(verification.Issues) ?? new List<string>()
                : new List<string>(),
            Recommendations = verification.Recommendations != null
                ? JsonSerializer.Deserialize<List<string>>(verification.Recommendations) ?? new List<string>()
                : new List<string>(),
            VerificationMethod = verification.VerificationMethod,
            VerifiedBy = verification.VerifiedBy,
            VerifierName = verification.Verifier?.UserName ?? "System",
            DurationSeconds = verification.DurationSeconds,
            NextVerificationDate = verification.NextVerificationDate
        };
    }

    private VerificationScheduleDto MapSchedule(ComplianceVerificationSchedule schedule, string controlName)
    {
        return new VerificationScheduleDto
        {
            ScheduleId = schedule.Id,
            ControlId = schedule.ControlId,
            ControlName = controlName,
            Frequency = schedule.Frequency,
            CronExpression = schedule.CronExpression,
            IsEnabled = schedule.IsEnabled,
            LastRunAt = schedule.LastRunAt,
            NextRunAt = schedule.NextRunAt,
            LastRunStatus = schedule.LastRunStatus,
            LastRunDurationSeconds = schedule.LastRunDurationSeconds,
            NotificationSettings = schedule.NotificationSettings != null
                ? JsonSerializer.Deserialize<Dictionary<string, object>>(schedule.NotificationSettings)
                : null,
            AutoRemediationSettings = schedule.AutoRemediationSettings != null
                ? JsonSerializer.Deserialize<Dictionary<string, object>>(schedule.AutoRemediationSettings)
                : null
        };
    }
}
