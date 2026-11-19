namespace Paperless.Domain.Entities;

public class DocumentText
{
    public int DocumentId { get; set; }
    public Guid JobId { get; set; }
    public string StorageKey { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public int PageCount { get; set; }
    public DateTime ExtractedAt { get; set; }
}
