using Microsoft.EntityFrameworkCore;
using Paperless.Application.Exceptions;
using Paperless.Domain.Entities;

namespace Paperless.Infrastructure.Persistence;

public class PaperlessDbContext : DbContext
{
    public PaperlessDbContext(DbContextOptions<PaperlessDbContext> options) : base(options) { }

    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentCategory> Categories => Set<DocumentCategory>();
    public DbSet<FileCleanupJob> FileCleanupJobs => Set<FileCleanupJob>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("public");

        modelBuilder.Entity<Document>(entity =>
        {
            entity.HasKey(d => d.Id);
            entity.Property(d => d.Title).IsRequired().HasMaxLength(255);
            entity.Property(d => d.FileName).IsRequired().HasMaxLength(255);
            entity.Property(d => d.FileContent).HasColumnType("bytea");
            entity.Property(d => d.StorageKey).HasMaxLength(128);
            entity.HasIndex(d => d.StorageKey).IsUnique();
            entity.Property(d => d.FileSha256).HasMaxLength(64);
            entity.Property(d => d.Version).IsRowVersion();
            entity.Ignore(d => d.HasFile);
            entity.HasOne(d => d.Category)
                  .WithMany(c => c.Documents)
                  .HasForeignKey(d => d.CategoryId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<DocumentCategory>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Name).IsRequired().HasMaxLength(100);
        });

        modelBuilder.Entity<FileCleanupJob>(entity =>
        {
            entity.HasKey(job => job.ObjectKey);
            entity.Property(job => job.ObjectKey).HasMaxLength(128);
            entity.HasIndex(job => job.NotBefore);
        });
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new ApplicationConflictException("The document changed during this operation. Reload it and try again.", exception);
        }
    }
}
