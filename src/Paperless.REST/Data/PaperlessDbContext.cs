using Microsoft.EntityFrameworkCore;
using Paperless.REST.Models;

namespace Paperless.REST.Data;

public class PaperlessDbContext : DbContext
{
    public PaperlessDbContext(DbContextOptions<PaperlessDbContext> options) : base(options) { }

    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentCategory> Categories => Set<DocumentCategory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Document>(entity =>
        {
            entity.HasKey(d => d.Id);
            entity.Property(d => d.Title).IsRequired().HasMaxLength(255);
            entity.Property(d => d.FileName).IsRequired().HasMaxLength(255);
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
    }
}