namespace Paperless.Domain.Entities;

public enum ProcessingState { Queued, Processing, RetryScheduled, ReadyForOcr, Failed, RecognizingText, OcrCompleted }

public class DocumentProcessingJob
{
    public int DocumentId { get; set; }
    public Guid JobId { get; set; }
    public Guid CurrentMessageId { get; set; }
    public string StorageKey { get; set; } = string.Empty;
    public string FileSha256 { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public ProcessingState State { get; set; }
    public int Attempts { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public DateTime? NextAttemptAt { get; set; }
    public Guid? LeaseId { get; set; }
    public DateTime? LeaseUntil { get; set; }
    public string? LastErrorCode { get; set; }
    public string? LastError { get; set; }
}
