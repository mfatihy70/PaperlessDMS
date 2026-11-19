using Paperless.Domain.Entities;

namespace Paperless.Application.Abstractions;

public interface IDocumentRepository : IRepository<Document>
{
    Task<IEnumerable<Document>> GetAllWithCategoriesAsync();
    Task<Document?> GetByIdWithCategoryAsync(int id);
    /// <summary>Durably reserves a new immutable key before any upload or entity modification.</summary>
    Task<string> BeginFileUploadAsync(CancellationToken cancellationToken);
    /// <summary>Commits metadata, claims the upload and schedules old-object deletion in one transaction.</summary>
    Task SaveFileChangesAsync(string? uploadedKey, string? retiredKey, CancellationToken cancellationToken);
}
