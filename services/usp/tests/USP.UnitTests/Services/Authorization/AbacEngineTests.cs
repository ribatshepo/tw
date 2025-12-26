using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using USP.Core.Models.DTOs.Authorization;
using USP.Core.Models.Entities;
using USP.Core.Services.Authorization;
using USP.Infrastructure.Data;
using USP.Infrastructure.Services.Authorization;
using Xunit;

namespace USP.UnitTests.Services.Authorization;

public class AbacEngineTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<IHclPolicyEvaluator> _hclEvaluatorMock;
    private readonly Mock<ILogger<AbacEngine>> _loggerMock;
    private readonly AbacEngine _abacEngine;
    private readonly Guid _testUserId;

    public AbacEngineTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;

        _context = new ApplicationDbContext(options);
        _hclEvaluatorMock = new Mock<IHclPolicyEvaluator>();
        _loggerMock = new Mock<ILogger<AbacEngine>>();
        _abacEngine = new AbacEngine(_context, _hclEvaluatorMock.Object, _loggerMock.Object);

        _testUserId = Guid.NewGuid();
        SeedTestData();
    }

    private void SeedTestData()
    {
        var role = new Role
        {
            Id = Guid.NewGuid(),
            Name = "DataEngineer",
            Description = "Data Engineer",
            IsBuiltIn = true,
            CreatedAt = DateTime.UtcNow
        };

        var user = new ApplicationUser
        {
            Id = _testUserId,
            UserName = "test.user",
            Email = "test@example.com",
            Status = "active",
            MfaEnabled = true,
            EmailConfirmed = true,
            PhoneNumberConfirmed = false,
            LockoutEnabled = true,
            CreatedAt = DateTime.UtcNow.AddDays(-30),
            LastLoginAt = DateTime.UtcNow.AddHours(-2),
            Metadata = System.Text.Json.JsonSerializer.Serialize(new
            {
                department = "engineering",
                clearance_level = "confidential",
                job_function = "data_engineer",
                location = "US/Seattle",
                employment_type = "full-time"
            })
        };

        var userRole = new UserRole
        {
            UserId = user.Id,
            RoleId = role.Id,
            AssignedAt = DateTime.UtcNow
        };

        var riskProfile = new UserRiskProfile
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            CurrentRiskScore = 25,
            LastCalculatedAt = DateTime.UtcNow
        };

        _context.Roles.Add(role);
        _context.Users.Add(user);
        _context.UserRoles.Add(userRole);
        _context.UserRiskProfiles.Add(riskProfile);
        _context.SaveChanges();
    }

    #region Attribute Extraction Tests

    [Fact]
    public async Task ExtractAttributesAsync_ShouldExtractSubjectAttributes()
    {
        // Arrange
        var request = new AttributeExtractionRequest
        {
            UserId = _testUserId,
            ResourceType = "secret",
            ResourceId = null
        };

        // Act
        var attributes = await _abacEngine.ExtractAttributesAsync(request);

        // Assert
        attributes.Should().NotBeNull();
        attributes.SubjectAttributes.Should().ContainKey("user_id");
        attributes.SubjectAttributes.Should().ContainKey("username");
        attributes.SubjectAttributes.Should().ContainKey("email");
        attributes.SubjectAttributes.Should().ContainKey("status");
        attributes.SubjectAttributes.Should().ContainKey("mfa_enabled");
        attributes.SubjectAttributes.Should().ContainKey("department");
        attributes.SubjectAttributes.Should().ContainKey("clearance_level");
        attributes.SubjectAttributes.Should().ContainKey("job_function");
        attributes.SubjectAttributes.Should().ContainKey("location");
        attributes.SubjectAttributes.Should().ContainKey("risk_score");

        attributes.SubjectAttributes["email"].Should().Be("test@example.com");
        attributes.SubjectAttributes["department"].Should().Be("engineering");
        attributes.SubjectAttributes["clearance_level"].Should().Be("confidential");
        attributes.SubjectAttributes["mfa_enabled"].Should().Be(true);
        attributes.SubjectAttributes["risk_score"].Should().Be(25);
    }

    [Fact]
    public async Task ExtractAttributesAsync_ShouldExtractEnvironmentAttributes()
    {
        // Arrange
        var request = new AttributeExtractionRequest
        {
            UserId = _testUserId,
            ResourceType = "secret",
            AdditionalContext = new Dictionary<string, object>
            {
                { "ip_address", "192.168.1.100" },
                { "network_zone", "internal" },
                { "device_compliance", "compliant" },
                { "geo_location", "US/Seattle" }
            }
        };

        // Act
        var attributes = await _abacEngine.ExtractAttributesAsync(request);

        // Assert
        attributes.EnvironmentAttributes.Should().ContainKey("current_time");
        attributes.EnvironmentAttributes.Should().ContainKey("day_of_week");
        attributes.EnvironmentAttributes.Should().ContainKey("hour_of_day");
        attributes.EnvironmentAttributes.Should().ContainKey("is_business_hours");
        attributes.EnvironmentAttributes.Should().ContainKey("is_weekend");
        attributes.EnvironmentAttributes.Should().ContainKey("ip_address");
        attributes.EnvironmentAttributes.Should().ContainKey("network_zone");
        attributes.EnvironmentAttributes.Should().ContainKey("device_compliance_status");
        attributes.EnvironmentAttributes.Should().ContainKey("geo_location");

        attributes.EnvironmentAttributes["ip_address"].Should().Be("192.168.1.100");
        attributes.EnvironmentAttributes["network_zone"].Should().Be("internal");
        attributes.EnvironmentAttributes["is_internal_network"].Should().Be(true);
        attributes.EnvironmentAttributes["is_compliant_device"].Should().Be(true);
    }

    [Fact]
    public async Task ExtractAttributesAsync_ShouldExtractResourceAttributes_ForSecret()
    {
        // Arrange
        var secret = new Secret
        {
            Id = Guid.NewGuid(),
            Path = "engineering/production/database",
            Version = 5,
            CreatedBy = _testUserId,
            CreatedAt = DateTime.UtcNow.AddDays(-10),
            IsDeleted = false,
            Metadata = System.Text.Json.JsonSerializer.Serialize(new
            {
                classification = "confidential",
                sensitivity = "high",
                owner = "engineering",
                tags = new[] { "production", "database", "critical" }
            })
        };

        _context.Secrets.Add(secret);
        await _context.SaveChangesAsync();

        var request = new AttributeExtractionRequest
        {
            UserId = _testUserId,
            ResourceType = "secret",
            ResourceId = secret.Id.ToString()
        };

        // Act
        var attributes = await _abacEngine.ExtractAttributesAsync(request);

        // Assert
        attributes.ResourceAttributes.Should().ContainKey("resource_id");
        attributes.ResourceAttributes.Should().ContainKey("path");
        attributes.ResourceAttributes.Should().ContainKey("version");
        attributes.ResourceAttributes.Should().ContainKey("classification");
        attributes.ResourceAttributes.Should().ContainKey("sensitivity_level");
        attributes.ResourceAttributes.Should().ContainKey("department");
        attributes.ResourceAttributes.Should().ContainKey("workspace");
        attributes.ResourceAttributes.Should().ContainKey("tags");

        attributes.ResourceAttributes["classification"].Should().Be("confidential");
        attributes.ResourceAttributes["sensitivity_level"].Should().Be("high");
        attributes.ResourceAttributes["department"].Should().Be("engineering");
        attributes.ResourceAttributes["workspace"].Should().Be("production");
    }

    #endregion

    #region Policy Evaluation Tests

    [Fact]
    public async Task EvaluateAsync_ShouldAllow_WhenNoActivePolicies()
    {
        // Arrange
        var request = new AbacEvaluationRequest
        {
            SubjectId = _testUserId,
            Action = "read",
            ResourceType = "secret",
            ResourceId = null
        };

        // Act
        var result = await _abacEngine.EvaluateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Decision.Should().Be("not_applicable");
        result.Allowed.Should().BeFalse();
        result.Reasons.Should().Contain("No active ABAC policies configured");
    }

    [Fact]
    public async Task EvaluateAsync_ShouldAllow_WhenPolicyMatches()
    {
        // Arrange
        var policy = new AccessPolicy
        {
            Id = Guid.NewGuid(),
            Name = "Allow Read Secrets",
            PolicyType = "ABAC",
            Policy = System.Text.Json.JsonSerializer.Serialize(new
            {
                rules = new[]
                {
                    new
                    {
                        name = "Allow engineers to read secrets",
                        effect = "allow",
                        action = "read",
                        resource = "secret",
                        conditions = new
                        {
                            department = "engineering"
                        }
                    }
                }
            }),
            IsActive = true,
            CreatedBy = _testUserId,
            CreatedAt = DateTime.UtcNow
        };

        _context.AccessPolicies.Add(policy);
        await _context.SaveChangesAsync();

        var request = new AbacEvaluationRequest
        {
            SubjectId = _testUserId,
            Action = "read",
            ResourceType = "secret"
        };

        // Act
        var result = await _abacEngine.EvaluateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Decision.Should().Be("allow");
        result.Allowed.Should().BeTrue();
        result.AppliedPolicies.Should().Contain("Allow Read Secrets");
    }

    [Fact]
    public async Task EvaluateAsync_ShouldDeny_WhenExplicitDenyPolicy()
    {
        // Arrange
        var policy = new AccessPolicy
        {
            Id = Guid.NewGuid(),
            Name = "Deny Delete Secrets",
            PolicyType = "ABAC",
            Policy = System.Text.Json.JsonSerializer.Serialize(new
            {
                rules = new[]
                {
                    new
                    {
                        name = "Deny delete for all",
                        effect = "deny",
                        action = "delete",
                        resource = "secret"
                    }
                }
            }),
            IsActive = true,
            CreatedBy = _testUserId,
            CreatedAt = DateTime.UtcNow
        };

        _context.AccessPolicies.Add(policy);
        await _context.SaveChangesAsync();

        var request = new AbacEvaluationRequest
        {
            SubjectId = _testUserId,
            Action = "delete",
            ResourceType = "secret"
        };

        // Act
        var result = await _abacEngine.EvaluateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Decision.Should().Be("deny");
        result.Allowed.Should().BeFalse();
        result.Reasons.Should().Contain(r => r.Contains("Denied by policies"));
    }

    [Fact]
    public async Task EvaluateAsync_ShouldRespectClearanceLevel()
    {
        // Arrange
        var policy = new AccessPolicy
        {
            Id = Guid.NewGuid(),
            Name = "Clearance Level Policy",
            PolicyType = "ABAC",
            Policy = System.Text.Json.JsonSerializer.Serialize(new
            {
                rules = new[]
                {
                    new
                    {
                        name = "Require confidential clearance",
                        effect = "allow",
                        action = "read",
                        resource = "secret",
                        conditions = new
                        {
                            clearance_level = "confidential"
                        }
                    }
                }
            }),
            IsActive = true,
            CreatedBy = _testUserId,
            CreatedAt = DateTime.UtcNow
        };

        _context.AccessPolicies.Add(policy);
        await _context.SaveChangesAsync();

        var request = new AbacEvaluationRequest
        {
            SubjectId = _testUserId,
            Action = "read",
            ResourceType = "secret"
        };

        // Act
        var result = await _abacEngine.EvaluateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Decision.Should().Be("allow");
        result.Allowed.Should().BeTrue();
    }

    #endregion

    #region Has Access Tests

    [Fact]
    public async Task HasAccessAsync_ShouldReturnTrue_WhenUserHasAccess()
    {
        // Arrange
        var policy = new AccessPolicy
        {
            Id = Guid.NewGuid(),
            Name = "Allow Policy",
            PolicyType = "ABAC",
            Policy = System.Text.Json.JsonSerializer.Serialize(new
            {
                rules = new[]
                {
                    new
                    {
                        name = "Allow read",
                        effect = "allow",
                        action = "read",
                        resource = "secret"
                    }
                }
            }),
            IsActive = true,
            CreatedBy = _testUserId,
            CreatedAt = DateTime.UtcNow
        };

        _context.AccessPolicies.Add(policy);
        await _context.SaveChangesAsync();

        // Act
        var hasAccess = await _abacEngine.HasAccessAsync(_testUserId, "read", "secret");

        // Assert
        hasAccess.Should().BeTrue();
    }

    [Fact]
    public async Task HasAccessAsync_ShouldReturnFalse_WhenUserDoesNotHaveAccess()
    {
        // Act
        var hasAccess = await _abacEngine.HasAccessAsync(_testUserId, "delete", "secret");

        // Assert
        hasAccess.Should().BeFalse();
    }

    #endregion

    #region Policy Simulation Tests

    [Fact]
    public async Task SimulatePolicyAsync_ShouldReturnSimulationResult()
    {
        // Arrange
        var policy = new AccessPolicy
        {
            Id = Guid.NewGuid(),
            Name = "Test Policy",
            PolicyType = "ABAC",
            Policy = System.Text.Json.JsonSerializer.Serialize(new
            {
                rules = new[]
                {
                    new
                    {
                        name = "Allow engineers",
                        effect = "allow",
                        action = "read",
                        resource = "secret",
                        conditions = new
                        {
                            department = "engineering"
                        }
                    }
                }
            }),
            IsActive = true,
            CreatedBy = _testUserId,
            CreatedAt = DateTime.UtcNow
        };

        _context.AccessPolicies.Add(policy);
        await _context.SaveChangesAsync();

        var request = new PolicySimulationRequest
        {
            PolicyId = policy.Id,
            UserId = _testUserId,
            Action = "read",
            Resource = "secret"
        };

        // Act
        var result = await _abacEngine.SimulatePolicyAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Decision.Should().Be("allow");
        result.Allowed.Should().BeTrue();
        result.EvaluationSteps.Should().NotBeEmpty();
        result.AttributesUsed.Should().ContainKey("subject");
        result.AttributesUsed.Should().ContainKey("resource");
        result.AttributesUsed.Should().ContainKey("environment");
    }

    #endregion

    public void Dispose()
    {
        _context.Dispose();
    }
}
