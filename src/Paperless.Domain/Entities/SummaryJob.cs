namespace Paperless.Domain.Entities;

public enum SummaryState { Queued, Summarizing, RetryScheduled, Completed, Failed }

public class SummaryJob
{
    public int DocumentId { get; set; }
    public Guid JobId { get; set; }
    public Guid SourceJobId { get; set; }
    public Guid CurrentMessageId { get; set; }
    public SummaryState State { get; set; }
    public int Attempts { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public DateTime? NextAttemptAt { get; set; }
    public Guid? LeaseId { get; set; }
    public DateTime? LeaseUntil { get; set; }
    public string? LastErrorCode { get; set; }
    public string? LastError { get; set; }
}
