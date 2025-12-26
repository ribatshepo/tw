using Microsoft.EntityFrameworkCore;
using USP.Infrastructure.Data;

namespace USP.UnitTests.TestHelpers;

/// <summary>
/// Test version of ApplicationDbContext that configures entities for InMemory database provider
/// Handles JsonDocument and other types not supported by InMemory provider
/// </summary>
public class TestApplicationDbContext : ApplicationDbContext
{
    public TestApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure entities with JsonDocument properties for InMemory provider
        // AccessPolicy has 3 JsonDocument properties - ignore them all for InMemory testing
        modelBuilder.Entity<USP.Core.Models.Entities.AccessPolicy>()
            .Ignore(e => e.Subjects)
            .Ignore(e => e.Resources)
            .Ignore(e => e.Conditions);

        // Secret has 1 JsonDocument property
        modelBuilder.Entity<USP.Core.Models.Entities.Secret>()
            .Ignore(e => e.Metadata);
    }
}
