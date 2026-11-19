using Microsoft.AspNetCore.Diagnostics;
using Paperless.Application.Exceptions;

namespace Paperless.REST.ExceptionHandling;

public sealed class FileStorageExceptionHandler(
    IProblemDetailsService problemDetails,
    ILogger<FileStorageExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var status = exception switch
        {
            FileStorageUnavailableException => StatusCodes.Status503ServiceUnavailable,
            FileStorageIntegrityException => StatusCodes.Status502BadGateway,
            ApplicationConflictException => StatusCodes.Status409Conflict,
            _ => 0
        };
        if (status == 0)
            return false;
        logger.LogWarning("Request {Path} failed with {FailureType} ({Status}).", httpContext.Request.Path, exception.GetType().Name, status);
        httpContext.Response.StatusCode = status;
        await problemDetails.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new Microsoft.AspNetCore.Mvc.ProblemDetails
            {
                Status = status,
                Title = status switch
                {
                    503 => "File storage unavailable",
                    502 => "Stored original unavailable or damaged",
                    _ => "Document changed concurrently"
                },
                Detail = exception.Message
            }
        });
        return true;
    }
}
