namespace Paperless.REST.DTOs;

/// <summary>Stored document metadata including its category and file availability.</summary>
/// <param name="Id">The generated document ID.</param>
/// <param name="Title">A title derived from the uploaded file name.</param>
/// <param name="FileName">The uploaded file name.</param>
/// <param name="ContentType">The uploaded file's media type.</param>
/// <param name="FileSizeBytes">The uploaded file size in bytes.</param>
/// <param name="CreatedAt">The creation timestamp in UTC.</param>
/// <param name="CategoryId">The assigned category ID, or null if unassigned.</param>
/// <param name="CategoryName">The assigned category name, or null if unassigned.</param>
/// <param name="HasFile">Whether original file bytes are stored.</param>
public sealed record DocumentResponseDto(
    int Id,
    string Title,
    string FileName,
    string ContentType,
    long FileSizeBytes,
    DateTime CreatedAt,
    int? CategoryId,
    string? CategoryName,
    bool HasFile);

/// <summary>A stored original document file returned to the file download endpoint.</summary>
public sealed record DocumentFileDto(string FileName, string ContentType, byte[] Content);

/// <summary>A new category for organizing documents.</summary>
/// <param name="Name">The category name, for example Invoices.</param>
/// <param name="Description">An optional description of the category.</param>
public record CreateCategoryDto(string Name, string? Description);

/// <summary>A stored document category.</summary>
/// <param name="Id">The generated category ID.</param>
/// <param name="Name">The category name.</param>
/// <param name="Description">The category description, or null if none was provided.</param>
public record CategoryResponseDto(int Id, string Name, string? Description);
