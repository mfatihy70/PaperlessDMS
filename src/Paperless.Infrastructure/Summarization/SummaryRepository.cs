using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Paperless.Application.Abstractions;
using Paperless.Application.DTOs;
using Paperless.Application.Exceptions;
using Paperless.Domain.Entities;
using Paperless.Infrastructure.Persistence;
using Paperless.Infrastructure.Processing;

namespace Paperless.Infrastructure.Summarization;

public sealed class SummaryRepository(IDbContextFactory<PaperlessDbContext> factory, IOptions<ProcessingOptions> processing) : ISummaryRepository
{
    public async Task<SummaryStatusDto?> GetAsync(int documentId, CancellationToken ct)
    {
        await using var context = await factory.CreateDbContextAsync(ct);
        var row = await (from doc in context.Documents.AsNoTracking()
                         where doc.Id == documentId
                         join job in context.SummaryJobs on doc.Id equals job.DocumentId into jobs
                         from job in jobs.DefaultIfEmpty()
                         join message in context.SummaryOutbox on job.CurrentMessageId equals message.Id into messages
                         from message in messages.DefaultIfEmpty()
                         select new { Job = job, PublishedAt = (DateTime?)message.PublishedAt, DeliveryError = message.LastError })
            .SingleOrDefaultAsync(ct);
        if (row is null) return null;
        var current = row.Job;
        return new SummaryStatusDto
        {
            DocumentId = documentId,
            JobId = current?.JobId,
            SourceJobId = current?.SourceJobId,
            State = current?.State.ToString() ?? "AwaitingOcr",
            Attempts = current?.Attempts ?? 0,
            CreatedAt = current?.CreatedAt,
            UpdatedAt = current?.UpdatedAt,
            FinishedAt = current?.FinishedAt,
            NextAttemptAt = current?.NextAttemptAt,
            PublishedAt = row.PublishedAt,
            LastErrorCode = current?.LastErrorCode ?? (row.DeliveryError is null ? null : "BrokerUnavailable"),
            LastError = current?.LastError ?? row.DeliveryError
        };
    }

    public async Task<DocumentSummaryDto?> GetSummaryAsync(int documentId, CancellationToken ct)
    {
        await using var context = await factory.CreateDbContextAsync(ct);
        var result = await (from summary in context.DocumentSummaries.AsNoTracking()
                            join job in context.SummaryJobs on summary.DocumentId equals job.DocumentId
                            join text in context.DocumentTexts on summary.DocumentId equals text.DocumentId
                            join doc in context.Documents on text.DocumentId equals doc.Id
                            where doc.Id == documentId && job.State == SummaryState.Completed && job.JobId == summary.JobId &&
                                summary.SourceJobId == text.JobId && job.SourceJobId == text.JobId && text.StorageKey == doc.StorageKey
                            select new DocumentSummaryDto
                            {
                                DocumentId = doc.Id,
                                JobId = summary.JobId,
                                SourceJobId = summary.SourceJobId,
                                Text = summary.Text,
                                Language = summary.Language,
                                Provider = summary.Provider,
                                Model = summary.Model,
                                GeneratedAt = summary.GeneratedAt
                            }).SingleOrDefaultAsync(ct);
        if (result is not null) return result;
        if (!await context.Documents.AnyAsync(doc => doc.Id == documentId, ct)) return null;
        throw new ApplicationConflictException("No summary is available for the current original. Read its summarization and OCR processing status.");
    }

    public async Task<SummaryStatusDto?> RetryAsync(int documentId, CancellationToken ct)
    {
        await using var context = await factory.CreateDbContextAsync(ct);
        await using (var transaction = await context.Database.BeginTransactionAsync(ct))
        {
            await LockAsync(context, documentId, ct);
            if (!await context.Documents.AnyAsync(doc => doc.Id == documentId, ct)) return null;
            var sourceId = await (from text in context.DocumentTexts
                                  join doc in context.Documents on text.DocumentId equals doc.Id
                                  join ocr in context.ProcessingJobs on doc.Id equals ocr.DocumentId
                                  where doc.Id == documentId && doc.StorageKey == text.StorageKey &&
                                      ocr.JobId == text.JobId && ocr.State == ProcessingState.OcrCompleted
                                  select (Guid?)text.JobId).SingleOrDefaultAsync(ct);
            if (sourceId is null) throw new ApplicationConflictException("Complete OCR for the current original before scheduling a summary.");
            var job = await context.SummaryJobs.AsNoTracking().SingleOrDefaultAsync(job => job.DocumentId == documentId, ct);
            if (job is not null && job.State != SummaryState.Failed)
                throw new ApplicationConflictException("The summary is already scheduled, being generated or completed.");
            await SummaryScheduler.QueueAsync(context, documentId, sourceId.Value, ct);
            await transaction.CommitAsync(ct);
        }
        return await GetAsync(documentId, ct);
    }

