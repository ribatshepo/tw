using System.Diagnostics;
using FluentAssertions;
using USP.Api.Observability;
using Xunit;

namespace USP.IntegrationTests.Observability;

/// <summary>
/// Integration tests for OpenTelemetry distributed tracing configuration.
/// Validates that traces are properly created and enriched with context.
/// </summary>
public class TracingConfigurationTests
{
    [Fact]
    public void ActivitySource_ShouldBeInitialized()
    {
        // Assert
        TracingConfiguration.ActivitySource.Should().NotBeNull();
        TracingConfiguration.ActivitySource.Name.Should().Be("USP.Api");
        TracingConfiguration.ActivitySource.Version.Should().Be("1.0.0");
    }

    [Fact]
    public void StartActivity_ShouldCreateNewActivity()
    {
        // Arrange
        var operationName = "TestOperation";

        // Act
        using var activity = TracingConfiguration.StartActivity(operationName);

        // Assert
        activity.Should().NotBeNull();
        activity!.OperationName.Should().Be(operationName);
    }

    [Fact]
    public void EnrichWithAuthenticationContext_ShouldAddAuthTags()
    {
        // Arrange
        using var activity = TracingConfiguration.StartActivity("TestAuth");
        var userId = "user-123";
        var userName = "testuser";
        var method = "password";

        // Act
        TracingConfiguration.EnrichWithAuthenticationContext(activity, userId, userName, method);

        // Assert
        activity.Should().NotBeNull();
        activity!.GetTagItem("auth.user_id").Should().Be(userId);
        activity.GetTagItem("auth.user_name").Should().Be(userName);
        activity.GetTagItem("auth.method").Should().Be(method);
    }

    [Fact]
    public void EnrichWithDatabaseContext_ShouldAddDatabaseTags()
    {
        // Arrange
        using var activity = TracingConfiguration.StartActivity("TestDbQuery");
        var operation = "SELECT";
        var table = "users";
        var rowCount = 10;

        // Act
        TracingConfiguration.EnrichWithDatabaseContext(activity, operation, table, rowCount);

        // Assert
        activity.Should().NotBeNull();
        activity!.GetTagItem("db.operation").Should().Be(operation);
        activity.GetTagItem("db.table").Should().Be(table);
        activity.GetTagItem("db.row_count").Should().Be(rowCount);
    }

    [Fact]
    public void EnrichWithSecretsContext_ShouldAddSecretsTags()
    {
        // Arrange
        using var activity = TracingConfiguration.StartActivity("TestSecretOp");
        var engine = "kv";
        var operation = "read";
        var path = "secret/data/myapp";

        // Act
        TracingConfiguration.EnrichWithSecretsContext(activity, engine, operation, path);

        // Assert
        activity.Should().NotBeNull();
        activity!.GetTagItem("secrets.engine").Should().Be(engine);
        activity!.GetTagItem("secrets.operation").Should().Be(operation);
        activity!.GetTagItem("secrets.path").Should().Be(path);
    }

    [Fact]
    public void EnrichWithPamContext_ShouldAddPamTags()
    {
        // Arrange
        using var activity = TracingConfiguration.StartActivity("TestPamCheckout");
        var safe = "production";
        var account = "admin";
        var operation = "checkout";

        // Act
        TracingConfiguration.EnrichWithPamContext(activity, safe, account, operation);

        // Assert
        activity.Should().NotBeNull();
        activity!.GetTagItem("pam.safe").Should().Be(safe);
        activity!.GetTagItem("pam.account").Should().Be(account);
        activity!.GetTagItem("pam.operation").Should().Be(operation);
    }

    [Fact]
    public void RecordException_ShouldSetActivityToError()
    {
        // Arrange
        using var activity = TracingConfiguration.StartActivity("TestExceptionHandling");
        var exception = new InvalidOperationException("Test exception");

        // Act
        TracingConfiguration.RecordException(activity, exception);

        // Assert
        activity.Should().NotBeNull();
        activity!.Status.Should().Be(ActivityStatusCode.Error);
        activity.StatusDescription.Should().Be(exception.Message);
    }

    [Fact]
    public void SetOk_ShouldSetActivityStatusToOk()
    {
        // Arrange
        using var activity = TracingConfiguration.StartActivity("TestSuccess");

        // Act
        TracingConfiguration.SetOk(activity);

        // Assert
        activity.Should().NotBeNull();
        activity!.Status.Should().Be(ActivityStatusCode.Ok);
    }

    [Fact]
    public void SetError_ShouldSetActivityStatusToError()
    {
        // Arrange
        using var activity = TracingConfiguration.StartActivity("TestError");
        var description = "Operation failed";

        // Act
        TracingConfiguration.SetError(activity, description);

        // Assert
        activity.Should().NotBeNull();
        activity!.Status.Should().Be(ActivityStatusCode.Error);
        activity.StatusDescription.Should().Be(description);
    }

    [Fact]
    public void AddTag_ShouldAddCustomTag()
    {
        // Arrange
        using var activity = TracingConfiguration.StartActivity("TestCustomTag");
        var key = "custom.field";
        var value = "custom value";

        // Act
        TracingConfiguration.AddTag(activity, key, value);

        // Assert
        activity.Should().NotBeNull();
        activity!.GetTagItem(key).Should().Be(value);
    }

    [Fact]
    public void AddEvent_ShouldAddActivityEvent()
    {
        // Arrange
        using var activity = TracingConfiguration.StartActivity("TestEvent");
        var eventName = "UserLoggedIn";
        var tags = new Dictionary<string, object?>
        {
            ["user_id"] = "user-123",
            ["timestamp"] = DateTime.UtcNow
        };

        // Act
        TracingConfiguration.AddEvent(activity, eventName, tags);

        // Assert
        activity.Should().NotBeNull();
        activity!.Events.Should().Contain(e => e.Name == eventName);
    }

    [Fact]
    public void StartActivity_WithClientKind_ShouldCreateClientActivity()
    {
        // Arrange
        var operationName = "HttpClientCall";

        // Act
        using var activity = TracingConfiguration.StartActivity(operationName, ActivityKind.Client);

        // Assert
        activity.Should().NotBeNull();
        activity!.Kind.Should().Be(ActivityKind.Client);
    }

    [Fact]
    public void StartActivity_WithServerKind_ShouldCreateServerActivity()
    {
        // Arrange
        var operationName = "HandleRequest";

        // Act
        using var activity = TracingConfiguration.StartActivity(operationName, ActivityKind.Server);

        // Assert
        activity.Should().NotBeNull();
        activity!.Kind.Should().Be(ActivityKind.Server);
    }

    [Fact]
    public void EnrichWithAuthenticationContext_WithNullActivity_ShouldNotThrow()
    {
        // Arrange
        Activity? activity = null;

        // Act
        var act = () => TracingConfiguration.EnrichWithAuthenticationContext(activity, "user", "name", "method");

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void AddTag_WithNullValue_ShouldNotAddTag()
    {
        // Arrange
        using var activity = TracingConfiguration.StartActivity("TestNullTag");
        var key = "nullable.field";
        object? value = null;

        // Act
        TracingConfiguration.AddTag(activity, key, value);

        // Assert
        activity.Should().NotBeNull();
        activity!.GetTagItem(key).Should().BeNull();
    }
}
