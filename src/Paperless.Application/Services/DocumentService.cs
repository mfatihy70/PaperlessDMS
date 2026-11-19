using System.Net.Http.Headers;
using Paperless.Application.Abstractions;
using Paperless.Application.DTOs;
using Paperless.Application.Exceptions;
using Paperless.Application.Mapping;
using Paperless.Application.Storage;
using Paperless.Domain.Entities;

namespace Paperless.Application.Services;

public sealed class DocumentService : IDocumentService
{
    public const long MaxDocumentSizeBytes = 25 * 1024 * 1024;

    private readonly IDocumentRepository _docRepo;
    private readonly IRepository<DocumentCategory> _categoryRepo;
    private readonly IFileStorage _fileStorage;

    public DocumentService(IDocumentRepository docRepo, IRepository<DocumentCategory> categoryRepo, IFileStorage fileStorage)
    {
        _docRepo = docRepo;
        _categoryRepo = categoryRepo;
        _fileStorage = fileStorage;
    }

    public async Task<IEnumerable<DocumentResponseDto>> GetAllDocumentsAsync()
    {
        var documents = await _docRepo.GetAllWithCategoriesAsync();
        return documents.Select(ResponseMapper.ToResponse);
    }

    public async Task<DocumentResponseDto?> GetDocumentByIdAsync(int id)
    {
        ValidateId(id);
        var document = await _docRepo.GetByIdWithCategoryAsync(id);
        return document is null ? null : ResponseMapper.ToResponse(document);
    }

    public async Task<DocumentResponseDto> CreateDocumentAsync(
        DocumentUpload file,
        int? categoryId,
        CancellationToken cancellationToken)
    {
        await ValidateCategoryAsync(categoryId);
        var uploadedFile = await ReadUploadAsync(file, cancellationToken);
        var storedFile = await StoreVerifiedAsync(uploadedFile, cancellationToken);
        var title = Path.GetFileNameWithoutExtension(uploadedFile.FileName);
        if (string.IsNullOrWhiteSpace(title))
            title = uploadedFile.FileName;

        var document = new Document
        {
            Title = title.Length > 255 ? title[..255] : title,
            FileName = uploadedFile.FileName,
            ContentType = uploadedFile.ContentType,
            FileSizeBytes = uploadedFile.Content.Length,
            StorageKey = storedFile.Key,
            FileSha256 = storedFile.Sha256,
            CategoryId = categoryId,
            CreatedAt = DateTime.UtcNow
        };

        await _docRepo.AddAsync(document);
        await _docRepo.SaveFileChangesAsync(storedFile.Key, null, cancellationToken);

        var created = await _docRepo.GetByIdWithCategoryAsync(document.Id);
        return ResponseMapper.ToResponse(created ?? throw new InvalidOperationException("The saved document could not be loaded."));
    }

    public async Task<DocumentFileDto?> GetDocumentFileAsync(int id, CancellationToken cancellationToken = default)
    {
        ValidateId(id);
        var document = await _docRepo.GetByIdAsync(id);
        if (document is null)
            return null;

        if (document.StorageKey is not null)
        {
            try
            {
                var content = await _fileStorage.ReadAsync(document.StorageKey, cancellationToken);
                FileIntegrity.Verify(content, document.FileSha256, document.FileSizeBytes);
                return new DocumentFileDto(document.FileName, document.ContentType, content);
            }
            catch (Exception exception) when (
                exception is FileStorageUnavailableException or FileStorageIntegrityException &&
                document.FileContent is { Length: > 0 })
            {
                // A verified legacy copy remains available until the operator explicitly purges it.
                FileIntegrity.Verify(document.FileContent, document.FileSha256, document.FileSizeBytes);
                return ResponseMapper.ToFile(document);
            }
        }

        return document.FileContent is { Length: > 0 } ? ResponseMapper.ToFile(document) : null;
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
        string? uploadedKey = null;
        var previousKey = document.StorageKey;

        if (uploadedFile is not null)
        {
            var storedFile = await StoreVerifiedAsync(uploadedFile, cancellationToken);
            uploadedKey = storedFile.Key;
            var title = Path.GetFileNameWithoutExtension(uploadedFile.FileName);
            document.Title = string.IsNullOrWhiteSpace(title)
                ? uploadedFile.FileName
                : title.Length > 255 ? title[..255] : title;
            document.FileName = uploadedFile.FileName;
            document.ContentType = uploadedFile.ContentType;
            document.FileSizeBytes = uploadedFile.Content.Length;
            document.StorageKey = storedFile.Key;
            document.FileSha256 = storedFile.Sha256;
            document.FileContent = null;
        }

        document.CategoryId = categoryId;
        _docRepo.Update(document);
        await _docRepo.SaveFileChangesAsync(uploadedKey, uploadedKey is null ? null : previousKey, cancellationToken);
        if (uploadedKey is not null)
            await TryDeleteRetiredAsync(previousKey);
        return true;
    }

    public async Task<bool> DeleteDocumentAsync(int id, CancellationToken cancellationToken = default)
    {
        ValidateId(id);
        var document = await _docRepo.GetByIdAsync(id);
        if (document is null)
            return false;

        _docRepo.Delete(document);
        await _docRepo.SaveFileChangesAsync(null, document.StorageKey, cancellationToken);
        await TryDeleteRetiredAsync(document.StorageKey);
        return true;
    }

    private async Task<(string Key, string Sha256)> StoreVerifiedAsync(
        UploadedDocumentFile file, CancellationToken cancellationToken)
    {
        var sha256 = FileIntegrity.Sha256(file.Content);
        var key = await _docRepo.BeginFileUploadAsync(cancellationToken);
        await _fileStorage.WriteAsync(key, file.Content, file.ContentType, cancellationToken);
        var stored = await _fileStorage.ReadAsync(key, cancellationToken);
        FileIntegrity.Verify(stored, sha256, file.Content.LongLength);
        return (key, sha256);
    }

    private async Task TryDeleteRetiredAsync(string? key)
    {
        if (key is null)
            return;
        try
        {
            // Deletion is already durably queued; request cancellation must not discard it.
            await _fileStorage.DeleteAsync(key, CancellationToken.None);
        }
        catch (Exception exception) when (exception is FileStorageUnavailableException or FileStorageIntegrityException)
        {
            // The adapter logs the failure; the cleanup worker retries the durable job.
        }
    }

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
