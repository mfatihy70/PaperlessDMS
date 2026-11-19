using Paperless.Application.Abstractions;
using Paperless.Application.Exceptions;

namespace Paperless.Application.Services;

public sealed class DocumentIndexing(ISearchRepository repository, ISearchIndex index)
{
    public async Task<bool> TryProcessAsync(IndexDocumentMessage message, CancellationToken cancellationToken)
    {
        var result = await repository.ClaimAsync(message, cancellationToken);
        if (result.Busy) return false;
        if (result.Claim is not { } claim) return true;
        string? code = null;
        string? error = null;
        var retryable = false;
        try { await index.WriteAsync(claim.Document, cancellationToken); }
        catch (SearchException exception)
        {
            code = exception.Code;
            error = exception.Message;
            retryable = exception.Retryable;
        }
        await repository.FinishAsync(claim, code, error, retryable, cancellationToken);
        return true;
    }
}
