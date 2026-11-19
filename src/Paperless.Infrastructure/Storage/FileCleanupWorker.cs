using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Paperless.Application.Abstractions;
using Paperless.Application.Exceptions;
using Paperless.Infrastructure.Persistence;

namespace Paperless.Infrastructure.Storage;

public sealed class FileCleanupWorker(
    IDbContextFactory<PaperlessDbContext> contextFactory,
    IFileStorage storage,
    IOptions<MinioStorageOptions> options,
    ILogger<FileCleanupWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.CleanupEnabled)
            return;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.CleanupIntervalSeconds));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    for (var count = 0; count < 25 && await ProcessNextAsync(stoppingToken); count++) { }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
                catch (Exception exception)
                {
                    logger.LogError(exception, "File cleanup could not complete; durable jobs will be retried.");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    public async Task<bool> ProcessNextAsync(CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        // The lease row also serializes cleanup against upload activation in SaveFileChangesAsync.
        var jobs = await context.FileCleanupJobs.FromSqlInterpolated($"""
            SELECT * FROM public."FileCleanupJobs" WHERE "NotBefore" <= {DateTime.UtcNow}
            ORDER BY "NotBefore", "ObjectKey" LIMIT 1 FOR UPDATE SKIP LOCKED
            """).ToListAsync(cancellationToken);
        if (jobs.Count == 0)
            return false;
        var job = jobs[0];
        if (await context.Documents.AnyAsync(document => document.StorageKey == job.ObjectKey, cancellationToken))
        {
            // A committed reference always wins; uncertain database commits must never delete live files.
            context.FileCleanupJobs.Remove(job);
        }
        else
        {
            try
            {
                await storage.DeleteAsync(job.ObjectKey, cancellationToken);
                context.FileCleanupJobs.Remove(job);
            }
            catch (Exception exception) when (exception is FileStorageUnavailableException or FileStorageIntegrityException)
            {
                job.Attempts = Math.Min(job.Attempts + 1, 1000);
                job.NotBefore = DateTime.UtcNow.AddSeconds(Math.Min(3600, 30 * Math.Pow(2, Math.Min(job.Attempts, 7))));
                logger.LogWarning("Cleanup of {ObjectKey} deferred after attempt {Attempt}.", job.ObjectKey, job.Attempts);
            }
        }
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }
}
