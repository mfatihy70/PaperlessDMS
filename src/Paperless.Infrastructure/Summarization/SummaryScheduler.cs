using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Paperless.Application.Abstractions;
using Paperless.Domain.Entities;
using Paperless.Infrastructure.Persistence;
using Paperless.Infrastructure.Processing;

namespace Paperless.Infrastructure.Summarization;

internal static class SummaryScheduler
{
    // Caller owns the transaction and holds the document row lock.
    internal static async Task QueueAsync(PaperlessDbContext context, int documentId, Guid sourceJobId, CancellationToken ct)
    {
        await context.SummaryJobs.Where(job => job.DocumentId == documentId).ExecuteDeleteAsync(ct);
        var job = new SummaryJob
        {
            DocumentId = documentId,
            JobId = Guid.NewGuid(),
            SourceJobId = sourceJobId,
            State = SummaryState.Queued,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.SummaryJobs.Add(job);
        Enqueue(context, job, job.CreatedAt);
        await context.SaveChangesAsync(ct);
    }

    internal static void Enqueue(PaperlessDbContext context, SummaryJob job, DateTime availableAt)
    {
        job.CurrentMessageId = Guid.NewGuid();
        context.SummaryOutbox.Add(new SummaryOutboxMessage
        {
            Id = job.CurrentMessageId,
            DocumentId = job.DocumentId,
            JobId = job.JobId,
            Payload = JsonSerializer.Serialize(Message(job), ProcessingScheduler.Json),
            CreatedAt = DateTime.UtcNow,
            AvailableAt = availableAt
        });
    }

    internal static SummarizeDocumentMessage Message(SummaryJob job) =>
        new(1, job.CurrentMessageId, job.JobId, job.DocumentId);

    internal static void Fail(PaperlessDbContext context, SummaryJob job, string code, string error)
    {
        job.State = SummaryState.Failed;
        job.LastErrorCode = code;
        job.LastError = error;
        job.FinishedAt = job.UpdatedAt = DateTime.UtcNow;
        job.NextAttemptAt = job.LeaseUntil = null;
        job.LeaseId = null;
        context.SummaryOutbox.Add(new SummaryOutboxMessage
        {
            Id = Guid.NewGuid(),
            DocumentId = job.DocumentId,
            JobId = job.JobId,
            IsDeadLetter = true,
            Payload = JsonSerializer.Serialize(new SummaryFailure(Message(job), code, error, job.Attempts, job.UpdatedAt), ProcessingScheduler.Json),
            CreatedAt = job.UpdatedAt,
            AvailableAt = job.UpdatedAt
        });
    }
}
