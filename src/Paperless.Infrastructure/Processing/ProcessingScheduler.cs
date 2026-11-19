using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Paperless.Application.Abstractions;
using Paperless.Domain.Entities;
using Paperless.Infrastructure.Persistence;
using Paperless.Infrastructure.Search;

namespace Paperless.Infrastructure.Processing;

internal static class ProcessingScheduler
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // Caller owns a transaction and has already locked/saved the document row.
    internal static async Task ScheduleAsync(PaperlessDbContext context, Document document, CancellationToken cancellationToken)
    {
        // Invalidate the former original's result in the same transaction as replacement/retry.
        await context.SummaryJobs.Where(job => job.DocumentId == document.Id).ExecuteDeleteAsync(cancellationToken);
        await context.DocumentTexts.Where(text => text.DocumentId == document.Id).ExecuteDeleteAsync(cancellationToken);
        var job = await context.ProcessingJobs.SingleOrDefaultAsync(j => j.DocumentId == document.Id, cancellationToken);
        if (job is null)
        {
            job = new DocumentProcessingJob { DocumentId = document.Id };
            context.ProcessingJobs.Add(job);
        }
        job.JobId = Guid.NewGuid();
        job.StorageKey = document.StorageKey!;
        job.FileSha256 = document.FileSha256!;
        job.FileSizeBytes = document.FileSizeBytes;
        job.State = ProcessingState.Queued;
        job.Attempts = 0;
        job.CreatedAt = job.UpdatedAt = DateTime.UtcNow;
        job.StartedAt = job.FinishedAt = job.NextAttemptAt = job.LeaseUntil = null;
        job.LeaseId = null;
        job.LastError = job.LastErrorCode = null;
        Enqueue(context, job, job.CreatedAt);
        await context.SaveChangesAsync(cancellationToken);
        await SearchIndexScheduler.QueueAsync(context, document.Id, cancellationToken);
    }

    internal static void Enqueue(PaperlessDbContext context, DocumentProcessingJob job, DateTime availableAt)
    {
        job.CurrentMessageId = Guid.NewGuid();
        var message = Message(job);
        context.ProcessingOutbox.Add(new ProcessingOutboxMessage
        {
            Id = message.MessageId,
            DocumentId = job.DocumentId,
            JobId = job.JobId,
            Payload = JsonSerializer.Serialize(message, Json),
            CreatedAt = DateTime.UtcNow,
            AvailableAt = availableAt
        });
    }

    internal static PrepareDocumentMessage Message(DocumentProcessingJob job) =>
        new(1, job.CurrentMessageId, job.JobId, job.DocumentId, job.StorageKey);

    internal static void Fail(PaperlessDbContext context, DocumentProcessingJob job, string code, string error)
    {
        job.State = ProcessingState.Failed;
        job.UpdatedAt = DateTime.UtcNow;
        job.FinishedAt = job.UpdatedAt;
        job.LastErrorCode = code;
        job.LastError = error;
        job.NextAttemptAt = job.LeaseUntil = null;
        job.LeaseId = null;
        context.ProcessingOutbox.Add(new ProcessingOutboxMessage
        {
            Id = Guid.NewGuid(),
            DocumentId = job.DocumentId,
            JobId = job.JobId,
            IsDeadLetter = true,
            Payload = JsonSerializer.Serialize(new PreparationFailure(Message(job), code, error, job.Attempts, job.FinishedAt.Value), Json),
            CreatedAt = job.FinishedAt.Value,
            AvailableAt = job.FinishedAt.Value
        });
    }
}
