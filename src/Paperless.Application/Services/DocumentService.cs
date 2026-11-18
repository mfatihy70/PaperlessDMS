using System.Net.Http.Headers;
using Paperless.Application.Abstractions;
using Paperless.Application.DTOs;
using Paperless.Application.Exceptions;
using Paperless.Domain.Entities;

namespace Paperless.Application.Services;

public sealed class DocumentService : IDocumentService
{
    public const long MaxDocumentSizeBytes = 25 * 1024 * 1024;

    private readonly IDocumentRepository _docRepo;
    private readonly IRepository<DocumentCategory> _categoryRepo;

    public DocumentService(IDocumentRepository docRepo, IRepository<DocumentCategory> categoryRepo)
    {
        _docRepo = docRepo;
        _categoryRepo = categoryRepo;
    }

    public async Task<IEnumerable<DocumentResponseDto>> GetAllDocumentsAsync()
    {
        var documents = await _docRepo.GetAllWithCategoriesAsync();
        return documents.Select(ToResponse);
    }

    public async Task<DocumentResponseDto?> GetDocumentByIdAsync(int id)
    {
        ValidateId(id);
        var document = await _docRepo.GetByIdWithCategoryAsync(id);
        return document is null ? null : ToResponse(document);
    }

    public async Task<DocumentResponseDto> CreateDocumentAsync(
        DocumentUpload file,
        int? categoryId,
        CancellationToken cancellationToken)
    {
        await ValidateCategoryAsync(categoryId);
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
        return ToResponse(created ?? throw new InvalidOperationException("The saved document could not be loaded."));
    }

    public async Task<DocumentFileDto?> GetDocumentFileAsync(int id)
    {
        ValidateId(id);
        var document = await _docRepo.GetByIdAsync(id);
        var content = document?.FileContent;
        if (document is null || content is not { Length: > 0 })
            return null;

        return new DocumentFileDto(document.FileName, document.ContentType, content);
    }

    public async Task<bool> UpdateDocumentAsync(
        int id,
        DocumentUpload? file,
        int? categoryId,
        CancellationToken cancellationToken)
    {
        ValidateId(id);
        var document = await _docRepo.GetByIdAsync(id);
        if (document is null)
            return false;

        await ValidateCategoryAsync(categoryId);
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
        ValidateId(id);
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

    private async Task ValidateCategoryAsync(int? categoryId)
    {
        if (categoryId.HasValue &&
            (categoryId.Value <= 0 || await _categoryRepo.GetByIdAsync(categoryId.Value) is null))
        {
            throw new ApplicationValidationException("categoryId", "Choose an existing category or leave the document unassigned.");
        }
    }

    private static async Task<UploadedDocumentFile> ReadUploadAsync(
        DocumentUpload file,
        CancellationToken cancellationToken)
    {
        if (file.Length <= 0)
            throw new ApplicationValidationException("file", "The selected file is empty.");
        if (file.Length > MaxDocumentSizeBytes)
            throw new ApplicationValidationException("file", $"File exceeds the {MaxDocumentSizeBytes}-byte limit.");

        if (string.IsNullOrWhiteSpace(file.FileName))
            throw new ApplicationValidationException("file", "The uploaded file must have a name.");

        var fileName = Path.GetFileName(file.FileName.Replace('\\', '/'));
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ApplicationValidationException("file", "The uploaded file must have a name.");
        if (fileName.Length > 255)
            throw new ApplicationValidationException("file", "File name cannot exceed 255 characters.");
        if (fileName.Any(char.IsControl))
            throw new ApplicationValidationException("file", "File name contains invalid characters.");

        if (file.Content is null || !file.Content.CanRead)
            throw new ApplicationValidationException("file", "The uploaded file cannot be read.");

        await using var memory = new MemoryStream((int)file.Length);
        var buffer = new byte[81920];
        int bytesRead;
        while ((bytesRead = await file.Content.ReadAsync(buffer, cancellationToken)) > 0)
        {
            if (memory.Length + bytesRead > MaxDocumentSizeBytes)
                throw new ApplicationValidationException("file", $"File exceeds the {MaxDocumentSizeBytes}-byte limit.");

            await memory.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
        }

        if (memory.Length == 0)
            throw new ApplicationValidationException("file", "The selected file is empty.");

        var contentType = MediaTypeHeaderValue.TryParse(file.ContentType, out var mediaType)
            ? mediaType.MediaType ?? "application/octet-stream"
            : "application/octet-stream";

        return new UploadedDocumentFile(fileName, contentType, memory.ToArray());
    }

    private sealed record UploadedDocumentFile(string FileName, string ContentType, byte[] Content);

    private static void ValidateId(int id)
    {
        if (id <= 0)
            throw new ApplicationValidationException("id", "The document ID must be a positive integer.");
    }
}
