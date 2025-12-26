using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using USP.Core.Models.Configuration;

namespace USP.Infrastructure.Data;

/// <summary>
/// Design-time factory for ApplicationDbContext
/// Used by EF Core tools for migrations
/// </summary>
public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();

        // Load configuration from User Secrets and environment variables
        // For migrations to work, you MUST set Database:Password in User Secrets:
        // dotnet user-secrets set "Database:Password" "your-password"
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddUserSecrets<ApplicationDbContextFactory>(optional: true)
            .AddEnvironmentVariables(prefix: "USP_")
            .Build();

        // Try to load connection string from configuration
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        // If no connection string, build from Database settings
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            var dbSettings = new DatabaseSettings();
            configuration.GetSection("Database").Bind(dbSettings);

            // Validate that password is provided
            if (string.IsNullOrWhiteSpace(dbSettings.Password))
            {
                throw new InvalidOperationException(
                    "Database password is required for migrations. " +
                    "Set it in User Secrets: dotnet user-secrets set \"Database:Password\" \"your-password\" " +
                    "OR set environment variable: export USP_Database__Password=\"your-password\"");
            }

            connectionString = dbSettings.BuildConnectionString();
        }

        optionsBuilder.UseNpgsql(connectionString);

        return new ApplicationDbContext(optionsBuilder.Options);
    }
}
