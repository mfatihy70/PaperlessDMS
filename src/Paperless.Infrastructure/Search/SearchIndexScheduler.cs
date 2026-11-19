using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Paperless.Application.Abstractions;
using Paperless.Domain.Entities;
using Paperless.Infrastructure.Persistence;
using Paperless.Infrastructure.Processing;

namespace Paperless.Infrastructure.Search;

internal static class SearchIndexScheduler
{
    internal static Task<int> LockAsync(PaperlessDbContext context, int documentId, CancellationToken cancellationToken) =>
        context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({7_000_000_000_000_000_000L + documentId})", cancellationToken);

    // Caller owns a transaction. Serialize scheduling before reading the source and allocating its revision.
    internal static async Task QueueAsync(PaperlessDbContext context, int documentId, CancellationToken cancellationToken)
    {
        await LockAsync(context, documentId, cancellationToken);
        var sourceJobId = await (from document in context.Documents
                                 join text in context.DocumentTexts on document.Id equals text.DocumentId
                                 join job in context.ProcessingJobs on document.Id equals job.DocumentId
                                 where document.Id == documentId && document.StorageKey == text.StorageKey &&
                                     text.JobId == job.JobId && job.State == ProcessingState.OcrCompleted
                                 select (Guid?)text.JobId).SingleOrDefaultAsync(cancellationToken);
        var action = sourceJobId.HasValue ? SearchIndexAction.Index.ToString() : SearchIndexAction.Remove.ToString();
        var revision = await context.Database.SqlQuery<long>($"SELECT nextval('public.\"SearchIndexRevisions\"') AS \"Value\"").SingleAsync(cancellationToken);
        var messageId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO public."SearchIndexJobs"
                ("DocumentId", "Revision", "MessageId", "SourceJobId", "Action", "State", "Attempts", "CreatedAt", "UpdatedAt")
            VALUES ({documentId}, {revision}, {messageId}, {sourceJobId}, {action}, 'Queued', 0, {now}, {now})
            ON CONFLICT ("DocumentId") DO UPDATE SET
                "Revision" = EXCLUDED."Revision", "MessageId" = EXCLUDED."MessageId", "SourceJobId" = EXCLUDED."SourceJobId",
                "Action" = EXCLUDED."Action", "State" = 'Queued', "Attempts" = 0, "UpdatedAt" = EXCLUDED."UpdatedAt",
                "CompletedAt" = NULL, "NextAttemptAt" = NULL, "LeaseId" = NULL, "LeaseUntil" = NULL,
                "LastErrorCode" = NULL, "LastError" = NULL;
            """, cancellationToken);
        Enqueue(context, new IndexDocumentMessage(1, messageId, documentId, revision), now);
        await context.SaveChangesAsync(cancellationToken);
    }

    internal static void Enqueue(PaperlessDbContext context, IndexDocumentMessage message, DateTime availableAt)
    {
        context.SearchOutbox.Add(new SearchOutboxMessage
        {
            Id = message.MessageId,
            DocumentId = message.DocumentId,
            Revision = message.Revision,
            Payload = JsonSerializer.Serialize(message, ProcessingScheduler.Json),
            CreatedAt = DateTime.UtcNow,
            AvailableAt = availableAt
        });
    }

    internal static void Fail(PaperlessDbContext context, SearchIndexJob job, string code, string error)
    {
        job.State = SearchIndexState.Failed;
        job.LastErrorCode = code;
        job.LastError = error;
        job.UpdatedAt = DateTime.UtcNow;
        job.CompletedAt = job.NextAttemptAt = job.LeaseUntil = null;
        job.LeaseId = null;
        var message = new IndexDocumentMessage(1, job.MessageId, job.DocumentId, job.Revision);
        context.SearchOutbox.Add(new SearchOutboxMessage
        {
            Id = Guid.NewGuid(),
            DocumentId = job.DocumentId,
            Revision = job.Revision,
            IsDeadLetter = true,
            Payload = JsonSerializer.Serialize(new IndexFailure(message, code, error, job.Attempts, job.UpdatedAt), ProcessingScheduler.Json),
            CreatedAt = job.UpdatedAt,
            AvailableAt = job.UpdatedAt
        });
    }
}
