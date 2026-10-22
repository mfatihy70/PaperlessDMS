namespace Paperless.REST.DTOs;

public record CreateDocumentDto(string Title, string FileName, string? ContentType, long FileSizeBytes, int? CategoryId);
public record UpdateDocumentDto(string Title, int? CategoryId);
public record DocumentResponseDto(int Id, string Title, string FileName, string ContentType, long FileSizeBytes, DateTime CreatedAt, int? CategoryId, string? CategoryName);

public record CreateCategoryDto(string Name, string? Description);
public record UpdateCategoryDto(string Name, string? Description);
public record CategoryResponseDto(int Id, string Name, string? Description);