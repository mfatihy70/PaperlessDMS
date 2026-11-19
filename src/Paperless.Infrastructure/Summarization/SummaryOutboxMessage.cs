namespace Paperless.Infrastructure.Summarization;

public class SummaryOutboxMessage
{
    public Guid Id { get; set; }
    public int DocumentId { get; set; }
    public Guid JobId { get; set; }
    public bool IsDeadLetter { get; set; }
    public string Payload { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime AvailableAt { get; set; }
    public DateTime? PublishedAt { get; set; }
    public int PublishAttempts { get; set; }
    public string? LastError { get; set; }
    public Guid? PublishLeaseId { get; set; }
    public DateTime? PublishLeaseUntil { get; set; }
}
