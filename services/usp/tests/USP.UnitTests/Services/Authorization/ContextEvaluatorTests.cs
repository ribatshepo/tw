using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using USP.Core.Models.Entities;
using USP.Core.Services.Authorization;
using USP.Infrastructure.Data;
using USP.Infrastructure.Services.Authorization;
using Xunit;

namespace USP.UnitTests.Services.Authorization;

public class ContextEvaluatorTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<ILogger<ContextEvaluator>> _loggerMock;
    private readonly ContextEvaluator _evaluator;
    private readonly Guid _testUserId;

    public ContextEvaluatorTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;

        _context = new ApplicationDbContext(options);
        _loggerMock = new Mock<ILogger<ContextEvaluator>>();
        _evaluator = new ContextEvaluator(_context, _loggerMock.Object);

        _testUserId = Guid.NewGuid();
        SeedTestData();
    }

    private void SeedTestData()
    {
        var user = new ApplicationUser
        {
            Id = _testUserId,
            UserName = "test.user",
            Email = "test@example.com",
            Status = "active",
            CreatedAt = DateTime.UtcNow
        };

        var riskProfile = new UserRiskProfile
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            CurrentRiskScore = 30,
            LastCalculatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        _context.UserRiskProfiles.Add(riskProfile);
        _context.SaveChanges();
    }

    [Fact]
    public async Task EvaluateContextAsync_ShouldAllow_WhenNoContextPolicy()
    {
        // Arrange
        var request = new ContextEvaluationRequest
        {
            UserId = _testUserId,
            Action = "read",
            ResourceType = "secret"
        };

        // Act
        var result = await _evaluator.EvaluateContextAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Decision.Should().Be("allow");
        result.Allowed.Should().BeTrue();
        result.Reasons.Should().Contain("No context policy defined");
    }

    [Fact]
    public async Task IsTimeBasedAccessAllowedAsync_ShouldAllow_DuringBusinessHours()
    {
        // Arrange
        await _evaluator.CreateContextPolicyAsync(new CreateContextPolicyRequest
        {
            ResourceType = "secret",
            EnableTimeRestriction = true,
            AllowedDaysOfWeek = "Monday,Tuesday,Wednesday,Thursday,Friday",
            AllowedStartTime = new TimeSpan(9, 0, 0),
            AllowedEndTime = new TimeSpan(17, 0, 0)
        });

        var businessHourTime = new DateTime(2025, 12, 26, 14, 0, 0, DateTimeKind.Utc); // Thursday 2 PM

        // Act
        var allowed = await _evaluator.IsTimeBasedAccessAllowedAsync(_testUserId, "secret", businessHourTime);

        // Assert
        allowed.Should().BeTrue();
    }

    [Fact]
    public async Task IsTimeBasedAccessAllowedAsync_ShouldDeny_OutsideBusinessHours()
    {
        // Arrange
        await _evaluator.CreateContextPolicyAsync(new CreateContextPolicyRequest
        {
            ResourceType = "secret",
            EnableTimeRestriction = true,
            AllowedDaysOfWeek = "Monday,Tuesday,Wednesday,Thursday,Friday",
            AllowedStartTime = new TimeSpan(9, 0, 0),
            AllowedEndTime = new TimeSpan(17, 0, 0)
        });

        var afterHoursTime = new DateTime(2025, 12, 26, 20, 0, 0, DateTimeKind.Utc); // Thursday 8 PM

        // Act
        var allowed = await _evaluator.IsTimeBasedAccessAllowedAsync(_testUserId, "secret", afterHoursTime);

        // Assert
        allowed.Should().BeFalse();
    }

    [Fact]
    public async Task IsTimeBasedAccessAllowedAsync_ShouldDeny_OnWeekend()
    {
        // Arrange
        await _evaluator.CreateContextPolicyAsync(new CreateContextPolicyRequest
        {
            ResourceType = "secret",
            EnableTimeRestriction = true,
            AllowedDaysOfWeek = "Monday,Tuesday,Wednesday,Thursday,Friday",
            AllowedStartTime = new TimeSpan(9, 0, 0),
            AllowedEndTime = new TimeSpan(17, 0, 0)
        });

        var weekendTime = new DateTime(2025, 12, 27, 14, 0, 0, DateTimeKind.Utc); // Saturday

        // Act
        var allowed = await _evaluator.IsTimeBasedAccessAllowedAsync(_testUserId, "secret", weekendTime);

        // Assert
        allowed.Should().BeFalse();
    }

    [Fact]
    public async Task IsLocationBasedAccessAllowedAsync_ShouldAllow_FromAllowedCountry()
    {
        // Arrange
        await _evaluator.CreateContextPolicyAsync(new CreateContextPolicyRequest
        {
            ResourceType = "secret",
            EnableLocationRestriction = true,
            AllowedCountries = new List<string> { "US", "CA", "UK" }
        });

        // Act
        var allowed = await _evaluator.IsLocationBasedAccessAllowedAsync(_testUserId, "secret", "US/Seattle");

        // Assert
        allowed.Should().BeTrue();
    }

    [Fact]
    public async Task IsLocationBasedAccessAllowedAsync_ShouldDeny_FromDeniedCountry()
    {
        // Arrange
        await _evaluator.CreateContextPolicyAsync(new CreateContextPolicyRequest
        {
            ResourceType = "secret",
            EnableLocationRestriction = true,
            DeniedCountries = new List<string> { "XX", "Unknown" }
        });

        // Act
        var allowed = await _evaluator.IsLocationBasedAccessAllowedAsync(_testUserId, "secret", "XX");

        // Assert
        allowed.Should().BeFalse();
    }

    [Fact]
    public async Task IsDeviceCompliantAsync_ShouldReturnTrue_ForTrustedDevice()
    {
        // Arrange
        var device = new TrustedDevice
        {
            Id = Guid.NewGuid(),
            UserId = _testUserId,
            DeviceId = "device-123",
            IsTrusted = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.TrustedDevices.Add(device);
        await _context.SaveChangesAsync();

        // Act
        var compliant = await _evaluator.IsDeviceCompliantAsync(_testUserId, "device-123");

        // Assert
        compliant.Should().BeTrue();
    }

    [Fact]
    public async Task IsDeviceCompliantAsync_ShouldReturnFalse_ForUnknownDevice()
    {
        // Act
        var compliant = await _evaluator.IsDeviceCompliantAsync(_testUserId, "unknown-device");

        // Assert
        compliant.Should().BeFalse();
    }

    [Fact]
    public async Task CalculateAccessRiskScoreAsync_ShouldIncludeUserRiskScore()
    {
        // Arrange
        var request = new ContextEvaluationRequest
        {
            UserId = _testUserId,
            Action = "read",
            ResourceType = "secret"
        };

        // Act
        var riskScore = await _evaluator.CalculateAccessRiskScoreAsync(request);

        // Assert
        riskScore.Should().BeGreaterOrEqualTo(30); // User's base risk score
    }

    [Fact]
    public async Task CalculateAccessRiskScoreAsync_ShouldIncreaseForNonCompliantDevice()
    {
        // Arrange
        var request = new ContextEvaluationRequest
        {
            UserId = _testUserId,
            Action = "read",
            ResourceType = "secret",
            DeviceCompliant = false
        };

        // Act
        var riskScore = await _evaluator.CalculateAccessRiskScoreAsync(request);

        // Assert
        riskScore.Should().BeGreaterThan(30); // Base risk + device non-compliance penalty
    }

    [Fact]
    public async Task CalculateAccessRiskScoreAsync_ShouldIncreaseForImpossibleTravel()
    {
        // Arrange
        var request = new ContextEvaluationRequest
        {
            UserId = _testUserId,
            Action = "read",
            ResourceType = "secret",
            ImpossibleTravel = true
        };

        // Act
        var riskScore = await _evaluator.CalculateAccessRiskScoreAsync(request);

        // Assert
        riskScore.Should().BeGreaterThan(60); // Significant penalty for impossible travel
    }

    [Fact]
    public async Task EvaluateContextAsync_ShouldRequireMfa_OnHighRisk()
    {
        // Arrange
        await _evaluator.CreateContextPolicyAsync(new CreateContextPolicyRequest
        {
            ResourceType = "secret",
            EnableRiskRestriction = true,
            RequireMfaOnHighRisk = true,
            HighRiskThreshold = 50
        });

        var request = new ContextEvaluationRequest
        {
            UserId = _testUserId,
            Action = "delete",
            ResourceType = "secret",
            UserRiskScore = 75,
            NetworkZone = "external"
        };

        // Act
        var result = await _evaluator.EvaluateContextAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.RiskScore.Should().BeGreaterThan(50);
        result.RequiredAction.Should().Be("mfa");
        result.Reasons.Should().Contain(r => r.Contains("MFA required"));
    }

    [Fact]
    public async Task EvaluateContextAsync_ShouldDeny_WhenMaxRiskExceeded()
    {
        // Arrange
        await _evaluator.CreateContextPolicyAsync(new CreateContextPolicyRequest
        {
            ResourceType = "secret",
            EnableRiskRestriction = true,
            MaxAllowedRiskScore = 50
        });

        var request = new ContextEvaluationRequest
        {
            UserId = _testUserId,
            Action = "delete",
            ResourceType = "secret",
            ImpossibleTravel = true, // This will push risk score high
            DeviceCompliant = false,
            NetworkZone = "external"
        };

        // Act
        var result = await _evaluator.EvaluateContextAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Allowed.Should().BeFalse();
        result.Reasons.Should().Contain(r => r.Contains("risk score") && r.Contains("exceeds maximum"));
    }

    [Fact]
    public async Task EvaluateContextAsync_ShouldProvideDetailedContextChecks()
    {
        // Arrange
        await _evaluator.CreateContextPolicyAsync(new CreateContextPolicyRequest
        {
            ResourceType = "secret",
            EnableTimeRestriction = true,
            AllowedDaysOfWeek = "Monday,Tuesday,Wednesday,Thursday,Friday",
            AllowedStartTime = new TimeSpan(9, 0, 0),
            AllowedEndTime = new TimeSpan(17, 0, 0),
            EnableLocationRestriction = true,
            AllowedCountries = new List<string> { "US" },
            AllowedNetworkZones = new List<string> { "internal", "vpn" },
            EnableDeviceRestriction = true,
            RequireCompliantDevice = true
        });

        var request = new ContextEvaluationRequest
        {
            UserId = _testUserId,
            Action = "read",
            ResourceType = "secret",
            RequestTime = new DateTime(2025, 12, 26, 14, 0, 0, DateTimeKind.Utc), // Thursday 2 PM
            GeoLocation = "US/Seattle",
            NetworkZone = "internal",
            DeviceCompliant = true
        };

        // Act
        var result = await _evaluator.EvaluateContextAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.ContextChecks.Should().ContainKey("time_based");
        result.ContextChecks.Should().ContainKey("location_based");
        result.ContextChecks.Should().ContainKey("network_zone");
        result.ContextChecks.Should().ContainKey("device_compliant");
        result.ContextChecks.Values.Should().AllBe(v => v == true);
        result.Allowed.Should().BeTrue();
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
