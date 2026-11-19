namespace Paperless.Application.DTOs;

/// <summary>The current original's background status. OcrCompleted means its recognized text is saved and available.</summary>
public class ProcessingStatusDto
{
    /// <summary>The document ID.</summary>
    public int DocumentId { get; set; }
    /// <summary>The current file generation's processing ID; null when no job exists.</summary>
    public Guid? JobId { get; set; }
    /// <summary>Queued, Processing (original verification), RecognizingText, RetryScheduled, OcrCompleted or Failed. ReadyForOcr is a legacy preparation state. Without a job: NoOriginal, RequiresMigration or NotQueued.</summary>
    public string State { get; set; } = string.Empty;
    /// <summary>Worker attempts for this job, including recovered attempts after a crash.</summary>
    public int Attempts { get; set; }
    /// <summary>When this job was created, in UTC.</summary>
    public DateTime? CreatedAt { get; set; }
    /// <summary>When the job last changed, in UTC.</summary>
    public DateTime? UpdatedAt { get; set; }
    /// <summary>When the latest worker attempt started, in UTC.</summary>
    public DateTime? StartedAt { get; set; }
    /// <summary>When OCR succeeded or processing finally failed, in UTC.</summary>
    public DateTime? FinishedAt { get; set; }
    /// <summary>The earliest scheduled retry time, in UTC.</summary>
    public DateTime? NextAttemptAt { get; set; }
    /// <summary>When RabbitMQ last confirmed the current message. Null until confirmation.</summary>
    public DateTime? PublishedAt { get; set; }
    /// <summary>A stable failure code, such as StorageUnavailable, IntegrityCheckFailed or BrokerUnavailable.</summary>
    public string? LastErrorCode { get; set; }
    /// <summary>A safe explanation of the latest processing or delivery failure, without credentials or internal exception details.</summary>
    public string? LastError { get; set; }
}
