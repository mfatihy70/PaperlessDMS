using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Paperless.Application.Abstractions;
using Paperless.Application.DTOs;
using Paperless.Application.Exceptions;
using Paperless.Domain.Entities;
using Paperless.Infrastructure.Persistence;

namespace Paperless.Infrastructure.Processing;

public sealed class ProcessingRepository(IDbContextFactory<PaperlessDbContext> factory, IOptions<ProcessingOptions> options) : IProcessingRepository
{
    public async Task<ProcessingStatusDto?> GetAsync(int documentId, CancellationToken cancellationToken)
    {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        var row = await (from document in context.Documents.AsNoTracking()
                         where document.Id == documentId
                         join job in context.ProcessingJobs on document.Id equals job.DocumentId into jobs
                         from job in jobs.DefaultIfEmpty()
                         join message in context.ProcessingOutbox on job.CurrentMessageId equals message.Id into messages
                         from message in messages.DefaultIfEmpty()
                         select new
                         {
                             document.Id,
                             document.StorageKey,
                             HasLegacy = document.FileContent != null && document.FileContent.Length > 0,
                             Job = job,
                             PublishedAt = (DateTime?)message.PublishedAt,
                             DeliveryError = message.LastError
                         }).SingleOrDefaultAsync(cancellationToken);
        if (row is null) return null;
        if (row.Job is not { } current)
            return new ProcessingStatusDto
            {
                DocumentId = documentId,
                State = row.StorageKey is not null ? "NotQueued" : row.HasLegacy ? "RequiresMigration" : "NoOriginal"
            };
        return new ProcessingStatusDto
        {
            DocumentId = documentId,
            JobId = current.JobId,
            State = current.State.ToString(),
            Attempts = current.Attempts,
            CreatedAt = current.CreatedAt,
            UpdatedAt = current.UpdatedAt,
            StartedAt = current.StartedAt,
            FinishedAt = current.FinishedAt,
            NextAttemptAt = current.NextAttemptAt,
            PublishedAt = row.PublishedAt,
            LastErrorCode = current.LastErrorCode ?? (row.DeliveryError is not null ? "BrokerUnavailable" : null),
            LastError = current.LastError ?? row.DeliveryError
        };
    }

    public async Task<ProcessingStatusDto?> RetryAsync(int documentId, CancellationToken cancellationToken)
    {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        await using (var transaction = await context.Database.BeginTransactionAsync(cancellationToken))
        {
            await LockDocumentAsync(context, documentId, cancellationToken);
            var document = await context.Documents.AsNoTracking().Where(d => d.Id == documentId)
                .Select(d => new Document { Id = d.Id, StorageKey = d.StorageKey, FileSha256 = d.FileSha256, FileSizeBytes = d.FileSizeBytes })
                .SingleOrDefaultAsync(cancellationToken);
            if (document is null) return null;
            if (document.StorageKey is null || document.FileSha256 is null || document.FileSizeBytes <= 0)
                throw new ApplicationConflictException("A verified original in MinIO is required. Migrate a legacy original or upload a file first.");
            var job = await context.ProcessingJobs.SingleOrDefaultAsync(j => j.DocumentId == documentId, cancellationToken);
            if (job is not null && job.StorageKey == document.StorageKey && job.State != ProcessingState.Failed)
                throw new ApplicationConflictException("This original is already scheduled, being processed or ready for OCR.");
            await ProcessingScheduler.ScheduleAsync(context, document, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        return await GetAsync(documentId, cancellationToken);
    }

    public async Task<PreparationClaimResult> ClaimAsync(PrepareDocumentMessage message, CancellationToken cancellationToken)
    {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await LockDocumentAsync(context, message.DocumentId, cancellationToken);
        var job = await context.ProcessingJobs.SingleOrDefaultAsync(j => j.DocumentId == message.DocumentId, cancellationToken);
        if (!Matches(job, message) || job!.State is ProcessingState.ReadyForOcr or ProcessingState.Failed)
            return new(false, null);
        var now = DateTime.UtcNow;
        if ((job.State == ProcessingState.Processing && job.LeaseUntil > now) || job.NextAttemptAt > now)
            return new(true, null);
        if (job.Attempts >= options.Value.MaxAttempts)
        {
            ProcessingScheduler.Fail(context, job, "AttemptLimitReached", "The preparation attempt limit was reached after interrupted worker attempts.");
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(false, null);
        }
        job.State = ProcessingState.Processing;
        job.Attempts++;
        job.UpdatedAt = now;
        job.StartedAt = now;
        job.NextAttemptAt = null;
        job.LeaseId = Guid.NewGuid();
        job.LeaseUntil = now.AddSeconds(options.Value.LeaseSeconds);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(false, new PreparationClaim(message, job.LeaseId.Value, job.FileSha256, job.FileSizeBytes));
    }

    public async Task FinishAsync(PreparationClaim claim, string? errorCode, string? error, bool retryable, CancellationToken cancellationToken)
    {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await LockDocumentAsync(context, claim.Message.DocumentId, cancellationToken);
        var job = await context.ProcessingJobs.SingleOrDefaultAsync(j => j.DocumentId == claim.Message.DocumentId, cancellationToken);
        if (!Matches(job, claim.Message) || job!.State != ProcessingState.Processing || job.LeaseId != claim.LeaseId) return;
        job.UpdatedAt = DateTime.UtcNow;
        job.LeaseId = null;
        job.LeaseUntil = null;
        if (errorCode is null)
        {
            job.State = ProcessingState.ReadyForOcr;
            job.FinishedAt = job.UpdatedAt;
            job.LastError = job.LastErrorCode = null;
        }
        else if (retryable && job.Attempts < options.Value.MaxAttempts)
        {
            job.State = ProcessingState.RetryScheduled;
            job.LastErrorCode = errorCode;
            job.LastError = error;
            job.NextAttemptAt = job.UpdatedAt.AddSeconds(options.Value.RetryDelaySeconds * job.Attempts);
            ProcessingScheduler.Enqueue(context, job, job.NextAttemptAt.Value);
        }
        else
        {
            ProcessingScheduler.Fail(context, job, errorCode, error!);
        }
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static bool Matches(DocumentProcessingJob? job, PrepareDocumentMessage message) =>
        job is not null && job.JobId == message.JobId && job.CurrentMessageId == message.MessageId && job.StorageKey == message.StorageKey;

    // Every job mutation takes the document row first. Upload/replace/delete already take that same row through EF.
    private static Task<int> LockDocumentAsync(PaperlessDbContext context, int documentId, CancellationToken cancellationToken) =>
        context.Database.ExecuteSqlInterpolatedAsync($"SELECT \"Id\" FROM public.\"Documents\" WHERE \"Id\" = {documentId} FOR UPDATE", cancellationToken);
}
