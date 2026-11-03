namespace Paperless.REST.DTOs;

/// <summary>Metadata for a new document; no file contents are uploaded.</summary>
/// <param name="Title">The document title, for example Invoice October 2026.</param>
/// <param name="FileName">The original file name, for example invoice.pdf.</param>
/// <param name="ContentType">The MIME type. Null defaults to application/pdf.</param>
/// <param name="FileSizeBytes">The file size in bytes, for example 4096.</param>
/// <param name="CategoryId">An existing category ID, or null for an unassigned document.</param>
public record CreateDocumentDto(string Title, string FileName, string? ContentType, long FileSizeBytes, int? CategoryId);

/// <summary>Editable document metadata.</summary>
/// <param name="Title">The new document title.</param>
/// <param name="CategoryId">An existing category ID, or null to remove the category assignment.</param>
public record UpdateDocumentDto(string Title, int? CategoryId);

/// <summary>Stored document metadata including its category.</summary>
/// <param name="Id">The generated document ID.</param>
/// <param name="Title">The document title.</param>
/// <param name="FileName">The original file name.</param>
/// <param name="ContentType">The MIME type, for example application/pdf.</param>
/// <param name="FileSizeBytes">The file size in bytes.</param>
/// <param name="CreatedAt">The creation timestamp in UTC.</param>
/// <param name="CategoryId">The assigned category ID, or null if unassigned.</param>
/// <param name="CategoryName">The assigned category name, or null if unassigned.</param>
public record DocumentResponseDto(int Id, string Title, string FileName, string ContentType, long FileSizeBytes, DateTime CreatedAt, int? CategoryId, string? CategoryName);

/// <summary>A new category for organizing documents.</summary>
/// <param name="Name">The category name, for example Invoices.</param>
/// <param name="Description">An optional description of the category.</param>
public record CreateCategoryDto(string Name, string? Description);

/// <summary>A stored document category.</summary>
/// <param name="Id">The generated category ID.</param>
/// <param name="Name">The category name.</param>
/// <param name="Description">The category description, or null if none was provided.</param>
public record CategoryResponseDto(int Id, string Name, string? Description);
