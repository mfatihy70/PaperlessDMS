using Microsoft.AspNetCore.Diagnostics;
using Paperless.Application.Exceptions;

namespace Paperless.REST.ExceptionHandling;

public sealed class SearchExceptionHandler(IProblemDetailsService problemDetails, ILogger<SearchExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not SearchException failure) return false;
        var status = failure.Retryable ? StatusCodes.Status503ServiceUnavailable : StatusCodes.Status502BadGateway;
        logger.LogWarning("Search request failed with {Code} ({Status}).", failure.Code, status);
        context.Response.StatusCode = status;
        await problemDetails.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new Microsoft.AspNetCore.Mvc.ProblemDetails
            {
                Status = status,
                Title = "Search service unavailable",
                Detail = failure.Message
            }
        });
        return true;
    }
}
