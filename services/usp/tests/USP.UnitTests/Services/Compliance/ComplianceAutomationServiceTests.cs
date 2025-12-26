using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using USP.Core.Models.Entities;
using USP.Infrastructure.Data;
using USP.Infrastructure.Services.Compliance;
using Xunit;

namespace USP.UnitTests.Services.Compliance;

public class ComplianceAutomationServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<ILogger<ComplianceAutomationService>> _mockLogger;
    private readonly ComplianceAutomationService _service;

    public ComplianceAutomationServiceTests()
    {
        // Setup in-memory database
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"ComplianceAutomationTestDb_{Guid.NewGuid()}")
            .Options;

        _context = new ApplicationDbContext(options);
        _mockLogger = new Mock<ILogger<ComplianceAutomationService>>();
        _service = new ComplianceAutomationService(_context, _mockLogger.Object);

        // Seed test data
        SeedTestData();
    }

    private void SeedTestData()
    {
        // Create test control
        var control = new ComplianceControl
        {
            Id = Guid.NewGuid(),
            ControlId = "SOC2-CC6.1",
            ControlName = "Access Control Test",
            ControlDescription = "Test access control policies",
            Name = "Access Control",
            Description = "Verify access control implementation",
            FrameworkName = "SOC2",
            ControlType = "access",
            Category = "Access Control",
            IsActive = true,
            Status = "not_implemented"
        };

        var encryptionControl = new ComplianceControl
        {
            Id = Guid.NewGuid(),
            ControlId = "SOC2-CC6.2",
            ControlName = "Encryption Control Test",
            ControlDescription = "Test encryption policies",
            Name = "Encryption Control",
            Description = "Verify encryption implementation",
            FrameworkName = "SOC2",
            ControlType = "encryption",
            Category = "Encryption",
            IsActive = true,
            Status = "not_implemented"
        };

        // Add test roles and permissions for access control verification
        var role = new Role
        {
            Id = Guid.NewGuid(),
            Name = "TestRole",
            NormalizedName = "TESTROLE"
        };

        var permission = new Permission
        {
            Id = Guid.NewGuid(),
            Name = "test.read",
            Resource = "test",
            Action = "read"
        };

        var policy = new AccessPolicy
        {
            Id = Guid.NewGuid(),
            Name = "TestPolicy",
            Effect = "allow",
            Resources = "[\"*\"]",
            Actions = new[] { "read" }
        };

        _context.ComplianceControls.AddRange(control, encryptionControl);
        _context.Roles.Add(role);
        _context.Permissions.Add(permission);
        _context.AccessPolicies.Add(policy);
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    [Fact]
    public async Task VerifyControlAsync_WithValidControl_ReturnsVerificationResult()
    {
        // Arrange
        var control = await _context.ComplianceControls.FirstAsync(c => c.ControlType == "access");

        // Act
        var result = await _service.VerifyControlAsync(control.Id, null);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(control.Id, result.ControlId);
        Assert.Equal("Access Control", result.ControlName);
        Assert.InRange(result.Score, 0, 100);
        Assert.NotEmpty(result.Evidence);
        Assert.Equal("automated", result.VerificationMethod);
    }

    [Fact]
    public async Task VerifyControlAsync_WithInvalidControlId_ThrowsException()
    {
        // Arrange
        var invalidControlId = Guid.NewGuid();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.VerifyControlAsync(invalidControlId, null));
    }

    [Fact]
    public async Task VerifyControlAsync_WithNoVerifier_ReturnsManualReviewRequired()
    {
        // Arrange - Create a control with a type that has no verifier
        var control = new ComplianceControl
        {
            Id = Guid.NewGuid(),
            ControlId = "TEST-1",
            ControlName = "Unknown Control",
            ControlDescription = "Test unknown control",
            Name = "Unknown",
            Description = "Unknown control type",
            FrameworkName = "TEST",
            ControlType = "unknown",
            Category = "Unknown",
            IsActive = true
        };
        _context.ComplianceControls.Add(control);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.VerifyControlAsync(control.Id, null);

        // Assert
        Assert.Equal("manual_review_required", result.Status);
        Assert.Contains("No automated verifier available", result.Findings);
    }

    [Fact]
    public async Task VerifyFrameworkAsync_WithActiveControls_ReturnsMultipleResults()
    {
        // Arrange
        var frameworkName = "SOC2";

        // Act
        var results = await _service.VerifyFrameworkAsync(frameworkName, null);

        // Assert
        Assert.NotEmpty(results);
        Assert.All(results, r => Assert.NotNull(r.ControlName));
    }

    [Fact]
    public async Task CollectEvidenceAsync_WithValidControl_ReturnsEvidence()
    {
        // Arrange
        var control = await _context.ComplianceControls.FirstAsync(c => c.ControlType == "access");

        // Act
        var evidence = await _service.CollectEvidenceAsync(control.Id);

        // Assert
        Assert.NotNull(evidence);
        Assert.Equal(control.Id, evidence.ControlId);
        Assert.NotEmpty(evidence.Items);
        Assert.True(evidence.TotalItems > 0);
    }

    [Fact]
    public async Task GenerateAutomatedReportAsync_WithFramework_ReturnsReport()
    {
        // Arrange
        var frameworkName = "SOC2";

        // Act
        var report = await _service.GenerateAutomatedReportAsync(frameworkName);

        // Assert
        Assert.NotNull(report);
        Assert.Equal(frameworkName, report.FrameworkName);
        Assert.True(report.TotalControls > 0);
        Assert.NotEmpty(report.ControlResults);
        Assert.InRange(report.ComplianceScore, 0, 100);
    }

    [Fact]
    public async Task RunContinuousComplianceCheckAsync_ReturnsCheckSummary()
    {
        // Act
        var summary = await _service.RunContinuousComplianceCheckAsync();

        // Assert
        Assert.NotNull(summary);
        Assert.True(summary.TotalControlsVerified > 0);
        Assert.NotEmpty(summary.FrameworksVerified);
        Assert.True(summary.FrameworkScores.Count > 0);
    }

    [Fact]
    public async Task CreateRemediationTaskAsync_WithValidRequest_CreatesTask()
    {
        // Arrange
        var control = await _context.ComplianceControls.FirstAsync();
        var request = new Core.Models.DTOs.Compliance.CreateRemediationTaskDto
        {
            ControlId = control.Id,
            Title = "Fix Access Control Issues",
            Description = "Need to configure RBAC properly",
            RemediationAction = "Configure roles and permissions",
            Priority = "high",
            ImpactLevel = "high"
        };

        // Act
        var task = await _service.CreateRemediationTaskAsync(request);

        // Assert
        Assert.NotNull(task);
        Assert.Equal(control.Id, task.ControlId);
        Assert.Equal(request.Title, task.Title);
        Assert.Equal("open", task.Status);
    }

    [Fact]
    public async Task UpdateRemediationTaskStatusAsync_UpdatesTaskStatus()
    {
        // Arrange
        var control = await _context.ComplianceControls.FirstAsync();
        var task = new ComplianceRemediationTask
        {
            Id = Guid.NewGuid(),
            ControlId = control.Id,
            Title = "Test Task",
            Description = "Test description",
            RemediationAction = "Test action",
            Priority = "medium",
            Status = "open"
        };
        _context.ComplianceRemediationTasks.Add(task);
        await _context.SaveChangesAsync();

        var userId = Guid.NewGuid();

        // Act
        var updated = await _service.UpdateRemediationTaskStatusAsync(
            task.Id,
            "completed",
            userId,
            "Completed successfully");

        // Assert
        Assert.Equal("completed", updated.Status);
        Assert.NotNull(updated.CompletedAt);
        Assert.Equal(userId, updated.CompletedBy);
        Assert.Equal("Completed successfully", updated.CompletionNotes);
    }

    [Fact]
    public async Task GetRemediationTasksAsync_ReturnsTasksForControl()
    {
        // Arrange
        var control = await _context.ComplianceControls.FirstAsync();
        var task1 = new ComplianceRemediationTask
        {
            Id = Guid.NewGuid(),
            ControlId = control.Id,
            Title = "Task 1",
            Description = "Description 1",
            RemediationAction = "Action 1",
            Priority = "high",
            Status = "open"
        };
        var task2 = new ComplianceRemediationTask
        {
            Id = Guid.NewGuid(),
            ControlId = control.Id,
            Title = "Task 2",
            Description = "Description 2",
            RemediationAction = "Action 2",
            Priority = "medium",
            Status = "completed",
            CompletedAt = DateTime.UtcNow
        };
        _context.ComplianceRemediationTasks.AddRange(task1, task2);
        await _context.SaveChangesAsync();

        // Act
        var allTasks = await _service.GetRemediationTasksAsync(control.Id);
        var openTasks = await _service.GetRemediationTasksAsync(control.Id, "open");

        // Assert
        Assert.Equal(2, allTasks.Count);
        Assert.Single(openTasks);
        Assert.Equal("open", openTasks[0].Status);
    }

    [Fact]
    public async Task GetVerificationHistoryAsync_ReturnsVerifications()
    {
        // Arrange
        var control = await _context.ComplianceControls.FirstAsync();

        // Create verification history
        for (int i = 0; i < 5; i++)
        {
            var verification = new ComplianceControlVerification
            {
                Id = Guid.NewGuid(),
                ControlId = control.Id,
                VerifiedAt = DateTime.UtcNow.AddDays(-i),
                Status = i % 2 == 0 ? "pass" : "warning",
                Score = 80 + i,
                VerificationMethod = "automated",
                DurationSeconds = 5
            };
            _context.ComplianceControlVerifications.Add(verification);
        }
        await _context.SaveChangesAsync();

        // Act
        var history = await _service.GetVerificationHistoryAsync(control.Id, 3);

        // Assert
        Assert.Equal(3, history.Count);
        Assert.True(history[0].VerifiedAt >= history[1].VerifiedAt); // Ordered by date desc
    }

    [Fact]
    public async Task GetComplianceDashboardAsync_ReturnsDashboard()
    {
        // Arrange
        var control = await _context.ComplianceControls.FirstAsync();
        var verification = new ComplianceControlVerification
        {
            Id = Guid.NewGuid(),
            ControlId = control.Id,
            VerifiedAt = DateTime.UtcNow,
            Status = "pass",
            Score = 85,
            VerificationMethod = "automated",
            DurationSeconds = 5
        };
        _context.ComplianceControlVerifications.Add(verification);
        await _context.SaveChangesAsync();

        // Act
        var dashboard = await _service.GetComplianceDashboardAsync();

        // Assert
        Assert.NotNull(dashboard);
        Assert.True(dashboard.TotalControls > 0);
        Assert.InRange(dashboard.OverallComplianceScore, 0, 100);
    }

    [Fact]
    public async Task CreateOrUpdateScheduleAsync_CreatesNewSchedule()
    {
        // Arrange
        var control = await _context.ComplianceControls.FirstAsync();
        var request = new Core.Models.DTOs.Compliance.CreateVerificationScheduleDto
        {
            ControlId = control.Id,
            Frequency = "daily",
            IsEnabled = true
        };

        // Act
        var schedule = await _service.CreateOrUpdateScheduleAsync(request);

        // Assert
        Assert.NotNull(schedule);
        Assert.Equal(control.Id, schedule.ControlId);
        Assert.Equal("daily", schedule.Frequency);
        Assert.True(schedule.IsEnabled);
    }

    [Fact]
    public async Task CreateOrUpdateScheduleAsync_UpdatesExistingSchedule()
    {
        // Arrange
        var control = await _context.ComplianceControls.FirstAsync();
        var existingSchedule = new ComplianceVerificationSchedule
        {
            Id = Guid.NewGuid(),
            ControlId = control.Id,
            Frequency = "daily",
            IsEnabled = true
        };
        _context.ComplianceVerificationSchedules.Add(existingSchedule);
        await _context.SaveChangesAsync();

        var request = new Core.Models.DTOs.Compliance.CreateVerificationScheduleDto
        {
            ControlId = control.Id,
            Frequency = "weekly",
            IsEnabled = false
        };

        // Act
        var updated = await _service.CreateOrUpdateScheduleAsync(request);

        // Assert
        Assert.Equal("weekly", updated.Frequency);
        Assert.False(updated.IsEnabled);
    }

    [Fact]
    public async Task GetScheduleAsync_ReturnsSchedule()
    {
        // Arrange
        var control = await _context.ComplianceControls.FirstAsync();
        var schedule = new ComplianceVerificationSchedule
        {
            Id = Guid.NewGuid(),
            ControlId = control.Id,
            Frequency = "monthly",
            IsEnabled = true
        };
        _context.ComplianceVerificationSchedules.Add(schedule);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetScheduleAsync(control.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("monthly", result.Frequency);
    }

    [Fact]
    public async Task GetScheduleAsync_WithNoSchedule_ReturnsNull()
    {
        // Arrange
        var nonExistentControlId = Guid.NewGuid();

        // Act
        var result = await _service.GetScheduleAsync(nonExistentControlId);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteScheduleAsync_RemovesSchedule()
    {
        // Arrange
        var control = await _context.ComplianceControls.FirstAsync();
        var schedule = new ComplianceVerificationSchedule
        {
            Id = Guid.NewGuid(),
            ControlId = control.Id,
            Frequency = "daily",
            IsEnabled = true
        };
        _context.ComplianceVerificationSchedules.Add(schedule);
        await _context.SaveChangesAsync();

        // Act
        await _service.DeleteScheduleAsync(control.Id);

        // Assert
        var deleted = await _context.ComplianceVerificationSchedules
            .FirstOrDefaultAsync(s => s.ControlId == control.Id);
        Assert.Null(deleted);
    }

    [Fact]
    public async Task GetComplianceTrendAsync_ReturnsTrendData()
    {
        // Arrange
        var frameworkName = "SOC2";
        var control = await _context.ComplianceControls.FirstAsync(c => c.FrameworkName == frameworkName);

        // Create trend data
        for (int i = 0; i < 7; i++)
        {
            var verification = new ComplianceControlVerification
            {
                Id = Guid.NewGuid(),
                ControlId = control.Id,
                VerifiedAt = DateTime.UtcNow.Date.AddDays(-i),
                Status = "pass",
                Score = 70 + (i * 2),
                VerificationMethod = "automated",
                DurationSeconds = 5
            };
            _context.ComplianceControlVerifications.Add(verification);
        }
        await _context.SaveChangesAsync();

        // Act
        var trend = await _service.GetComplianceTrendAsync(frameworkName, 30);

        // Assert
        Assert.NotNull(trend);
        Assert.Equal(frameworkName, trend.FrameworkName);
        Assert.NotEmpty(trend.DataPoints);
        Assert.InRange(trend.AverageScore, 0, 100);
        Assert.Contains(trend.Trend, new[] { "improving", "declining", "stable" });
    }
}
