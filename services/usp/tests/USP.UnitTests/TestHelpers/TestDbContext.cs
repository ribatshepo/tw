using Microsoft.EntityFrameworkCore;
using USP.Core.Models.Entities;

namespace USP.UnitTests.TestHelpers;

/// <summary>
/// Lightweight test DbContext that only includes entities needed for Transit Engine tests
/// Avoids InMemory database issues with JsonDocument and other unsupported types
/// </summary>
public class TestDbContext : DbContext
{
    public TestDbContext(DbContextOptions<TestDbContext> options) : base(options)
    {
    }

    public DbSet<TransitKey> TransitKeys { get; set; } = null!;
    public DbSet<TransitKeyVersion> TransitKeyVersions { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure TransitKey
        modelBuilder.Entity<TransitKey>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
            entity.HasIndex(e => e.Name).IsUnique();
            entity.Property(e => e.Type).IsRequired();
            entity.HasMany(e => e.Versions)
                .WithOne(v => v.TransitKey)
                .HasForeignKey(v => v.TransitKeyId);
        });

        // Configure TransitKeyVersion
        modelBuilder.Entity<TransitKeyVersion>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.TransitKeyId, e.Version }).IsUnique();
            entity.Property(e => e.EncryptedKeyMaterial).IsRequired();
        });
    }
}
