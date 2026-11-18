namespace Paperless.Domain.Entities;

public class Document
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/pdf";
    public long FileSizeBytes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Original uploaded file bytes. Null for legacy records without an upload.</summary>
    public byte[]? FileContent { get; set; }

    public bool HasFile { get; set; }

    // Foreign Key to Additional Entity
    public int? CategoryId { get; set; }
    public DocumentCategory? Category { get; set; }
}
