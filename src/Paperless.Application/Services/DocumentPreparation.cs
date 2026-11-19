using Paperless.Application.Abstractions;
using Paperless.Application.Exceptions;
using Paperless.Application.Storage;

namespace Paperless.Application.Services;

/// <summary>Verifies the claimed original, recognizes its text and atomically saves the result and job status.</summary>
public sealed class DocumentPreparation(IProcessingRepository repository, IFileStorage storage, IOcrEngine ocr)
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
        OcrExtraction? extraction = null;
        try
        {
            var original = await storage.ReadAsync(message.StorageKey, cancellationToken);
            FileIntegrity.Verify(original, claim.FileSha256, claim.FileSizeBytes);
            if (!await repository.StartOcrAsync(claim, cancellationToken)) return true;
            extraction = await ocr.ExtractAsync(original, cancellationToken);
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
        catch (OcrException exception)
        {
            errorCode = exception.Code;
            error = exception.Message;
            retryable = exception.Retryable;
        }
        await repository.FinishAsync(claim, extraction, errorCode, error, retryable, cancellationToken);
        return true;
    }
}
