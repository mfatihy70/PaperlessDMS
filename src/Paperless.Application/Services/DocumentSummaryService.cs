using Paperless.Application.Abstractions;
using Paperless.Application.DTOs;
using Paperless.Application.Exceptions;

namespace Paperless.Application.Services;

public sealed class DocumentSummaryService(ISummaryRepository repository) : IDocumentSummaryService
{
    public Task<SummaryStatusDto?> GetAsync(int documentId, CancellationToken cancellationToken)
    {
        Validate(documentId);
        return repository.GetAsync(documentId, cancellationToken);
    }
    public Task<DocumentSummaryDto?> GetSummaryAsync(int documentId, CancellationToken cancellationToken)
    {
        Validate(documentId);
        return repository.GetSummaryAsync(documentId, cancellationToken);
    }
    public Task<SummaryStatusDto?> RetryAsync(int documentId, CancellationToken cancellationToken)
    {
        Validate(documentId);
        return repository.RetryAsync(documentId, cancellationToken);
    }
    private static void Validate(int id)
    {
        if (id <= 0) throw new ApplicationValidationException("id", "The document ID must be positive.");
    }
}
