using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

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

        // Use a simple connection string for design-time operations
        // This will be replaced by actual configuration at runtime
        var connectionString = Environment.GetEnvironmentVariable("DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=usp_db;Username=usp_user;Password=changeme";

        optionsBuilder.UseNpgsql(connectionString);

        return new ApplicationDbContext(optionsBuilder.Options);
    }
}
