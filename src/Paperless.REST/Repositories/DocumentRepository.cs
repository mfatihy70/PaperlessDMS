using Microsoft.EntityFrameworkCore;
using Paperless.REST.Data;
using Paperless.REST.Models;

namespace Paperless.REST.Repositories;

public class DocumentRepository : Repository<Document>, IDocumentRepository
{
    public DocumentRepository(PaperlessDbContext context) : base(context) { }

    public async Task<IEnumerable<Document>> GetAllWithCategoriesAsync() =>
        await _context.Documents.Include(d => d.Category).ToListAsync();

    public async Task<Document?> GetByIdWithCategoryAsync(int id) =>
        await _context.Documents.Include(d => d.Category).FirstOrDefaultAsync(d => d.Id == id);
}