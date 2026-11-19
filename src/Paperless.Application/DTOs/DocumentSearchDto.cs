namespace Paperless.Application.DTOs;

/// <summary>A page of ranked matches validated against the current documents in PostgreSQL.</summary>
public class DocumentSearchDto
{
    /// <summary>The normalized search phrase.</summary>
    public string Query { get; set; } = string.Empty;
    /// <summary>Whether matching tolerates small spelling differences.</summary>
    public bool Fuzzy { get; set; }
    /// <summary>The requested one-based page.</summary>
    public int Page { get; set; }
    /// <summary>The maximum number of results per page.</summary>
    public int PageSize { get; set; }
    /// <summary>Indexed match count, or null while synchronization is pending or outdated matches were discarded.</summary>
    public long? TotalCount { get; set; }
    /// <summary>Whether additional indexed candidates exist after this page.</summary>
    public bool HasMore { get; set; }
    /// <summary>True when known indexing work/errors or discarded stale matches make the result incomplete. Retry after synchronization.</summary>
    public bool IsPartial { get; set; }
    /// <summary>Ranked, current document results. A page can be shorter during background synchronization.</summary>
    public IReadOnlyList<DocumentSearchHitDto> Items { get; set; } = [];
}

/// <summary>A matching current document with its search rank and optional plain-text excerpt.</summary>
public class DocumentSearchHitDto
{
    /// <summary>Current document metadata from PostgreSQL.</summary>
    public required DocumentResponseDto Document { get; set; }
    /// <summary>Relevance score within this search; higher values rank first.</summary>
    public double Score { get; set; }
    /// <summary>An optional OCR excerpt without added HTML highlighting markup.</summary>
    public string? Snippet { get; set; }
}

/// <summary>Current search-index synchronization status for a document.</summary>
public class DocumentIndexingDto
{
    /// <summary>The document ID.</summary>
    public int DocumentId { get; set; }
    /// <summary>Monotonically increasing index revision, or null before scheduling.</summary>
    public long? Revision { get; set; }
    /// <summary>NotQueued, Queued, Indexing, RetryScheduled, Indexed, AwaitingOcr or Failed.</summary>
    public string State { get; set; } = string.Empty;
    /// <summary>Whether the desired index entry has current recognized text.</summary>
    public bool HasOcrText { get; set; }
    /// <summary>Indexing attempts for this revision.</summary>
    public int Attempts { get; set; }
    /// <summary>Last status change in UTC.</summary>
    public DateTime? UpdatedAt { get; set; }
    /// <summary>Last confirmed index write in UTC.</summary>
    public DateTime? CompletedAt { get; set; }
    /// <summary>Earliest automatic retry time in UTC.</summary>
    public DateTime? NextAttemptAt { get; set; }
    /// <summary>Safe error code, or null when no failure is known.</summary>
    public string? LastErrorCode { get; set; }
    /// <summary>Safe explanation without internal exception details.</summary>
    public string? LastError { get; set; }
}

/// <summary>A durable reindex request; it does not run OCR or wait for Elasticsearch.</summary>
public class SearchReindexDto
{
    /// <summary>Number of document and deletion records scheduled.</summary>
    public int ScheduledCount { get; set; }
    /// <summary>When scheduling completed, in UTC.</summary>
    public DateTime RequestedAt { get; set; }
}
