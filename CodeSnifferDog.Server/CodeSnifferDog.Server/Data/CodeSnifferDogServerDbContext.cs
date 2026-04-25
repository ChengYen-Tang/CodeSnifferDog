using CodeSnifferDog.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CodeSnifferDog.Server.Data;

public sealed class CodeSnifferDogServerDbContext(DbContextOptions<CodeSnifferDogServerDbContext> options) : DbContext(options)
{
    public DbSet<ProjectRecord> Projects => Set<ProjectRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProjectRecord>(entity =>
        {
            entity.ToTable("Projects");
            entity.HasKey(project => project.Id);

            entity.Property(project => project.OriginalFileName)
                .HasMaxLength(260);

            entity.Property(project => project.StoredZipRelativePath)
                .HasMaxLength(1024);

            entity.Property(project => project.Status)
                .HasConversion<string>()
                .HasMaxLength(64);

            entity.Property(project => project.FailureReason)
                .HasMaxLength(2000);

            entity.HasIndex(project => new
            {
                project.Status,
                project.QueueTimestampUtc,
                project.CreatedAtUtc,
            });
        });
    }
}
