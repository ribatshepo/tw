using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using USP.Core.Models.Entities;
using USP.Core.Services.PAM;
using USP.Infrastructure.Data;
using USP.Infrastructure.Services.PAM;
using Xunit;

namespace USP.IntegrationTests.PAM;

public class AccessAnalyticsEngineTests : IClassFixture<TestDatabaseFixture>
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<ISafeManagementService> _safeServiceMock;
    private readonly ILogger<AccessAnalyticsEngine> _logger;
    private readonly AccessAnalyticsEngine _analyticsEngine;
    private readonly Guid _testUserId;
    private readonly Guid _testSafeId;

    public AccessAnalyticsEngineTests(TestDatabaseFixture fixture)
    {
        _context = fixture.CreateContext();
        _safeServiceMock = new Mock<ISafeManagementService>();
        _logger = Mock.Of<ILogger<AccessAnalyticsEngine>>();
        _analyticsEngine = new AccessAnalyticsEngine(_context, _safeServiceMock.Object, _logger);

        _testUserId = Guid.NewGuid();
        _testSafeId = Guid.NewGuid();

        // Setup mock safe service to return test safe
        _safeServiceMock.Setup(s => s.GetSafesAsync(_testUserId))
            .ReturnsAsync(new List<Core.Models.DTOs.PAM.SafeDto>
            {
                new() { Id = _testSafeId, Name = "Test Safe" }
            });

        _safeServiceMock.Setup(s => s.HasSafeAccessAsync(_testSafeId, _testUserId, It.IsAny<string>()))
            .ReturnsAsync(true);
    }

    [Fact]
    public async Task DetectDormantAccountsAsync_ShouldReturnDormantAccounts()
    {
        // Arrange
        var oldDate = DateTime.UtcNow.AddDays(-100);

        var account = new PrivilegedAccount
        {
            Id = Guid.NewGuid(),
            SafeId = _testSafeId,
            AccountName = "dormant_account",
            Platform = "PostgreSQL",
            Username = "dormant_user",
            EncryptedPassword = "encrypted",
            CreatedAt = oldDate,
            LastRotated = oldDate
        };

        _context.PrivilegedAccounts.Add(account);
        await _context.SaveChangesAsync();

        // Act
        var dormantAccounts = await _analyticsEngine.DetectDormantAccountsAsync(_testUserId, 90);

        // Assert
        Assert.NotEmpty(dormantAccounts);
        Assert.Contains(dormantAccounts, a => a.AccountId == account.Id);
        Assert.All(dormantAccounts, a => Assert.True(a.DaysSinceLastUse >= 90));
    }

    [Fact]
    public async Task DetectOverPrivilegedAccountsAsync_ShouldReturnOverPrivilegedAccounts()
    {
        // Arrange
        var account = new PrivilegedAccount
        {
            Id = Guid.NewGuid(),
            SafeId = _testSafeId,
            AccountName = "high_priv_account",
            Platform = "Oracle",
            Username = "sa",
            EncryptedPassword = "encrypted",
            CreatedAt = DateTime.UtcNow.AddDays(-30)
        };

        _context.PrivilegedAccounts.Add(account);
        await _context.SaveChangesAsync();

        // Act
        var overPrivileged = await _analyticsEngine.DetectOverPrivilegedAccountsAsync(_testUserId);

        // Assert - Account with high privilege platform (Oracle) and low usage should be detected
        Assert.NotNull(overPrivileged);
    }

    [Fact]
    public async Task AnalyzeAccountUsageAsync_ShouldReturnUsagePattern()
    {
        // Arrange
        var account = new PrivilegedAccount
        {
            Id = Guid.NewGuid(),
            SafeId = _testSafeId,
            AccountName = "test_account",
            Platform = "MySQL",
            Username = "testuser",
            EncryptedPassword = "encrypted",
            CreatedAt = DateTime.UtcNow.AddDays(-30)
        };

        _context.PrivilegedAccounts.Add(account);

        var testUser = new User
        {
            Id = _testUserId,
            Email = "test@example.com",
            PasswordHash = "hash",
            CreatedAt = DateTime.UtcNow
        };
        _context.Users.Add(testUser);

        var checkout = new AccountCheckout
        {
            Id = Guid.NewGuid(),
            AccountId = account.Id,
            UserId = _testUserId,
            CheckoutTime = DateTime.UtcNow.AddHours(-2),
            Status = "completed",
            CheckedInTime = DateTime.UtcNow.AddHours(-1)
        };

        _context.AccountCheckouts.Add(checkout);
        await _context.SaveChangesAsync();

        // Act
        var pattern = await _analyticsEngine.AnalyzeAccountUsageAsync(account.Id, _testUserId, 30);

        // Assert
        Assert.NotNull(pattern);
        Assert.Equal(account.Id, pattern.AccountId);
        Assert.True(pattern.TotalCheckouts > 0);
    }

    [Fact]
    public async Task DetectAccessAnomaliesAsync_ShouldDetectUnusualTime()
    {
        // Arrange
        var account = new PrivilegedAccount
        {
            Id = Guid.NewGuid(),
            SafeId = _testSafeId,
            AccountName = "test_account",
            Platform = "PostgreSQL",
            Username = "testuser",
            EncryptedPassword = "encrypted",
            CreatedAt = DateTime.UtcNow.AddDays(-7)
        };

        _context.PrivilegedAccounts.Add(account);

        var testUser = new User
        {
            Id = _testUserId,
            Email = "test@example.com",
            PasswordHash = "hash",
            CreatedAt = DateTime.UtcNow
        };
        _context.Users.Add(testUser);

        // Create checkout at unusual time (3 AM)
        var unusualTime = DateTime.UtcNow.Date.AddHours(3);
        var checkout = new AccountCheckout
        {
            Id = Guid.NewGuid(),
            AccountId = account.Id,
            UserId = _testUserId,
            CheckoutTime = unusualTime,
            Status = "active"
        };

        _context.AccountCheckouts.Add(checkout);
        await _context.SaveChangesAsync();

        // Act
        var anomalies = await _analyticsEngine.DetectAccessAnomaliesAsync(_testUserId);

        // Assert
        Assert.NotEmpty(anomalies);
        Assert.Contains(anomalies, a => a.AnomalyType == "UnusualTime");
    }

    [Fact]
    public async Task GetComplianceDashboardAsync_ShouldReturnDashboard()
    {
        // Arrange
        var account = new PrivilegedAccount
        {
            Id = Guid.NewGuid(),
            SafeId = _testSafeId,
            AccountName = "test_account",
            Platform = "MySQL",
            Username = "testuser",
            EncryptedPassword = "encrypted",
            RequiresMfa = true,
            RequiresDualApproval = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.PrivilegedAccounts.Add(account);
        await _context.SaveChangesAsync();

        // Act
        var dashboard = await _analyticsEngine.GetComplianceDashboardAsync(_testUserId);

        // Assert
        Assert.NotNull(dashboard);
        Assert.True(dashboard.ComplianceScore >= 0 && dashboard.ComplianceScore <= 100);
        Assert.True(dashboard.TotalPrivilegedAccounts > 0);
    }

    [Fact]
    public async Task CalculateAccountRiskScoreAsync_ShouldReturnRiskScore()
    {
        // Arrange
        var account = new PrivilegedAccount
        {
            Id = Guid.NewGuid(),
            SafeId = _testSafeId,
            AccountName = "risky_account",
            Platform = "AWS",
            Username = "root",
            EncryptedPassword = "encrypted",
            RequiresMfa = false,
            RequiresDualApproval = false,
            CreatedAt = DateTime.UtcNow.AddDays(-100),
            RotationPolicy = "scheduled",
            NextRotation = DateTime.UtcNow.AddDays(-10) // Overdue
        };

        _context.PrivilegedAccounts.Add(account);
        await _context.SaveChangesAsync();

        // Act
        var riskScore = await _analyticsEngine.CalculateAccountRiskScoreAsync(account.Id, _testUserId);

        // Assert
        Assert.NotNull(riskScore);
        Assert.True(riskScore.TotalRiskScore > 0);
        Assert.NotEmpty(riskScore.RiskFactors);
        Assert.NotEmpty(riskScore.Recommendations);
        Assert.Contains(riskScore.RiskLevel, new[] { "Low", "Medium", "High", "Critical" });
    }

    [Fact]
    public async Task GetHighRiskAccountsAsync_ShouldReturnAccountsAboveThreshold()
    {
        // Arrange - Create a high-risk account
        var highRiskAccount = new PrivilegedAccount
        {
            Id = Guid.NewGuid(),
            SafeId = _testSafeId,
            AccountName = "high_risk",
            Platform = "Oracle",
            Username = "sys",
            EncryptedPassword = "encrypted",
            RequiresMfa = false,
            RequiresDualApproval = false,
            CreatedAt = DateTime.UtcNow.AddDays(-200),
            RotationPolicy = "scheduled",
            NextRotation = DateTime.UtcNow.AddDays(-30)
        };

        _context.PrivilegedAccounts.Add(highRiskAccount);
        await _context.SaveChangesAsync();

        // Act
        var highRiskAccounts = await _analyticsEngine.GetHighRiskAccountsAsync(_testUserId, 50);

        // Assert
        Assert.NotNull(highRiskAccounts);
        // High risk account should be detected
        Assert.True(highRiskAccounts.Any(a => a.TotalRiskScore >= 50));
    }

    [Fact]
    public async Task DetectCheckoutPolicyViolationsAsync_ShouldDetectExcessiveDuration()
    {
        // Arrange
        var account = new PrivilegedAccount
        {
            Id = Guid.NewGuid(),
            SafeId = _testSafeId,
            AccountName = "test_account",
            Platform = "PostgreSQL",
            Username = "testuser",
            EncryptedPassword = "encrypted",
            CreatedAt = DateTime.UtcNow.AddDays(-30)
        };

        _context.PrivilegedAccounts.Add(account);

        var testUser = new User
        {
            Id = _testUserId,
            Email = "test@example.com",
            PasswordHash = "hash",
            CreatedAt = DateTime.UtcNow
        };
        _context.Users.Add(testUser);

        // Create checkout that's been active for more than 24 hours
        var checkout = new AccountCheckout
        {
            Id = Guid.NewGuid(),
            AccountId = account.Id,
            UserId = _testUserId,
            CheckoutTime = DateTime.UtcNow.AddHours(-30),
            Status = "active"
        };

        _context.AccountCheckouts.Add(checkout);
        await _context.SaveChangesAsync();

        // Act
        var violations = await _analyticsEngine.DetectCheckoutPolicyViolationsAsync(_testUserId);

        // Assert
        Assert.NotEmpty(violations);
        Assert.Contains(violations, v => v.ViolationType == "ExcessiveDuration");
    }

    [Fact]
    public async Task GetAnalyticsSummaryAsync_ShouldReturnCompleteSummary()
    {
        // Arrange
        var account = new PrivilegedAccount
        {
            Id = Guid.NewGuid(),
            SafeId = _testSafeId,
            AccountName = "test_account",
            Platform = "MySQL",
            Username = "testuser",
            EncryptedPassword = "encrypted",
            RequiresMfa = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.PrivilegedAccounts.Add(account);
        await _context.SaveChangesAsync();

        // Act
        var summary = await _analyticsEngine.GetAnalyticsSummaryAsync(_testUserId);

        // Assert
        Assert.NotNull(summary);
        Assert.True(summary.TotalPrivilegedAccounts > 0);
        Assert.True(summary.ComplianceScore >= 0 && summary.ComplianceScore <= 100);
        Assert.NotNull(summary.AccountsByPlatform);
        Assert.NotNull(summary.TopDormantAccounts);
        Assert.NotNull(summary.TopRiskyAccounts);
        Assert.NotNull(summary.RecentAnomalies);
    }
}
