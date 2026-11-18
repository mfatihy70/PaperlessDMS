namespace Paperless.Application.DTOs;

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

/// <summary>A framework-independent upload. The caller owns and disposes the content stream.</summary>
public sealed record DocumentUpload(string FileName, string? ContentType, long Length, Stream Content);
