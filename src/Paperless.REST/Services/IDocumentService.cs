using Microsoft.AspNetCore.Http;
using Paperless.REST.DTOs;

namespace Paperless.REST.Services;

public interface IDocumentService
{
    Task<IEnumerable<DocumentResponseDto>> GetAllDocumentsAsync();
    Task<DocumentResponseDto?> GetDocumentByIdAsync(int id);
    Task<DocumentResponseDto> CreateDocumentAsync(IFormFile file, int? categoryId, CancellationToken cancellationToken);
    Task<DocumentFileDto?> GetDocumentFileAsync(int id);
    Task<bool> UpdateDocumentAsync(int id, IFormFile? file, int? categoryId, CancellationToken cancellationToken);
    Task<bool> DeleteDocumentAsync(int id);
}
