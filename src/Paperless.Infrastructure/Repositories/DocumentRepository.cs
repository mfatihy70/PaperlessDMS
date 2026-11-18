using Microsoft.EntityFrameworkCore;
using Paperless.Application.Abstractions;
using Paperless.Domain.Entities;
using Paperless.Infrastructure.Persistence;

namespace Paperless.Infrastructure.Repositories;

public class DocumentRepository : Repository<Document>, IDocumentRepository
{
    public DocumentRepository(PaperlessDbContext context) : base(context) { }

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
                HasFile = d.FileContent != null
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
                HasFile = d.FileContent != null
            })
            .FirstOrDefaultAsync();
}
