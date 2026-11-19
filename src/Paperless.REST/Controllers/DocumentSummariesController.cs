using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Paperless.Application.Abstractions;
using Paperless.Application.DTOs;

namespace Paperless.REST.Controllers;

/// <summary>Read persisted AI summaries and their independent background status.</summary>
[ApiController]
[Route("api/Documents/{id}/")]
public class DocumentSummariesController(IDocumentSummaryService service) : ControllerBase
{
    /// <summary>Read the summary of the current original without calling the AI provider.</summary>
    /// <remarks>Returns 409 before a current summary exists. Empty OCR yields an empty summary with provider None and no external request. File replacement invalidates the previous result.</remarks>
    /// <param name="id">A positive document ID.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(DocumentSummaryDto), 200)]
    [ProducesResponseType(typeof(ValidationProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 409)]
    public async Task<ActionResult<DocumentSummaryDto>> GetSummary([Range(1, int.MaxValue)] int id, CancellationToken cancellationToken)
    {
        var result = await service.GetSummaryAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>Read summary status without contacting RabbitMQ or the AI provider.</summary>
    /// <remarks>AwaitingOcr means there is no summary job yet; check the OCR processing endpoint. Queued work waits until the GenAI worker is enabled. Failures affect neither OCR nor search.</remarks>
    /// <param name="id">A positive document ID.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpGet("summarization")]
    [ProducesResponseType(typeof(SummaryStatusDto), 200)]
    [ProducesResponseType(typeof(ValidationProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<ActionResult<SummaryStatusDto>> GetStatus([Range(1, int.MaxValue)] int id, CancellationToken cancellationToken)
    {
        var result = await service.GetAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>Durably retry failed or missing summary generation using the existing OCR result.</summary>
    /// <remarks>Requires completed OCR. Returns 409 for active or completed summary jobs. Does not rerun OCR or wait for the provider. Correct worker configuration before retrying a permanent error.</remarks>
    /// <param name="id">A positive document ID.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpPost("summarization/retry")]
    [ProducesResponseType(typeof(SummaryStatusDto), 202)]
    [ProducesResponseType(typeof(ValidationProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 409)]
    public async Task<ActionResult<SummaryStatusDto>> Retry([Range(1, int.MaxValue)] int id, CancellationToken cancellationToken)
    {
        var result = await service.RetryAsync(id, cancellationToken);
        return result is null ? NotFound() : AcceptedAtAction(nameof(GetStatus), new { id }, result);
    }
}
