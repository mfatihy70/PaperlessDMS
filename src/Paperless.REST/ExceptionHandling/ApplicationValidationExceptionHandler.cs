using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Paperless.Application.Exceptions;

namespace Paperless.REST.ExceptionHandling;

/// <summary>Translates application validation failures into the public HTTP error contract.</summary>
public sealed class ApplicationValidationExceptionHandler(
    ProblemDetailsFactory problemDetailsFactory,
    ILogger<ApplicationValidationExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not ApplicationValidationException validation)
            return false;

        logger.LogWarning("Rejected request {Path}: {Reason}", httpContext.Request.Path, validation.Message);
        var modelState = new ModelStateDictionary();
        modelState.AddModelError(validation.Field, validation.Message);
        var problem = problemDetailsFactory.CreateValidationProblemDetails(
            httpContext, modelState, statusCode: StatusCodes.Status400BadRequest);
        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        await httpContext.Response.WriteAsJsonAsync(
            problem, (JsonSerializerOptions?)null, "application/problem+json", cancellationToken);
        return true;
    }
}
