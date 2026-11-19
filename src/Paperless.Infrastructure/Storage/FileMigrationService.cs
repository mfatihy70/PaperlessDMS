using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Paperless.Application.Abstractions;
using Paperless.Application.Exceptions;
using Paperless.Application.Services;
using Paperless.Application.Storage;
using Paperless.Infrastructure.Persistence;

namespace Paperless.Infrastructure.Storage;

/// <summary>Explicit, resumable operator commands for moving and verifying legacy originals.</summary>
public sealed class FileMigrationService(
    PaperlessDbContext context,
    IDocumentRepository repository,
    IFileStorage storage,
    FileCleanupWorker cleanup,
    ILogger<FileMigrationService> logger)
{
    public async Task<int> RunAsync(string operation, CancellationToken cancellationToken)
    {
        if (operation == "status")
        {
            await LogStatusAsync(cancellationToken);
            return 0;
        }
        if (operation == "cleanup")
        {
            for (var count = 0; count < 100 && await cleanup.ProcessNextAsync(cancellationToken); count++) { }
            await LogStatusAsync(cancellationToken);
            return 0;
        }
        if (operation is not ("migrate" or "verify" or "purge"))
            throw new ArgumentException("Use files status, migrate, verify, purge or cleanup.", nameof(operation));

        var processed = 0;
        var failures = 0;
        var lastId = 0;
        while (true)
        {
            var query = context.Documents.AsNoTracking().Where(document => document.Id > lastId);
            query = operation == "migrate"
                ? query.Where(document => document.StorageKey == null && document.FileContent != null)
                : query.Where(document => document.StorageKey != null);
            if (operation == "purge")
                query = query.Where(document => document.FileContent != null);
            var ids = await query.OrderBy(document => document.Id).Select(document => document.Id).Take(50).ToListAsync(cancellationToken);
            if (ids.Count == 0)
                break;
            foreach (var id in ids)
            {
                lastId = id;
                try
                {
                    var document = await context.Documents.SingleOrDefaultAsync(document => document.Id == id, cancellationToken);
                    if (document is null)
                        continue;
                    if (operation == "migrate")
                    {
                        if (document.StorageKey is not null || document.FileContent is not { Length: > 0 } source)
                            continue;
                        if (source.LongLength > DocumentService.MaxDocumentSizeBytes)
                            throw new FileStorageIntegrityException("Legacy original exceeds the supported 25 MiB limit; its database copy is retained.");
                        var sha256 = FileIntegrity.Sha256(source);
                        var key = await repository.BeginFileUploadAsync(cancellationToken);
                        await storage.WriteAsync(key, source, document.ContentType, cancellationToken);
                        var stored = await storage.ReadAsync(key, cancellationToken);
                        FileIntegrity.Verify(stored, sha256, source.LongLength);
                        document.StorageKey = key;
                        document.FileSha256 = sha256;
                        document.FileSizeBytes = source.LongLength;
                        // Keep the source bytes until the separate purge command verifies them again.
                        await repository.SaveFileChangesAsync(key, null, cancellationToken);
                    }
                    else
                    {
                        if (document.StorageKey is null)
                            continue;
                        var stored = await storage.ReadAsync(document.StorageKey, cancellationToken);
                        FileIntegrity.Verify(stored, document.FileSha256, document.FileSizeBytes);
                        if (document.FileContent is { Length: > 0 } source)
                            FileIntegrity.Verify(source, document.FileSha256, document.FileSizeBytes);
                        if (operation == "purge")
                        {
                            document.FileContent = null;
                            await context.SaveChangesAsync(cancellationToken);
                        }
                    }
                    processed++;
                    logger.LogInformation("File command {Operation} completed for document {DocumentId}.", operation, id);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                catch (Exception exception)
                {
                    failures++;
                    logger.LogError("File command {Operation} failed for document {DocumentId}: {FailureType}: {Reason}",
                        operation, id, exception.GetType().Name, exception.Message);
                }
                finally
                {
                    context.ChangeTracker.Clear();
                }
            }
        }
        if (operation == "purge" && await context.Documents.AnyAsync(
            document => document.FileContent != null && document.FileContent.Length > 0, cancellationToken))
        {
            failures++;
            logger.LogWarning("Database originals remain. Successfully migrate and verify them before repeating purge.");
        }
        logger.LogInformation("File command {Operation}: {Processed} succeeded, {Failures} failed.", operation, processed, failures);
        await LogStatusAsync(cancellationToken);
        return failures == 0 ? 0 : 1;
    }

    private async Task LogStatusAsync(CancellationToken cancellationToken)
    {
        var legacy = await context.Documents.CountAsync(document => document.StorageKey == null &&
            document.FileContent != null && document.FileContent.Length > 0, cancellationToken);
        var stored = await context.Documents.CountAsync(document => document.StorageKey != null, cancellationToken);
        var copies = await context.Documents.CountAsync(document => document.StorageKey != null && document.FileContent != null, cancellationToken);
        var pending = await context.FileCleanupJobs.CountAsync(cancellationToken);
        logger.LogInformation("Files: {Legacy} legacy originals, {Stored} object-storage references, {Copies} retained database copies, {Pending} cleanup jobs.",
            legacy, stored, copies, pending);
    }
}
