using Paperless.REST.DTOs;
using Paperless.REST.Models;
using Paperless.REST.Repositories;

namespace Paperless.REST.Services;

public class DocumentService : IDocumentService
{
    private readonly IDocumentRepository _docRepo;
    private readonly IRepository<DocumentCategory> _catRepo;

    public DocumentService(IDocumentRepository docRepo, IRepository<DocumentCategory> catRepo)
    {
        _docRepo = docRepo;
        _catRepo = catRepo;
    }

    public async Task<IEnumerable<DocumentResponseDto>> GetAllDocumentsAsync()
    {
        var docs = await _docRepo.GetAllWithCategoriesAsync();
        return docs.Select(d => new DocumentResponseDto(
            d.Id, d.Title, d.FileName, d.ContentType, d.FileSizeBytes, d.CreatedAt, d.CategoryId, d.Category?.Name));
    }

    public async Task<DocumentResponseDto?> GetDocumentByIdAsync(int id)
    {
        var d = await _docRepo.GetByIdWithCategoryAsync(id);
        if (d is null) return null;
        return new DocumentResponseDto(
            d.Id, d.Title, d.FileName, d.ContentType, d.FileSizeBytes, d.CreatedAt, d.CategoryId, d.Category?.Name);
    }

    public async Task<DocumentResponseDto> CreateDocumentAsync(CreateDocumentDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Title))
            throw new ArgumentException("Title cannot be empty");

        var doc = new Document
        {
            Title = dto.Title,
            FileName = dto.FileName,
            ContentType = dto.ContentType ?? "application/pdf",
            FileSizeBytes = dto.FileSizeBytes,
            CategoryId = dto.CategoryId,
            CreatedAt = DateTime.UtcNow
        };

        await _docRepo.AddAsync(doc);
        await _docRepo.SaveChangesAsync();

        var created = await _docRepo.GetByIdWithCategoryAsync(doc.Id);
        return new DocumentResponseDto(
            created!.Id, created.Title, created.FileName, created.ContentType, created.FileSizeBytes, created.CreatedAt, created.CategoryId, created.Category?.Name);
    }

    public async Task<bool> UpdateDocumentAsync(int id, UpdateDocumentDto dto)
    {
        var doc = await _docRepo.GetByIdAsync(id);
        if (doc is null) return false;

        doc.Title = dto.Title;
        doc.CategoryId = dto.CategoryId;
        _docRepo.Update(doc);
        await _docRepo.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteDocumentAsync(int id)
    {
        var doc = await _docRepo.GetByIdAsync(id);
        if (doc is null) return false;

        _docRepo.Delete(doc);
        await _docRepo.SaveChangesAsync();
        return true;
    }
}