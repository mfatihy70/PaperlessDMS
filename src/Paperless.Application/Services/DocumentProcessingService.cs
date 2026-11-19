using Paperless.Application.Abstractions;
using Paperless.Application.DTOs;
using Paperless.Application.Exceptions;

namespace Paperless.Application.Services;

public sealed class DocumentProcessingService(IProcessingRepository repository) : IDocumentProcessingService
{
    public Task<ProcessingStatusDto?> GetAsync(int documentId, CancellationToken cancellationToken)
    {
        Validate(documentId);
        return repository.GetAsync(documentId, cancellationToken);
    }

    public Task<ProcessingStatusDto?> RetryAsync(int documentId, CancellationToken cancellationToken)
    {
        Validate(documentId);
        return repository.RetryAsync(documentId, cancellationToken);
    }

    private static void Validate(int id)
    {
        if (id <= 0) throw new ApplicationValidationException("id", "The document ID must be positive.");
    }

    public Task<DocumentTextDto?> GetTextAsync(int documentId, CancellationToken cancellationToken)
    {
        Validate(documentId);
        return repository.GetTextAsync(documentId, cancellationToken);
    }
}
