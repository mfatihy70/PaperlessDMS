using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Paperless.Application.Abstractions;
using Paperless.Application.DTOs;

namespace Paperless.REST.Controllers;

/// <summary>Observe and retry search-index synchronization independently of OCR.</summary>
[ApiController]
[Route("api/Documents/{id}/indexing")]
public class DocumentIndexingController(IDocumentSearchService service) : ControllerBase
{
    /// <summary>Get the current document's search-index status.</summary>
    /// <remarks>Reads PostgreSQL only. AwaitingOcr means any former searchable text was removed and no current OCR result is ready.</remarks>
    /// <param name="id">A positive document ID.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpGet]
    [ProducesResponseType(typeof(DocumentIndexingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DocumentIndexingDto>> Get([Range(1, int.MaxValue)] int id, CancellationToken cancellationToken)
    {
        var result = await service.GetIndexingAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>Schedule a fresh index revision after failure or when no indexing job exists.</summary>
    /// <remarks>Does not rerun OCR. A pending, active or already synchronized job returns 409. Location points to indexing status.</remarks>
    /// <param name="id">A positive document ID.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpPost("retry")]
    [ProducesResponseType(typeof(DocumentIndexingDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DocumentIndexingDto>> Retry([Range(1, int.MaxValue)] int id, CancellationToken cancellationToken)
    {
        var result = await service.RetryIndexingAsync(id, cancellationToken);
        return result is null ? NotFound() : AcceptedAtAction(nameof(Get), new { id }, result);
    }
}
