using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Paperless.Application.Abstractions;
using Paperless.Application.DTOs;

namespace Paperless.REST.Controllers;

/// <summary>Observe and retry preparation of stored originals, independently of Swagger UI.</summary>
[ApiController]
[Route("api/Documents/{id}/processing")]
public class DocumentProcessingController(IDocumentProcessingService service) : ControllerBase
{
    /// <summary>Get the current original's background preparation status.</summary>
    /// <remarks>
    /// Reads PostgreSQL without contacting RabbitMQ or MinIO. Queued means delivery or worker execution is pending.
    /// ReadyForOcr means the original's size and SHA-256 were verified; OCR, indexing and AI have not run.
    /// Replacing the original creates a new job. A category-only update preserves the job.
    /// </remarks>
    /// <param name="id">A positive document ID.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <response code="404">No document exists with this ID.</response>
    [HttpGet]
    [ProducesResponseType(typeof(ProcessingStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProcessingStatusDto>> Get([Range(1, int.MaxValue)] int id, CancellationToken cancellationToken)
    {
        var result = await service.GetAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>Schedule a new preparation job after failure or for an original without a job.</summary>
    /// <remarks>
    /// Saves the new job and outgoing message atomically, including while RabbitMQ is unavailable.
    /// Transient storage failures are retried automatically before a job becomes Failed.
    /// This operation resets the attempt counter and creates a new job ID. It cannot restart an active or ReadyForOcr job.
    /// </remarks>
    /// <param name="id">A positive document ID.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <response code="202">The new job is durably queued. Location points to its status endpoint.</response>
    /// <response code="404">No document exists with this ID.</response>
    /// <response code="409">The original is missing, requires migration, or already has an active or successful job.</response>
    [HttpPost("retry")]
    [ProducesResponseType(typeof(ProcessingStatusDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProcessingStatusDto>> Retry([Range(1, int.MaxValue)] int id, CancellationToken cancellationToken)
    {
        var result = await service.RetryAsync(id, cancellationToken);
        return result is null ? NotFound() : AcceptedAtAction(nameof(Get), new { id }, result);
    }
}
