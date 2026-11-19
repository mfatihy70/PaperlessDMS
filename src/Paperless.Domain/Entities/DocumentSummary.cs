namespace Paperless.Domain.Entities;

public class DocumentSummary
{
    public int DocumentId { get; set; }
    public Guid JobId { get; set; }
    public Guid SourceJobId { get; set; }
    public string Text { get; set; } = "";
    public string Language { get; set; } = "";
    public string Provider { get; set; } = "";
    public string? Model { get; set; }
    public DateTime GeneratedAt { get; set; }
}
