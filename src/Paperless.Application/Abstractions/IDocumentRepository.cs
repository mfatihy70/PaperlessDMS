using Paperless.Domain.Entities;

namespace Paperless.Application.Abstractions;

public interface IDocumentRepository : IRepository<Document>
{
    Task<IEnumerable<Document>> GetAllWithCategoriesAsync();
    Task<Document?> GetByIdWithCategoryAsync(int id);
}
