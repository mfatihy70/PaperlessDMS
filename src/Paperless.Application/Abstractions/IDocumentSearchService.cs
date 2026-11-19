using Paperless.Application.DTOs;

namespace Paperless.Application.Abstractions;

public interface IDocumentSearchService
{
    Task<DocumentSearchDto> SearchAsync(string query, bool fuzzy, int page, int pageSize, CancellationToken cancellationToken);
    Task<DocumentIndexingDto?> GetIndexingAsync(int id, CancellationToken cancellationToken);
    Task<DocumentIndexingDto?> RetryIndexingAsync(int id, CancellationToken cancellationToken);
    Task<SearchReindexDto> ReindexAsync(CancellationToken cancellationToken);
}

public interface ISearchIndex
{
    Task<string> GetIdentityAsync(CancellationToken cancellationToken);
    Task WriteAsync(IndexDocument snapshot, CancellationToken cancellationToken);
    Task<SearchCandidates> SearchAsync(SearchQuery query, CancellationToken cancellationToken);
}

public interface ISearchRepository
{
    Task<DocumentSearchDto> ResolveAsync(SearchQuery query, SearchCandidates candidates, CancellationToken cancellationToken);
    Task<DocumentIndexingDto?> GetAsync(int id, CancellationToken cancellationToken);
    Task<DocumentIndexingDto?> RetryAsync(int id, CancellationToken cancellationToken);
    Task<SearchReindexDto> ReindexAsync(string? indexIdentity, CancellationToken cancellationToken);
    Task RecoverUnavailableAsync(CancellationToken cancellationToken);
    Task<IndexClaimResult> ClaimAsync(IndexDocumentMessage message, CancellationToken cancellationToken);
    Task FinishAsync(IndexClaim claim, string? errorCode, string? error, bool retryable, CancellationToken cancellationToken);
}

public sealed record SearchQuery(string Query, bool Fuzzy, int Page, int PageSize);
public sealed record SearchCandidate(int DocumentId, long Revision, Guid JobId, double Score, string? Snippet);
public sealed record SearchCandidates(long Total, IReadOnlyList<SearchCandidate> Items);
public sealed record IndexDocumentMessage(int SchemaVersion, Guid MessageId, int DocumentId, long Revision);
public sealed record IndexFailure(IndexDocumentMessage Message, string ErrorCode, string Error, int Attempts, DateTime FailedAt);
public sealed record IndexDocument(int DocumentId, long Revision, bool IsDeleted, Guid? JobId = null,
    string? Title = null, string? FileName = null, string? Text = null);
public sealed record IndexClaim(IndexDocumentMessage Message, Guid LeaseId, IndexDocument Document);
public sealed record IndexClaimResult(bool Busy, IndexClaim? Claim);
