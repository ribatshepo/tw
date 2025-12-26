using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using USP.Core.Models.Entities;
using USP.Core.Services.Authentication;
using USP.Core.Services.Mfa;
using USP.Infrastructure.Data;
using USP.Infrastructure.Services.Authentication;
using USP.Core.Models.DTOs.Authentication;

namespace USP.UnitTests.Services.Authentication;

/// <summary>
/// Unit tests for AdaptiveAuthPolicyEngine
/// Tests policy evaluation, step-up challenges, and authentication event tracking
/// </summary>
public class AdaptiveAuthPolicyEngineTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<IRiskAssessmentService> _riskAssessmentMock;
    private readonly Mock<IMfaService> _mfaServiceMock;
    private readonly Mock<ILogger<AdaptiveAuthPolicyEngine>> _loggerMock;
    private readonly AdaptiveAuthPolicyEngine _service;

    public AdaptiveAuthPolicyEngineTests()
    {
        // Setup in-memory database
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"AdaptiveAuthTest_{Guid.NewGuid()}")
            .Options;

        _context = new ApplicationDbContext(options);

        // Setup mocks
        _riskAssessmentMock = new Mock<IRiskAssessmentService>();
        _mfaServiceMock = new Mock<IMfaService>();
        _loggerMock = new Mock<ILogger<AdaptiveAuthPolicyEngine>>();

        _service = new AdaptiveAuthPolicyEngine(
            _context,
            _riskAssessmentMock.Object,
            _mfaServiceMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task EvaluatePolicyAsync_LowRiskScore_ReturnsAllowAction()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var lowRiskScore = 20;

        // No policies configured, should default to allow

        // Act
        var result = await _service.EvaluatePolicyAsync(userId, lowRiskScore);

        // Assert
        Assert.Equal("allow", result.Action);
        Assert.Equal(lowRiskScore, result.RiskScore);
        Assert.Equal("low", result.RiskLevel);
        Assert.Null(result.PolicyId);
    }

    [Fact]
    public async Task EvaluatePolicyAsync_HighRiskScoreWithMatchingPolicy_ReturnsStepUpAction()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var highRiskScore = 75;

        var user = new ApplicationUser
        {
            Id = userId,
            Email = "user@example.com",
            UserName = "testuser",
            PasswordHash = "dummy-hash"
        };

        var policy = new AdaptiveAuthPolicy
        {
            Id = Guid.NewGuid(),
            Name = "High Risk Policy",
            Description = "Require step-up for high risk",
            MinRiskScore = 70,
            MaxRiskScore = 100,
            RequiredFactors = "[\"totp\",\"sms\"]",
            RequiredFactorCount = 2,
            StepUpValidityMinutes = 15,
            Action = "step_up",
            IsActive = true,
            Priority = 100
        };

        _context.Users.Add(user);
        _context.AdaptiveAuthPolicies.Add(policy);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.EvaluatePolicyAsync(userId, highRiskScore);

        // Assert
        Assert.Equal("step_up", result.Action);
        Assert.Equal(highRiskScore, result.RiskScore);
        Assert.Equal("high", result.RiskLevel);
        Assert.Equal(policy.Id, result.PolicyId);
        Assert.Equal("High Risk Policy", result.PolicyName);
        Assert.Equal(2, result.RequiredFactors.Count);
        Assert.Contains("totp", result.RequiredFactors);
        Assert.Contains("sms", result.RequiredFactors);
        Assert.Equal(2, result.RequiredFactorCount);
        Assert.Equal(15, result.StepUpValidityMinutes);
    }

    [Fact]
    public async Task EvaluatePolicyAsync_MultipleMatchingPolicies_UsesHighestPriority()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var riskScore = 60;

        var user = new ApplicationUser { Id = userId, Email = "user@example.com", UserName = "testuser", PasswordHash = "dummy-hash" };

        // Lower priority policy
        var lowPriorityPolicy = new AdaptiveAuthPolicy
        {
            Id = Guid.NewGuid(),
            Name = "Low Priority Policy",
            MinRiskScore = 50,
            MaxRiskScore = 100,
            RequiredFactors = "[\"totp\"]",
            RequiredFactorCount = 1,
            Action = "step_up",
            IsActive = true,
            Priority = 50
        };

        // Higher priority policy
        var highPriorityPolicy = new AdaptiveAuthPolicy
        {
            Id = Guid.NewGuid(),
            Name = "High Priority Policy",
            MinRiskScore = 50,
            MaxRiskScore = 100,
            RequiredFactors = "[\"totp\",\"sms\"]",
            RequiredFactorCount = 2,
            Action = "step_up",
            IsActive = true,
            Priority = 100
        };

        _context.Users.Add(user);
        _context.AdaptiveAuthPolicies.AddRange(lowPriorityPolicy, highPriorityPolicy);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.EvaluatePolicyAsync(userId, riskScore);

        // Assert
        Assert.Equal(highPriorityPolicy.Id, result.PolicyId);
        Assert.Equal("High Priority Policy", result.PolicyName);
        Assert.Equal(2, result.RequiredFactorCount);
    }

    [Fact]
    public async Task InitiateStepUpAsync_CreatesStepUpSession()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new ApplicationUser { Id = userId, Email = "user@example.com", UserName = "testuser", PasswordHash = "dummy-hash" };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var requiredFactors = new List<string> { "totp", "sms" };
        var validityMinutes = 15;

        // Act
        var result = await _service.InitiateStepUpAsync(
            userId,
            requiredFactors,
            "/api/v1/secrets/read",
            validityMinutes);

        // Assert
        Assert.NotEmpty(result.SessionToken);
        Assert.Equal(requiredFactors, result.RequiredFactors);
        Assert.Equal(2, result.RequiredFactorCount);
        Assert.Equal(validityMinutes, result.ValidityMinutes);
        Assert.Equal("/api/v1/secrets/read", result.ResourcePath);
        Assert.True(result.ExpiresAt > DateTime.UtcNow);
        Assert.NotNull(result.ChallengeData);

        // Verify session was saved to database
        var session = await _context.StepUpSessions.FirstOrDefaultAsync(s => s.SessionToken == result.SessionToken);
        Assert.NotNull(session);
        Assert.Equal(userId, session.UserId);
        Assert.False(session.IsCompleted);
        Assert.True(session.IsValid);
    }

    [Fact]
    public async Task ValidateStepUpFactorAsync_ValidTotpCode_ReturnsSuccess()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new ApplicationUser { Id = userId, Email = "user@example.com", UserName = "testuser", PasswordHash = "dummy-hash" };

        var session = new StepUpSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SessionToken = "test-token-123",
            CompletedFactors = "[]",
            ExpiresAt = DateTime.UtcNow.AddMinutes(15),
            IsValid = true,
            IsCompleted = false
        };

        _context.Users.Add(user);
        _context.StepUpSessions.Add(session);
        await _context.SaveChangesAsync();

        // Mock MFA service to return success
        _mfaServiceMock.Setup(m => m.VerifyTotpCodeAsync(userId, "123456"))
            .ReturnsAsync(true);

        // Act
        var result = await _service.ValidateStepUpFactorAsync("test-token-123", userId, "totp", "123456");

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal("totp", result.Factor);
        Assert.Contains("totp", result.CompletedFactors);
        Assert.Null(result.ErrorMessage);

        // Verify session was updated
        var updatedSession = await _context.StepUpSessions.FindAsync(session.Id);
        Assert.NotNull(updatedSession);
        Assert.Contains("totp", System.Text.Json.JsonSerializer.Deserialize<List<string>>(updatedSession.CompletedFactors)!);
    }

    [Fact]
    public async Task ValidateStepUpFactorAsync_InvalidCode_ReturnsFailure()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new ApplicationUser { Id = userId, Email = "user@example.com", UserName = "testuser", PasswordHash = "dummy-hash" };

        var session = new StepUpSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SessionToken = "test-token-456",
            CompletedFactors = "[]",
            ExpiresAt = DateTime.UtcNow.AddMinutes(15),
            IsValid = true,
            IsCompleted = false
        };

        _context.Users.Add(user);
        _context.StepUpSessions.Add(session);
        await _context.SaveChangesAsync();

        // Mock MFA service to return failure
        _mfaServiceMock.Setup(m => m.VerifyTotpCodeAsync(userId, "wrong-code"))
            .ReturnsAsync(false);

        // Act
        var result = await _service.ValidateStepUpFactorAsync("test-token-456", userId, "totp", "wrong-code");

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("totp", result.Factor);
        Assert.Equal("Invalid totp credential", result.ErrorMessage);
    }

    [Fact]
    public async Task ValidateStepUpFactorAsync_ExpiredSession_ReturnsFailure()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new ApplicationUser { Id = userId, Email = "user@example.com", UserName = "testuser", PasswordHash = "dummy-hash" };

        var expiredSession = new StepUpSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SessionToken = "expired-token",
            CompletedFactors = "[]",
            ExpiresAt = DateTime.UtcNow.AddMinutes(-5), // Expired 5 minutes ago
            IsValid = true,
            IsCompleted = false
        };

        _context.Users.Add(user);
        _context.StepUpSessions.Add(expiredSession);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.ValidateStepUpFactorAsync("expired-token", userId, "totp", "123456");

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("Session expired or invalid", result.ErrorMessage);
    }

    [Fact]
    public async Task CompleteStepUpAsync_ValidSession_MarksAsCompleted()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new ApplicationUser { Id = userId, Email = "user@example.com", UserName = "testuser", PasswordHash = "dummy-hash" };

        var session = new StepUpSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SessionToken = "complete-token",
            CompletedFactors = "[\"totp\",\"sms\"]",
            ExpiresAt = DateTime.UtcNow.AddMinutes(15),
            IsValid = true,
            IsCompleted = false,
            ResourcePath = "/api/v1/admin"
        };

        _context.Users.Add(user);
        _context.StepUpSessions.Add(session);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.CompleteStepUpAsync("complete-token", userId);

        // Assert
        Assert.True(result.IsCompleted);
        Assert.Equal("complete-token", result.SessionToken);
        Assert.Equal(2, result.CompletedFactors.Count);
        Assert.Contains("totp", result.CompletedFactors);
        Assert.Contains("sms", result.CompletedFactors);
        Assert.Equal("/api/v1/admin", result.ResourcePath);

        // Verify session was marked as completed
        var updatedSession = await _context.StepUpSessions.FindAsync(session.Id);
        Assert.NotNull(updatedSession);
        Assert.True(updatedSession.IsCompleted);
        Assert.NotNull(updatedSession.CompletedAt);
    }

    [Fact]
    public async Task HasValidStepUpSessionAsync_WithValidSession_ReturnsTrue()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new ApplicationUser { Id = userId, Email = "user@example.com", UserName = "testuser", PasswordHash = "dummy-hash" };

        var validSession = new StepUpSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SessionToken = "valid-session",
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
            IsValid = true,
            IsCompleted = true,
            ResourcePath = "/api/v1/secrets"
        };

        _context.Users.Add(user);
        _context.StepUpSessions.Add(validSession);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.HasValidStepUpSessionAsync(userId, "/api/v1/secrets");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task HasValidStepUpSessionAsync_NoValidSession_ReturnsFalse()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var result = await _service.HasValidStepUpSessionAsync(userId, "/api/v1/secrets");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task RecordAuthenticationEventAsync_CreatesEvent()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new ApplicationUser { Id = userId, Email = "user@example.com", UserName = "testuser", PasswordHash = "dummy-hash" };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var factorsUsed = new List<string> { "password", "totp" };
        var metadata = new Dictionary<string, string>
        {
            { "ip_address", "192.168.1.1" },
            { "user_agent", "Mozilla/5.0" },
            { "location", "New York, US" }
        };

        // Act
        await _service.RecordAuthenticationEventAsync(
            userId,
            "login",
            45,
            "success",
            factorsUsed,
            null,
            metadata);

        // Assert
        var events = await _context.AuthenticationEvents
            .Where(e => e.UserId == userId)
            .ToListAsync();

        Assert.Single(events);
        var evt = events[0];
        Assert.Equal("login", evt.EventType);
        Assert.Equal(45, evt.RiskScore);
        Assert.Equal("medium", evt.RiskLevel);
        Assert.Equal("success", evt.Outcome);
        Assert.Equal("192.168.1.1", evt.IpAddress);
        Assert.Equal("Mozilla/5.0", evt.UserAgent);
        Assert.Equal("New York, US", evt.Location);
    }

    [Fact]
    public async Task GetAuthenticationEventsAsync_ReturnsFilteredEvents()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new ApplicationUser { Id = userId, Email = "user@example.com", UserName = "testuser", PasswordHash = "dummy-hash" };

        var event1 = new AuthenticationEvent
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            EventType = "login",
            RiskScore = 30,
            RiskLevel = "low",
            Outcome = "success",
            FactorsUsed = "[\"password\",\"totp\"]",
            EventTime = DateTime.UtcNow.AddHours(-1)
        };

        var event2 = new AuthenticationEvent
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            EventType = "step_up",
            RiskScore = 70,
            RiskLevel = "high",
            Outcome = "success",
            FactorsUsed = "[\"totp\",\"sms\"]",
            EventTime = DateTime.UtcNow.AddMinutes(-30)
        };

        _context.Users.Add(user);
        _context.AuthenticationEvents.AddRange(event1, event2);
        await _context.SaveChangesAsync();

        // Act
        var allEvents = await _service.GetAuthenticationEventsAsync(userId);
        var stepUpEvents = await _service.GetAuthenticationEventsAsync(userId, eventType: "step_up");

        // Assert
        Assert.Equal(2, allEvents.Count);
        Assert.Single(stepUpEvents);
        Assert.Equal("step_up", stepUpEvents[0].EventType);
    }

    [Fact]
    public async Task CreateOrUpdatePolicyAsync_CreatesNewPolicy()
    {
        // Arrange
        var request = new CreateAdaptiveAuthPolicyDto
        {
            Name = "Test Policy",
            Description = "Test policy description",
            MinRiskScore = 50,
            MaxRiskScore = 100,
            RequiredFactors = new List<string> { "totp", "sms" },
            RequiredFactorCount = 2,
            StepUpValidityMinutes = 20,
            Action = "step_up",
            IsActive = true,
            Priority = 100
        };

        // Act
        var result = await _service.CreateOrUpdatePolicyAsync(request);

        // Assert
        Assert.NotEqual(Guid.Empty, result.PolicyId);
        Assert.Equal("Test Policy", result.Name);
        Assert.Equal(50, result.MinRiskScore);
        Assert.Equal(100, result.MaxRiskScore);
        Assert.Equal(2, result.RequiredFactors.Count);
        Assert.True(result.IsActive);

        // Verify saved to database
        var policy = await _context.AdaptiveAuthPolicies.FindAsync(result.PolicyId);
        Assert.NotNull(policy);
        Assert.Equal("Test Policy", policy.Name);
    }

    [Fact]
    public async Task CreateOrUpdatePolicyAsync_UpdatesExistingPolicy()
    {
        // Arrange
        var existingPolicy = new AdaptiveAuthPolicy
        {
            Id = Guid.NewGuid(),
            Name = "Original Name",
            Description = "Original description",
            MinRiskScore = 30,
            MaxRiskScore = 60,
            RequiredFactors = "[\"totp\"]",
            RequiredFactorCount = 1,
            StepUpValidityMinutes = 15,
            Action = "step_up",
            IsActive = true,
            Priority = 50
        };

        _context.AdaptiveAuthPolicies.Add(existingPolicy);
        await _context.SaveChangesAsync();

        var updateRequest = new CreateAdaptiveAuthPolicyDto
        {
            PolicyId = existingPolicy.Id,
            Name = "Updated Name",
            Description = "Updated description",
            MinRiskScore = 40,
            MaxRiskScore = 80,
            RequiredFactors = new List<string> { "totp", "sms" },
            RequiredFactorCount = 2,
            StepUpValidityMinutes = 25,
            Action = "step_up",
            IsActive = false,
            Priority = 75
        };

        // Act
        var result = await _service.CreateOrUpdatePolicyAsync(updateRequest);

        // Assert
        Assert.Equal(existingPolicy.Id, result.PolicyId);
        Assert.Equal("Updated Name", result.Name);
        Assert.Equal(40, result.MinRiskScore);
        Assert.Equal(80, result.MaxRiskScore);
        Assert.Equal(2, result.RequiredFactors.Count);
        Assert.False(result.IsActive);
        Assert.Equal(75, result.Priority);
    }

    [Fact]
    public async Task GetActivePoliciesAsync_ReturnsOnlyActivePolicies()
    {
        // Arrange
        var activePolicy1 = new AdaptiveAuthPolicy
        {
            Id = Guid.NewGuid(),
            Name = "Active Policy 1",
            MinRiskScore = 50,
            MaxRiskScore = 100,
            RequiredFactors = "[\"totp\"]",
            RequiredFactorCount = 1,
            Action = "step_up",
            IsActive = true,
            Priority = 100
        };

        var activePolicy2 = new AdaptiveAuthPolicy
        {
            Id = Guid.NewGuid(),
            Name = "Active Policy 2",
            MinRiskScore = 30,
            MaxRiskScore = 60,
            RequiredFactors = "[\"sms\"]",
            RequiredFactorCount = 1,
            Action = "step_up",
            IsActive = true,
            Priority = 50
        };

        var inactivePolicy = new AdaptiveAuthPolicy
        {
            Id = Guid.NewGuid(),
            Name = "Inactive Policy",
            MinRiskScore = 0,
            MaxRiskScore = 100,
            RequiredFactors = "[]",
            RequiredFactorCount = 0,
            Action = "allow",
            IsActive = false,
            Priority = 10
        };

        _context.AdaptiveAuthPolicies.AddRange(activePolicy1, activePolicy2, inactivePolicy);
        await _context.SaveChangesAsync();

        // Act
        var policies = await _service.GetActivePoliciesAsync();

        // Assert
        Assert.Equal(2, policies.Count);
        Assert.All(policies, p => Assert.True(p.IsActive));
        Assert.Equal("Active Policy 1", policies[0].Name); // Higher priority first
        Assert.Equal("Active Policy 2", policies[1].Name);
    }

    [Fact]
    public async Task DeletePolicyAsync_RemovesPolicy()
    {
        // Arrange
        var policy = new AdaptiveAuthPolicy
        {
            Id = Guid.NewGuid(),
            Name = "To Delete",
            MinRiskScore = 0,
            MaxRiskScore = 100,
            RequiredFactors = "[]",
            RequiredFactorCount = 0,
            Action = "allow",
            IsActive = true,
            Priority = 100
        };

        _context.AdaptiveAuthPolicies.Add(policy);
        await _context.SaveChangesAsync();

        // Act
        await _service.DeletePolicyAsync(policy.Id);

        // Assert
        var deletedPolicy = await _context.AdaptiveAuthPolicies.FindAsync(policy.Id);
        Assert.Null(deletedPolicy);
    }

    [Fact]
    public async Task GetAuthenticationStatisticsAsync_CalculatesCorrectStats()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new ApplicationUser { Id = userId, Email = "user@example.com", UserName = "testuser", PasswordHash = "dummy-hash" };

        var events = new List<AuthenticationEvent>
        {
            new() { Id = Guid.NewGuid(), UserId = userId, EventType = "login", RiskScore = 30, Outcome = "success", FactorsUsed = "[\"password\",\"totp\"]", IsTrustedDevice = true, EventTime = DateTime.UtcNow.AddDays(-1) },
            new() { Id = Guid.NewGuid(), UserId = userId, EventType = "login", RiskScore = 85, Outcome = "failure", FactorsUsed = "[\"password\"]", IsTrustedDevice = false, EventTime = DateTime.UtcNow.AddDays(-2) },
            new() { Id = Guid.NewGuid(), UserId = userId, EventType = "step_up", RiskScore = 70, Outcome = "success", FactorsUsed = "[\"totp\",\"sms\"]", IsTrustedDevice = false, EventTime = DateTime.UtcNow.AddDays(-3) },
            new() { Id = Guid.NewGuid(), UserId = userId, EventType = "step_up", RiskScore = 75, Outcome = "failure", FactorsUsed = "[\"totp\"]", IsTrustedDevice = false, EventTime = DateTime.UtcNow.AddDays(-5) }
        };

        _context.Users.Add(user);
        _context.AuthenticationEvents.AddRange(events);
        await _context.SaveChangesAsync();

        // Act
        var stats = await _service.GetAuthenticationStatisticsAsync(userId, days: 30);

        // Assert
        Assert.Equal(userId, stats.UserId);
        Assert.Equal(4, stats.TotalAuthenticationEvents);
        Assert.Equal(1, stats.SuccessfulLogins);
        Assert.Equal(1, stats.FailedLogins);
        Assert.Equal(2, stats.StepUpChallenges);
        Assert.Equal(1, stats.StepUpSuccesses);
        Assert.Equal(1, stats.StepUpFailures);
        Assert.Equal(65.0, stats.AverageRiskScore); // (30 + 85 + 70 + 75) / 4
        Assert.Equal(2, stats.HighRiskEvents); // Risk > 70: 85, 75 (70 is not > 70)
        Assert.Equal(1, stats.TrustedDeviceLogins);
        Assert.Equal(3, stats.NewDeviceLogins);
        Assert.NotNull(stats.LastAuthenticationEvent);
        Assert.NotNull(stats.LastStepUpChallenge);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
