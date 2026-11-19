using Paperless.Application.Abstractions;
using Paperless.Application.DTOs;
using Paperless.Application.Exceptions;

namespace Paperless.Application.Services;

public sealed class DocumentSearchService(ISearchIndex index, ISearchRepository repository) : IDocumentSearchService
{
    public async Task<DocumentSearchDto> SearchAsync(string query, bool fuzzy, int page, int pageSize, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length > 200 || query.Any(char.IsControl))
            throw new ApplicationValidationException("q", "Enter a search phrase of 1 to 200 characters without control characters.");
        if (pageSize is < 1 or > 100)
            throw new ApplicationValidationException("pageSize", "Page size must be between 1 and 100.");
        if (page < 1 || (long)page * pageSize > 10_000)
            throw new ApplicationValidationException("page", "The page must be positive and stay within the first 10,000 indexed matches.");
        var request = new SearchQuery(query.Trim(), fuzzy, page, pageSize);
        var candidates = await index.SearchAsync(request, cancellationToken);
        return await repository.ResolveAsync(request, candidates, cancellationToken);
    }

    public Task<DocumentIndexingDto?> GetIndexingAsync(int id, CancellationToken cancellationToken)
    {
        ValidateId(id);
        return repository.GetAsync(id, cancellationToken);
    }

    public Task<DocumentIndexingDto?> RetryIndexingAsync(int id, CancellationToken cancellationToken)
    {
        ValidateId(id);
        return repository.RetryAsync(id, cancellationToken);
    }

    public Task<SearchReindexDto> ReindexAsync(CancellationToken cancellationToken) => repository.ReindexAsync(null, cancellationToken);

    private static void ValidateId(int id)
    {
        if (id <= 0) throw new ApplicationValidationException("id", "The document ID must be positive.");
    }
}
