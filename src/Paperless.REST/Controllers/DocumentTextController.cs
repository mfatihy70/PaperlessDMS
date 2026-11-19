using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Paperless.Application.Abstractions;
using Paperless.Application.DTOs;

namespace Paperless.REST.Controllers;

/// <summary>Read persisted OCR text for the current document original.</summary>
[ApiController]
[Route("api/Documents/{id}/text")]
public class DocumentTextController(IDocumentProcessingService service) : ControllerBase
{
    /// <summary>Get the current original's recognized text and OCR metadata.</summary>
    /// <remarks>
    /// Returns a stored result without contacting MinIO, RabbitMQ or the OCR tools.
    /// Only the result for the current original and job is returned. Replacing the file invalidates the former result.
    /// Successful recognition of a blank document returns 200 with empty text and hasText false.
    /// Check GET /api/Documents/{id}/processing while recognition is pending or after a failure.
    /// </remarks>
    /// <param name="id">A positive document ID.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <response code="404">No document exists with this ID.</response>
    /// <response code="409">No OCR result exists for the current original, including pending, failed, unsupported or missing originals.</response>
    [HttpGet]
    [ProducesResponseType(typeof(DocumentTextDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DocumentTextDto>> Get([Range(1, int.MaxValue)] int id, CancellationToken cancellationToken)
    {
        var result = await service.GetTextAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }
}
