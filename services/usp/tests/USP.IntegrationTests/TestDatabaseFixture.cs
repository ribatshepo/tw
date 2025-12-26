using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using USP.Infrastructure.Data;

namespace USP.IntegrationTests;

/// <summary>
/// Test database fixture for integration tests
/// </summary>
public class TestDatabaseFixture : IDisposable
{
    public ApplicationDbContext Context { get; }
    public IServiceProvider ServiceProvider { get; }

    public TestDatabaseFixture()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Host"] = "localhost",
                ["Database:Port"] = "5432",
                ["Database:Database"] = $"usp_test_{Guid.NewGuid()}",
                ["Database:Username"] = "usp_test",
                ["Database:Password"] = "test_password"
            })
            .Build();

        var services = new ServiceCollection();

        // Use in-memory database for testing
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}"));

        services.AddSingleton<IConfiguration>(configuration);

        ServiceProvider = services.BuildServiceProvider();
        Context = ServiceProvider.GetRequiredService<ApplicationDbContext>();
    }

    public void Dispose()
    {
        Context?.Dispose();
        if (ServiceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
