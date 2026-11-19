using Paperless.Application.Abstractions;
using Paperless.Application.Exceptions;

namespace Paperless.Application.Services;

public sealed class DocumentSummarization(ISummaryRepository repository, ISummaryGenerator generator)
{
    public async Task<bool> TryProcessAsync(SummarizeDocumentMessage message, CancellationToken cancellationToken)
    {
        var outcome = await repository.ClaimAsync(message, cancellationToken);
        if (outcome.Busy) return false;
        if (outcome.Claim is not { } claim) return true;
        SummaryGeneration? result = null;
        string? code = null, error = null;
        var retryable = false;
        try { result = await generator.GenerateAsync(claim.Text, cancellationToken); }
        catch (SummaryException exception)
        {
            code = exception.Code;
            error = exception.Message;
            retryable = exception.Retryable;
        }
        await repository.FinishAsync(claim, result, code, error, retryable, cancellationToken);
        return true;
    }
}
