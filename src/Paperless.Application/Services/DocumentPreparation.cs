using Paperless.Application.Abstractions;
using Paperless.Application.Exceptions;
using Paperless.Application.Storage;

namespace Paperless.Application.Services;

/// <summary>Verifies the exact original referenced by a claimed job. OCR is a subsequent work package.</summary>
public sealed class DocumentPreparation(IProcessingRepository repository, IFileStorage storage)
{
    // False means another worker still owns the lease. Keep the delivery unacknowledged and try again.
    public async Task<bool> TryProcessAsync(PrepareDocumentMessage message, CancellationToken cancellationToken)
    {
        var result = await repository.ClaimAsync(message, cancellationToken);
        if (result.Busy) return false;
        if (result.Claim is not { } claim) return true; // Deleted, superseded or already completed.

        string? errorCode = null;
        string? error = null;
        var retryable = false;
        try
        {
            var original = await storage.ReadAsync(message.StorageKey, cancellationToken);
            FileIntegrity.Verify(original, claim.FileSha256, claim.FileSizeBytes);
        }
        catch (FileStorageUnavailableException)
        {
            errorCode = "StorageUnavailable";
            error = "The original file store is unavailable. Preparation will be retried within the configured attempt limit.";
            retryable = true;
        }
        catch (FileStorageIntegrityException)
        {
            errorCode = "IntegrityCheckFailed";
            error = "The original is missing or its size or SHA-256 does not match the stored metadata.";
        }
        await repository.FinishAsync(claim, errorCode, error, retryable, cancellationToken);
        return true;
    }
}
