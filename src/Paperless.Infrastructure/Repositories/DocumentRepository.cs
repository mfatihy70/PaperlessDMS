using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Paperless.Application.Abstractions;
using Paperless.Application.Exceptions;
using Paperless.Domain.Entities;
using Paperless.Infrastructure.Persistence;
using Paperless.Infrastructure.Processing;
using Paperless.Infrastructure.Storage;

namespace Paperless.Infrastructure.Repositories;

public class DocumentRepository : Repository<Document>, IDocumentRepository
{
    private readonly IDbContextFactory<PaperlessDbContext> _contextFactory;
    private readonly MinioStorageOptions _storageOptions;

    public DocumentRepository(PaperlessDbContext context, IDbContextFactory<PaperlessDbContext> contextFactory,
        IOptions<MinioStorageOptions> storageOptions) : base(context)
    {
        _contextFactory = contextFactory;
        _storageOptions = storageOptions.Value;
    }

    public async Task<string> BeginFileUploadAsync(CancellationToken cancellationToken)
    {
        var key = $"documents/{Guid.NewGuid():N}";
        await using var leaseContext = await _contextFactory.CreateDbContextAsync(cancellationToken);
        leaseContext.FileCleanupJobs.Add(new FileCleanupJob
        {
            ObjectKey = key,
            NotBefore = DateTime.UtcNow.AddMinutes(_storageOptions.UploadLeaseMinutes)
        });
        await leaseContext.SaveChangesAsync(cancellationToken);
        return key;
    }

    public async Task SaveFileChangesAsync(string? uploadedKey, string? retiredKey, CancellationToken cancellationToken)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        if (uploadedKey is not null)
        {
            var lease = await _context.FileCleanupJobs.FindAsync([uploadedKey], cancellationToken);
            if (lease is null)
                throw new ApplicationConflictException("The upload reservation expired. Please upload the file again.");
            _context.FileCleanupJobs.Remove(lease);
        }

        await _context.SaveChangesAsync(cancellationToken);
        if (uploadedKey is not null)
        {
            var document = _context.Documents.Local.Single(d => d.StorageKey == uploadedKey);
            await ProcessingScheduler.ScheduleAsync(_context, document, cancellationToken);
        }
        if (retiredKey is not null)
        {
            await _context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO public."FileCleanupJobs" ("ObjectKey", "NotBefore", "Attempts")
                VALUES ({retiredKey}, {DateTime.UtcNow}, 0)
                ON CONFLICT ("ObjectKey") DO NOTHING;
                """, cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IEnumerable<Document>> GetAllWithCategoriesAsync() =>
        await _context.Documents
            .AsNoTracking()
            .Select(d => new Document
            {
                Id = d.Id,
                Title = d.Title,
                FileName = d.FileName,
                ContentType = d.ContentType,
                FileSizeBytes = d.FileSizeBytes,
                CreatedAt = d.CreatedAt,
                CategoryId = d.CategoryId,
                Category = d.Category,
                StorageKey = d.StorageKey,
                HasFile = d.StorageKey != null || d.FileContent != null
            })
            .ToListAsync();

    public async Task<Document?> GetByIdWithCategoryAsync(int id) =>
        await _context.Documents
            .AsNoTracking()
            .Where(d => d.Id == id)
            .Select(d => new Document
            {
                Id = d.Id,
                Title = d.Title,
                FileName = d.FileName,
                ContentType = d.ContentType,
                FileSizeBytes = d.FileSizeBytes,
                CreatedAt = d.CreatedAt,
                CategoryId = d.CategoryId,
                Category = d.Category,
                StorageKey = d.StorageKey,
                HasFile = d.StorageKey != null || d.FileContent != null
            })
            .FirstOrDefaultAsync();
}
