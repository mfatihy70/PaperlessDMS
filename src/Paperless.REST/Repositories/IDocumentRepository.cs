using Paperless.REST.Models;

namespace Paperless.REST.Repositories;

public interface IDocumentRepository : IRepository<Document>
{
    Task<IEnumerable<Document>> GetAllWithCategoriesAsync();
    Task<Document?> GetByIdWithCategoryAsync(int id);
}
