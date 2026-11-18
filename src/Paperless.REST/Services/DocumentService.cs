using System.Net.Http.Headers;
using Microsoft.AspNetCore.Http;
using Paperless.REST.DTOs;
using Paperless.REST.Models;
using Paperless.REST.Repositories;

namespace Paperless.REST.Services;

public sealed class DocumentService : IDocumentService
{
    public const long MaxDocumentSizeBytes = 25 * 1024 * 1024;

    private readonly IDocumentRepository _docRepo;

    public DocumentService(IDocumentRepository docRepo)
    {
        _docRepo = docRepo;
    }

    public async Task<IEnumerable<DocumentResponseDto>> GetAllDocumentsAsync()
    {
        var documents = await _docRepo.GetAllWithCategoriesAsync();
        return documents.Select(ToResponse);
    }

    public async Task<DocumentResponseDto?> GetDocumentByIdAsync(int id)
    {
        var document = await _docRepo.GetByIdWithCategoryAsync(id);
        return document is null ? null : ToResponse(document);
    }

    public async Task<DocumentResponseDto> CreateDocumentAsync(
        IFormFile file,
        int? categoryId,
        CancellationToken cancellationToken)
    {
        var uploadedFile = await ReadUploadAsync(file, cancellationToken);
        var title = Path.GetFileNameWithoutExtension(uploadedFile.FileName);
        if (string.IsNullOrWhiteSpace(title))
            title = uploadedFile.FileName;

        var document = new Document
        {
            Title = title.Length > 255 ? title[..255] : title,
            FileName = uploadedFile.FileName,
            ContentType = uploadedFile.ContentType,
            FileSizeBytes = uploadedFile.Content.Length,
            FileContent = uploadedFile.Content,
            CategoryId = categoryId,
            CreatedAt = DateTime.UtcNow
        };

        await _docRepo.AddAsync(document);
        await _docRepo.SaveChangesAsync();

        var created = await _docRepo.GetByIdWithCategoryAsync(document.Id);
        return ToResponse(created!);
    }

    public async Task<DocumentFileDto?> GetDocumentFileAsync(int id)
    {
        var document = await _docRepo.GetByIdAsync(id);
        var content = document?.FileContent;
        if (document is null || content is not { Length: > 0 })
            return null;

        return new DocumentFileDto(document.FileName, document.ContentType, content);
    }

    public async Task<bool> UpdateDocumentAsync(
        int id,
        IFormFile? file,
        int? categoryId,
        CancellationToken cancellationToken)
    {
        var document = await _docRepo.GetByIdAsync(id);
        if (document is null)
            return false;

        var uploadedFile = file is null
            ? null
            : await ReadUploadAsync(file, cancellationToken);

        if (uploadedFile is not null)
        {
            var title = Path.GetFileNameWithoutExtension(uploadedFile.FileName);
            document.Title = string.IsNullOrWhiteSpace(title)
                ? uploadedFile.FileName
                : title.Length > 255 ? title[..255] : title;
            document.FileName = uploadedFile.FileName;
            document.ContentType = uploadedFile.ContentType;
            document.FileSizeBytes = uploadedFile.Content.Length;
            document.FileContent = uploadedFile.Content;
        }

        document.CategoryId = categoryId;
        _docRepo.Update(document);
        await _docRepo.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteDocumentAsync(int id)
    {
        var document = await _docRepo.GetByIdAsync(id);
        if (document is null)
            return false;

        _docRepo.Delete(document);
        await _docRepo.SaveChangesAsync();
        return true;
    }

    private static DocumentResponseDto ToResponse(Document document) =>
        new(
            document.Id,
            document.Title,
            document.FileName,
            document.ContentType,
            document.FileSizeBytes,
            document.CreatedAt,
            document.CategoryId,
            document.Category?.Name,
            document.HasFile || document.FileContent is { Length: > 0 });

    private static async Task<UploadedDocumentFile> ReadUploadAsync(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file.Length <= 0)
            throw new ArgumentException("The selected file is empty");
        if (file.Length > MaxDocumentSizeBytes)
            throw new ArgumentException($"File exceeds the {MaxDocumentSizeBytes}-byte limit");

        var fileName = Path.GetFileName(file.FileName.Replace('\\', '/'));
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("The uploaded file must have a name");
        if (fileName.Length > 255)
            throw new ArgumentException("File name cannot exceed 255 characters");
        if (fileName.Any(char.IsControl))
            throw new ArgumentException("File name contains invalid characters");

        await using var memory = new MemoryStream((int)file.Length);
        await file.CopyToAsync(memory, cancellationToken);
        if (memory.Length > MaxDocumentSizeBytes)
            throw new ArgumentException($"File exceeds the {MaxDocumentSizeBytes}-byte limit");

        var contentType = MediaTypeHeaderValue.TryParse(file.ContentType, out var mediaType)
            ? mediaType.MediaType ?? "application/octet-stream"
            : "application/octet-stream";

        return new UploadedDocumentFile(fileName, contentType, memory.ToArray());
    }

    private sealed record UploadedDocumentFile(string FileName, string ContentType, byte[] Content);
}
