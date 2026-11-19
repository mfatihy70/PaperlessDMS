using Paperless.Application.DTOs;

namespace Paperless.Application.Abstractions;

public interface IDocumentSummaryService
{
    Task<SummaryStatusDto?> GetAsync(int documentId, CancellationToken cancellationToken);
    Task<DocumentSummaryDto?> GetSummaryAsync(int documentId, CancellationToken cancellationToken);
    Task<SummaryStatusDto?> RetryAsync(int documentId, CancellationToken cancellationToken);
}

public interface ISummaryRepository : IDocumentSummaryService
{
    Task<SummaryClaimResult> ClaimAsync(SummarizeDocumentMessage message, CancellationToken cancellationToken);
    Task FinishAsync(SummaryClaim claim, SummaryGeneration? result, string? code, string? error, bool retryable, CancellationToken cancellationToken);
}

public interface ISummaryGenerator
{
    Task<SummaryGeneration> GenerateAsync(string text, CancellationToken cancellationToken);
}

public sealed record SummarizeDocumentMessage(int SchemaVersion, Guid MessageId, Guid JobId, int DocumentId);
public sealed record SummaryFailure(SummarizeDocumentMessage Message, string ErrorCode, string Error, int Attempts, DateTime FailedAt);
public sealed record SummaryClaim(SummarizeDocumentMessage Message, Guid LeaseId, string Text);
public sealed record SummaryClaimResult(bool Busy, SummaryClaim? Claim);
public sealed record SummaryGeneration(string Text, string Language, string Provider, string? Model);