    public async Task<SummaryClaimResult> ClaimAsync(SummarizeDocumentMessage message, CancellationToken ct)
    {
        await using var context = await factory.CreateDbContextAsync(ct);
        await using var transaction = await context.Database.BeginTransactionAsync(ct);
        await LockAsync(context, message.DocumentId, ct);
        var job = await context.SummaryJobs.SingleOrDefaultAsync(job => job.DocumentId == message.DocumentId, ct);
        if (!Matches(job, message) || job!.State is SummaryState.Completed or SummaryState.Failed) return new(false, null);
        var now = DateTime.UtcNow;
        if ((job.State == SummaryState.Summarizing && job.LeaseUntil > now) || job.NextAttemptAt > now) return new(true, null);
        if (job.Attempts >= processing.Value.MaxAttempts)
        {
            SummaryScheduler.Fail(context, job, "AttemptLimitReached", "The summary attempt limit was reached after interrupted worker attempts.");
            await context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return new(false, null);
        }
        var text = await (from source in context.DocumentTexts
                          join doc in context.Documents on source.DocumentId equals doc.Id
                          where doc.Id == message.DocumentId && source.JobId == job.SourceJobId && source.StorageKey == doc.StorageKey
                          select source.Text).SingleOrDefaultAsync(ct);
        if (text is null) return new(false, null);
        job.State = SummaryState.Summarizing;
        job.Attempts++;
        job.UpdatedAt = now;
        job.NextAttemptAt = null;
        job.LeaseId = Guid.NewGuid();
        // Shared lease is validated by worker setup against the provider timeout.
        job.LeaseUntil = now.AddSeconds(processing.Value.LeaseSeconds);
        await context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(false, new SummaryClaim(message, job.LeaseId.Value, text));
    }

    public async Task FinishAsync(SummaryClaim claim, SummaryGeneration? result, string? code, string? error, bool retryable, CancellationToken ct)
    {
        if (code is null && result is null) throw new ArgumentException("A successful summary requires a result.", nameof(result));
        await using var context = await factory.CreateDbContextAsync(ct);
        await using var transaction = await context.Database.BeginTransactionAsync(ct);
        await LockAsync(context, claim.Message.DocumentId, ct);
        var job = await context.SummaryJobs.SingleOrDefaultAsync(job => job.DocumentId == claim.Message.DocumentId, ct);
        if (!Matches(job, claim.Message) || job!.State != SummaryState.Summarizing || job.LeaseId != claim.LeaseId) return;
        job.UpdatedAt = DateTime.UtcNow;
        job.LeaseId = null;
        job.LeaseUntil = null;
        if (code is null)
        {
            job.State = SummaryState.Completed;
            job.FinishedAt = job.UpdatedAt;
            job.LastErrorCode = job.LastError = null;
            context.DocumentSummaries.Add(new DocumentSummary
            {
                DocumentId = job.DocumentId,
                JobId = job.JobId,
                SourceJobId = job.SourceJobId,
                Text = result!.Text,
                Language = result.Language,
                Provider = result.Provider,
                Model = result.Model,
                GeneratedAt = job.UpdatedAt
            });
        }
        else if (retryable && job.Attempts < processing.Value.MaxAttempts)
        {
            job.State = SummaryState.RetryScheduled;
            job.LastErrorCode = code;
            job.LastError = error;
            job.NextAttemptAt = job.UpdatedAt.AddSeconds(processing.Value.RetryDelaySeconds * job.Attempts);
            SummaryScheduler.Enqueue(context, job, job.NextAttemptAt.Value);
        }
        else SummaryScheduler.Fail(context, job, code, error!);
        await context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private static bool Matches(SummaryJob? job, SummarizeDocumentMessage message) =>
        job is not null && job.JobId == message.JobId && job.CurrentMessageId == message.MessageId;
    private static Task<int> LockAsync(PaperlessDbContext context, int id, CancellationToken ct) =>
        context.Database.ExecuteSqlInterpolatedAsync($"SELECT \"Id\" FROM public.\"Documents\" WHERE \"Id\" = {id} FOR UPDATE", ct);
}
