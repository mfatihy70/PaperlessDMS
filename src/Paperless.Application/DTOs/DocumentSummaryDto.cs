namespace Paperless.Application.DTOs;

/// <summary>A persisted summary of the current original's complete OCR text.</summary>
public class DocumentSummaryDto
{
    /// <summary>The document ID.</summary>
    public int DocumentId { get; set; }
    /// <summary>The summary generation ID.</summary>
    public Guid JobId { get; set; }
    /// <summary>The OCR generation used as input.</summary>
    public Guid SourceJobId { get; set; }
    /// <summary>Generated plain text. Empty when OCR contained no non-whitespace text.</summary>
    public string Text { get; set; } = "";
    /// <summary>Requested summary language.</summary>
    public string Language { get; set; } = "";
    /// <summary>Gemini, or None when empty OCR needed no external call.</summary>
    public string Provider { get; set; } = "";
    /// <summary>The model configured for generation; null for empty OCR.</summary>
    public string? Model { get; set; }
    /// <summary>When the result was saved, in UTC.</summary>
    public DateTime GeneratedAt { get; set; }
}

/// <summary>Summary job status, independent of OCR and search availability.</summary>
public class SummaryStatusDto
{
    /// <summary>The document ID.</summary>
    public int DocumentId { get; set; }
    /// <summary>The summary generation ID, or null before OCR completes.</summary>
    public Guid? JobId { get; set; }
    /// <summary>The source OCR generation, or null before scheduling.</summary>
    public Guid? SourceJobId { get; set; }
    /// <summary>AwaitingOcr, Queued, Summarizing, RetryScheduled, Completed or Failed. Queued jobs wait while the GenAI worker is disabled.</summary>
    public string State { get; set; } = "";
    /// <summary>Attempts, including interrupted worker executions.</summary>
    public int Attempts { get; set; }
    /// <summary>When the job was scheduled, in UTC.</summary>
    public DateTime? CreatedAt { get; set; }
    /// <summary>When its status last changed, in UTC.</summary>
    public DateTime? UpdatedAt { get; set; }
    /// <summary>When it completed or finally failed, in UTC.</summary>
    public DateTime? FinishedAt { get; set; }
    /// <summary>Earliest retry time, in UTC.</summary>
    public DateTime? NextAttemptAt { get; set; }
    /// <summary>When RabbitMQ confirmed the current message.</summary>
    public DateTime? PublishedAt { get; set; }
    /// <summary>Stable error code; credentials, OCR and provider responses are never included.</summary>
    public string? LastErrorCode { get; set; }
    /// <summary>A safe explanation of the latest generation or delivery failure.</summary>
    public string? LastError { get; set; }
}
