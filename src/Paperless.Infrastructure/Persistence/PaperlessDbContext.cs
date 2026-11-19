using Microsoft.EntityFrameworkCore;
using Paperless.Application.Exceptions;
using Paperless.Domain.Entities;
using Paperless.Infrastructure.Processing;
using Paperless.Infrastructure.Search;
using Paperless.Infrastructure.Summarization;

namespace Paperless.Infrastructure.Persistence;

public class PaperlessDbContext : DbContext
{
    public PaperlessDbContext(DbContextOptions<PaperlessDbContext> options) : base(options) { }

    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentCategory> Categories => Set<DocumentCategory>();
    public DbSet<FileCleanupJob> FileCleanupJobs => Set<FileCleanupJob>();
    public DbSet<DocumentProcessingJob> ProcessingJobs => Set<DocumentProcessingJob>();
    public DbSet<ProcessingOutboxMessage> ProcessingOutbox => Set<ProcessingOutboxMessage>();
    public DbSet<DocumentText> DocumentTexts => Set<DocumentText>();
    public DbSet<SearchIndexJob> SearchIndexJobs => Set<SearchIndexJob>();
    public DbSet<SearchOutboxMessage> SearchOutbox => Set<SearchOutboxMessage>();
    public DbSet<SearchIndexCatalog> SearchIndexCatalog => Set<SearchIndexCatalog>();
    public DbSet<SummaryJob> SummaryJobs => Set<SummaryJob>();
    public DbSet<DocumentSummary> DocumentSummaries => Set<DocumentSummary>();
    public DbSet<SummaryOutboxMessage> SummaryOutbox => Set<SummaryOutboxMessage>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<DocumentTag> DocumentTags => Set<DocumentTag>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("public");
        modelBuilder.HasSequence<long>("SearchIndexRevisions", "public");

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

        modelBuilder.Entity<DocumentProcessingJob>(entity =>
        {
            entity.HasKey(job => job.DocumentId);
            entity.HasOne<Document>().WithOne().HasForeignKey<DocumentProcessingJob>(job => job.DocumentId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(job => job.JobId).IsUnique();
            entity.Property(job => job.StorageKey).HasMaxLength(128);
            entity.Property(job => job.FileSha256).HasMaxLength(64);
            entity.Property(job => job.State).HasConversion<string>().HasMaxLength(32);
            entity.Property(job => job.LastErrorCode).HasMaxLength(64);
            entity.Property(job => job.LastError).HasMaxLength(512);
        });
        modelBuilder.Entity<ProcessingOutboxMessage>(entity =>
        {
            entity.HasKey(message => message.Id);
            entity.HasOne<Document>().WithMany().HasForeignKey(message => message.DocumentId).OnDelete(DeleteBehavior.Cascade);
            entity.Property(message => message.Payload).HasMaxLength(8192);
            entity.Property(message => message.LastError).HasMaxLength(512);
            entity.HasIndex(message => new { message.PublishedAt, message.AvailableAt });
            entity.HasIndex(message => new { message.DocumentId, message.JobId });
        });
        modelBuilder.Entity<DocumentText>(entity =>
        {
            entity.HasKey(text => text.DocumentId);
            entity.HasOne<Document>().WithOne().HasForeignKey<DocumentText>(text => text.DocumentId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(text => text.JobId).IsUnique();
            entity.Property(text => text.StorageKey).HasMaxLength(128);
            entity.Property(text => text.Text).HasMaxLength(1_000_000);
            entity.Property(text => text.Language).HasMaxLength(64);
        });
        modelBuilder.Entity<SearchIndexJob>(entity =>
        {
            entity.HasKey(job => job.DocumentId);
            entity.Property(job => job.DocumentId).ValueGeneratedNever();
            entity.Property(job => job.Action).HasConversion<string>().HasMaxLength(16);
            entity.Property(job => job.State).HasConversion<string>().HasMaxLength(32);
            entity.Property(job => job.LastErrorCode).HasMaxLength(64);
            entity.Property(job => job.LastError).HasMaxLength(512);
        });
        modelBuilder.Entity<SearchOutboxMessage>(entity =>
        {
            entity.HasKey(message => message.Id);
            entity.HasOne<SearchIndexJob>().WithMany().HasForeignKey(message => message.DocumentId).OnDelete(DeleteBehavior.Cascade);
            entity.Property(message => message.Payload).HasMaxLength(8192);
            entity.Property(message => message.LastError).HasMaxLength(512);
            entity.HasIndex(message => new { message.PublishedAt, message.AvailableAt });
            entity.HasIndex(message => new { message.DocumentId, message.Revision });
        });
        modelBuilder.Entity<SearchIndexCatalog>(entity =>
        {
            entity.HasKey(catalog => catalog.Id);
            entity.Property(catalog => catalog.Id).ValueGeneratedNever();
            entity.Property(catalog => catalog.Identity).HasMaxLength(255);
        });
        modelBuilder.Entity<SummaryJob>(entity =>
        {
            entity.HasKey(job => job.DocumentId);
            entity.HasOne<Document>().WithOne().HasForeignKey<SummaryJob>(job => job.DocumentId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(job => job.JobId).IsUnique();
            entity.Property(job => job.State).HasConversion<string>().HasMaxLength(32);
            entity.Property(job => job.LastErrorCode).HasMaxLength(64);
            entity.Property(job => job.LastError).HasMaxLength(512);
        });
        modelBuilder.Entity<DocumentSummary>(entity =>
        {
            entity.HasKey(summary => summary.DocumentId);
            entity.HasOne<SummaryJob>().WithOne().HasForeignKey<DocumentSummary>(summary => summary.DocumentId).OnDelete(DeleteBehavior.Cascade);
            entity.Property(summary => summary.Text).HasMaxLength(4000);
            entity.Property(summary => summary.Language).HasMaxLength(32);
            entity.Property(summary => summary.Provider).HasMaxLength(32);
            entity.Property(summary => summary.Model).HasMaxLength(128);
        });
        modelBuilder.Entity<SummaryOutboxMessage>(entity =>
        {
            entity.HasKey(message => message.Id);
            entity.HasOne<SummaryJob>().WithMany().HasForeignKey(message => message.DocumentId).OnDelete(DeleteBehavior.Cascade);
            entity.Property(message => message.Payload).HasMaxLength(8192);
            entity.Property(message => message.LastError).HasMaxLength(512);
            entity.HasIndex(message => new { message.PublishedAt, message.AvailableAt });
        });
        modelBuilder.Entity<Tag>(entity =>
        {
            entity.HasKey(tag => tag.Id);
            entity.Property(tag => tag.Name).HasMaxLength(100);
            entity.Property(tag => tag.NormalizedName).HasMaxLength(100);
            entity.Property(tag => tag.Description).HasMaxLength(1000);
            entity.HasIndex(tag => tag.NormalizedName).IsUnique();
        });
        modelBuilder.Entity<DocumentTag>(entity =>
        {
            entity.HasKey(tag => new { tag.DocumentId, tag.TagId });
            entity.HasOne<Document>().WithMany().HasForeignKey(tag => tag.DocumentId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Tag>().WithMany().HasForeignKey(tag => tag.TagId).OnDelete(DeleteBehavior.Cascade);
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
