using Paperless.Application.DTOs;

namespace Paperless.Application.Abstractions;

public interface IDocumentProcessingService
{
    Task<ProcessingStatusDto?> GetAsync(int documentId, CancellationToken cancellationToken);
    Task<ProcessingStatusDto?> RetryAsync(int documentId, CancellationToken cancellationToken);
}

public interface IProcessingRepository
{
    Task<ProcessingStatusDto?> GetAsync(int documentId, CancellationToken cancellationToken);
    Task<ProcessingStatusDto?> RetryAsync(int documentId, CancellationToken cancellationToken);
    Task<PreparationClaimResult> ClaimAsync(PrepareDocumentMessage message, CancellationToken cancellationToken);
    Task FinishAsync(PreparationClaim claim, string? errorCode, string? error, bool retryable, CancellationToken cancellationToken);
}

// Messages carry immutable object references, never original file bytes.
public sealed record PrepareDocumentMessage(int SchemaVersion, Guid MessageId, Guid JobId, int DocumentId, string StorageKey);
public sealed record PreparationFailure(PrepareDocumentMessage Message, string ErrorCode, string Error, int Attempts, DateTime FailedAt);
public sealed record PreparationClaim(PrepareDocumentMessage Message, Guid LeaseId, string FileSha256, long FileSizeBytes);
public sealed record PreparationClaimResult(bool Busy, PreparationClaim? Claim);
