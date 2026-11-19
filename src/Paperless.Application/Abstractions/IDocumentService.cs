using Paperless.Application.DTOs;

namespace Paperless.Application.Abstractions;

public interface IDocumentService
{
    Task<IEnumerable<DocumentResponseDto>> GetAllDocumentsAsync();
    Task<DocumentResponseDto?> GetDocumentByIdAsync(int id);
    Task<DocumentResponseDto> CreateDocumentAsync(DocumentUpload file, int? categoryId, CancellationToken cancellationToken);
    Task<DocumentFileDto?> GetDocumentFileAsync(int id, CancellationToken cancellationToken = default);
    Task<bool> UpdateDocumentAsync(int id, DocumentUpload? file, int? categoryId, CancellationToken cancellationToken);
    Task<bool> DeleteDocumentAsync(int id, CancellationToken cancellationToken = default);
}
