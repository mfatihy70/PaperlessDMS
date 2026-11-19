using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Paperless.Application.Abstractions;
using Paperless.Application.DTOs;
using Paperless.Application.Exceptions;
using Paperless.Domain.Entities;
using Paperless.Infrastructure.Persistence;
using Paperless.Infrastructure.Processing;

namespace Paperless.Infrastructure.Search;

public sealed class SearchRepository(IDbContextFactory<PaperlessDbContext> factory, IOptions<SearchOptions> options,
    IOptions<ProcessingOptions> processing, ILogger<SearchRepository> logger) : ISearchRepository
{
    public async Task<DocumentSearchDto> ResolveAsync(SearchQuery query, SearchCandidates candidates, CancellationToken cancellationToken)
    {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        var ids = candidates.Items.Select(candidate => candidate.DocumentId).ToArray();
        var rows = await (from document in context.Documents.AsNoTracking()
                          join text in context.DocumentTexts on document.Id equals text.DocumentId
                          join job in context.ProcessingJobs on document.Id equals job.DocumentId
                          join index in context.SearchIndexJobs on document.Id equals index.DocumentId
                          where ids.Contains(document.Id) && text.StorageKey == document.StorageKey && text.JobId == job.JobId &&
                              job.State == ProcessingState.OcrCompleted && index.SourceJobId == text.JobId && index.Action == SearchIndexAction.Index
                          select new
                          {
                              index.Revision,
                              text.JobId,
                              Document = new DocumentResponseDto(document.Id, document.Title, document.FileName, document.ContentType,
                                  document.FileSizeBytes, document.CreatedAt, document.CategoryId,
                                  document.Category != null ? document.Category.Name : null, true)
                          }).ToDictionaryAsync(row => row.Document.Id, cancellationToken);
        var hits = new List<DocumentSearchHitDto>();
        foreach (var candidate in candidates.Items)
            if (rows.TryGetValue(candidate.DocumentId, out var row) && row.Revision == candidate.Revision && row.JobId == candidate.JobId)
                hits.Add(new DocumentSearchHitDto { Document = row.Document, Score = candidate.Score, Snippet = candidate.Snippet });
        var pending = await context.SearchIndexJobs.AnyAsync(job => job.State != SearchIndexState.Indexed && job.State != SearchIndexState.Removed,
            cancellationToken);
        var partial = pending || hits.Count != candidates.Items.Count;
        return new DocumentSearchDto
        {
            Query = query.Query,
            Fuzzy = query.Fuzzy,
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = partial ? null : candidates.Total,
            HasMore = (long)query.Page * query.PageSize < candidates.Total,
            IsPartial = partial,
            Items = hits
        };
    }

    public async Task<DocumentIndexingDto?> GetAsync(int id, CancellationToken cancellationToken)
    {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        var row = await (from document in context.Documents.AsNoTracking()
                         where document.Id == id
                         join entry in context.SearchIndexJobs on document.Id equals entry.DocumentId into jobs
                         from entry in jobs.DefaultIfEmpty()
                         join message in context.SearchOutbox on entry.MessageId equals message.Id into messages
                         from message in messages.DefaultIfEmpty()
                         select new { Job = entry, Error = message.LastError }).SingleOrDefaultAsync(cancellationToken);
        if (row is null) return null;
        if (row.Job is not { } job) return new DocumentIndexingDto { DocumentId = id, State = "NotQueued" };
        return new DocumentIndexingDto
        {
            DocumentId = id,
            Revision = job.Revision,
            State = job.State == SearchIndexState.Removed ? "AwaitingOcr" : job.State.ToString(),
            HasOcrText = job.SourceJobId.HasValue,
            Attempts = job.Attempts,
            UpdatedAt = job.UpdatedAt,
            CompletedAt = job.CompletedAt,
            NextAttemptAt = job.NextAttemptAt,
            LastErrorCode = job.LastErrorCode ?? (row.Error is not null ? "BrokerUnavailable" : null),
            LastError = job.LastError ?? row.Error
        };
    }

    public async Task<DocumentIndexingDto?> RetryAsync(int id, CancellationToken cancellationToken)
    {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        await using (var transaction = await context.Database.BeginTransactionAsync(cancellationToken))
        {
            await SearchIndexScheduler.LockAsync(context, id, cancellationToken);
            if (!await context.Documents.AnyAsync(document => document.Id == id, cancellationToken)) return null;
            var job = await context.SearchIndexJobs.AsNoTracking().SingleOrDefaultAsync(job => job.DocumentId == id, cancellationToken);
            if (job is not null && job.State != SearchIndexState.Failed)
                throw new ApplicationConflictException("Indexing is already queued, active or synchronized. A rebuild can explicitly reschedule synchronized entries.");
            await SearchIndexScheduler.QueueAsync(context, id, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        return await GetAsync(id, cancellationToken);
    }

    public async Task<SearchReindexDto> ReindexAsync(string? indexIdentity, CancellationToken cancellationToken)
    {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({8_000_000_000_000_000_000L})", cancellationToken);
        var catalog = await context.SearchIndexCatalog.SingleOrDefaultAsync(catalog => catalog.Id == 1, cancellationToken);
        if (indexIdentity is not null && catalog?.Identity == indexIdentity) return new SearchReindexDto { RequestedAt = DateTime.UtcNow };
        var ids = await context.Documents.Select(document => document.Id).Union(context.SearchIndexJobs.Select(job => job.DocumentId))
            .Order().ToListAsync(cancellationToken);
        foreach (var id in ids) await SearchIndexScheduler.QueueAsync(context, id, cancellationToken);
        if (indexIdentity is not null)
        {
            if (catalog is null) context.SearchIndexCatalog.Add(new SearchIndexCatalog { Id = 1, Identity = indexIdentity, UpdatedAt = DateTime.UtcNow });
            else { catalog.Identity = indexIdentity; catalog.UpdatedAt = DateTime.UtcNow; }
            await context.SaveChangesAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        logger.LogInformation("Scheduled {Count} search-index entries from PostgreSQL.", ids.Count);
        return new SearchReindexDto { ScheduledCount = ids.Count, RequestedAt = DateTime.UtcNow };
    }

    public async Task<IndexClaimResult> ClaimAsync(IndexDocumentMessage message, CancellationToken cancellationToken)
    {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var job = await LockJobAsync(context, message.DocumentId, cancellationToken);
        if (!Matches(job, message) || job!.State is SearchIndexState.Indexed or SearchIndexState.Removed or SearchIndexState.Failed) return new(false, null);
        var now = DateTime.UtcNow;
        if ((job.State == SearchIndexState.Indexing && job.LeaseUntil > now) || job.NextAttemptAt > now) return new(true, null);
        if (job.Attempts >= processing.Value.MaxAttempts)
        {
            SearchIndexScheduler.Fail(context, job, "AttemptLimitReached", "The indexing attempt limit was reached after interrupted attempts.");
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(false, null);
        }
        IndexDocument snapshot = new(message.DocumentId, message.Revision, true);
        if (job.Action == SearchIndexAction.Index)
        {
            var source = await (from document in context.Documents.AsNoTracking()
                                join text in context.DocumentTexts on document.Id equals text.DocumentId
                                join original in context.ProcessingJobs on document.Id equals original.DocumentId
                                where document.Id == message.DocumentId && text.JobId == job.SourceJobId &&
                                    original.JobId == text.JobId && original.State == ProcessingState.OcrCompleted && document.StorageKey == text.StorageKey
                                select new { document.Title, document.FileName, text.Text, text.JobId }).SingleOrDefaultAsync(cancellationToken);
            if (source is null)
            {
                SearchIndexScheduler.Fail(context, job, "IndexSourceChanged", "The recognized source is no longer current. Reschedule indexing from PostgreSQL.");
                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return new(false, null);
            }
            snapshot = new(message.DocumentId, message.Revision, false, source.JobId, source.Title, source.FileName, source.Text);
        }
        job.State = SearchIndexState.Indexing;
        job.Attempts++;
        job.UpdatedAt = now;
        job.CompletedAt = job.NextAttemptAt = null;
        job.LeaseId = Guid.NewGuid();
        job.LeaseUntil = now.AddSeconds(options.Value.LeaseSeconds);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(false, new IndexClaim(message, job.LeaseId.Value, snapshot));
    }

    public async Task RecoverUnavailableAsync(CancellationToken cancellationToken)
    {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        var ids = await context.SearchIndexJobs.Where(job => job.State == SearchIndexState.Failed && job.LastErrorCode == "SearchUnavailable")
            .OrderBy(job => job.DocumentId).Select(job => job.DocumentId).Take(100).ToListAsync(cancellationToken);
        foreach (var id in ids)
        {
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            await SearchIndexScheduler.LockAsync(context, id, cancellationToken);
            if (await context.SearchIndexJobs.AnyAsync(job => job.DocumentId == id && job.State == SearchIndexState.Failed &&
                job.LastErrorCode == "SearchUnavailable", cancellationToken))
                await SearchIndexScheduler.QueueAsync(context, id, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        if (ids.Count > 0) logger.LogInformation("Reconsidered {Count} unavailable index entries after search-service recovery.", ids.Count);
    }

    public async Task FinishAsync(IndexClaim claim, string? errorCode, string? error, bool retryable, CancellationToken cancellationToken)
    {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var job = await LockJobAsync(context, claim.Message.DocumentId, cancellationToken);
        if (!Matches(job, claim.Message) || job!.State != SearchIndexState.Indexing || job.LeaseId != claim.LeaseId) return;
        job.UpdatedAt = DateTime.UtcNow;
        job.LeaseId = null;
        job.LeaseUntil = null;
        if (errorCode is null)
        {
            job.State = job.Action == SearchIndexAction.Index ? SearchIndexState.Indexed : SearchIndexState.Removed;
            job.CompletedAt = job.UpdatedAt;
            job.LastErrorCode = job.LastError = null;
        }
        else if (retryable && job.Attempts < processing.Value.MaxAttempts)
        {
            job.State = SearchIndexState.RetryScheduled;
            job.LastErrorCode = errorCode;
            job.LastError = error;
            job.NextAttemptAt = job.UpdatedAt.AddSeconds(processing.Value.RetryDelaySeconds * job.Attempts);
            job.MessageId = Guid.NewGuid();
            SearchIndexScheduler.Enqueue(context, new IndexDocumentMessage(1, job.MessageId, job.DocumentId, job.Revision), job.NextAttemptAt.Value);
        }
        else SearchIndexScheduler.Fail(context, job, errorCode, error!);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        logger.LogInformation("Index synchronization for document {DocumentId}, revision {Revision}: {State}, error {ErrorCode}.",
            job.DocumentId, job.Revision, job.State, job.LastErrorCode);
    }

    private static bool Matches(SearchIndexJob? job, IndexDocumentMessage message) =>
        job is not null && job.Revision == message.Revision && job.MessageId == message.MessageId;

    private static Task<SearchIndexJob?> LockJobAsync(PaperlessDbContext context, int id, CancellationToken cancellationToken) =>
        context.SearchIndexJobs.FromSqlInterpolated($"SELECT * FROM public.\"SearchIndexJobs\" WHERE \"DocumentId\"={id} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
}
