using Paperless.REST.DTOs;

namespace Paperless.REST.Services;

public interface IDocumentService
{
    Task<IEnumerable<DocumentResponseDto>> GetAllDocumentsAsync();
    Task<DocumentResponseDto?> GetDocumentByIdAsync(int id);
    Task<DocumentResponseDto> CreateDocumentAsync(CreateDocumentDto dto);
    Task<bool> UpdateDocumentAsync(int id, UpdateDocumentDto dto);
    Task<bool> DeleteDocumentAsync(int id);
}