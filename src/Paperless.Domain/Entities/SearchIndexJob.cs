namespace Paperless.Domain.Entities;

public enum SearchIndexAction { Index, Remove }
public enum SearchIndexState { Queued, Indexing, RetryScheduled, Indexed, Removed, Failed }

// Retained after document deletion so old deliveries cannot resurrect a removed search result.
public class SearchIndexJob
{
    public int DocumentId { get; set; }
    public long Revision { get; set; }
    public Guid MessageId { get; set; }
    public Guid? SourceJobId { get; set; }
    public SearchIndexAction Action { get; set; }
    public SearchIndexState State { get; set; }
    public int Attempts { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? NextAttemptAt { get; set; }
    public Guid? LeaseId { get; set; }
    public DateTime? LeaseUntil { get; set; }
    public string? LastErrorCode { get; set; }
    public string? LastError { get; set; }
}
