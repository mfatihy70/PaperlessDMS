using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Paperless.Application.Abstractions;
using Paperless.Application.DTOs;
using Paperless.REST.DTOs;

namespace Paperless.REST.Controllers;

/// <summary>Read and replace a document's manual tags independently of OCR and AI.</summary>
[ApiController]
[Route("api/Documents/{id}/tags")]
public class DocumentTagsController(ITagService service) : ControllerBase
{
    /// <summary>List a document's assigned tags; an existing document without tags returns an empty array.</summary>
    /// <param name="id">A positive document ID.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<TagResponseDto>), 200)]
    [ProducesResponseType(typeof(ValidationProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<ActionResult<IReadOnlyList<TagResponseDto>>> Get([Range(1, int.MaxValue)] int id, CancellationToken cancellationToken)
    {
        var result = await service.GetDocumentTagsAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>Replace the complete tag assignment atomically; an empty array removes all tags.</summary>
    /// <remarks>Idempotent. All supplied tags must exist. At most 50 distinct IDs are accepted. Assigned tags remain when the original file is replaced.</remarks>
    /// <param name="id">A positive document ID.</param>
    /// <param name="request">The complete desired set of existing tag IDs.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpPut]
    [ProducesResponseType(typeof(IReadOnlyList<TagResponseDto>), 200)]
    [ProducesResponseType(typeof(ValidationProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<ActionResult<IReadOnlyList<TagResponseDto>>> Set([Range(1, int.MaxValue)] int id, DocumentTagsRequest request, CancellationToken cancellationToken)
    {
        var result = await service.SetDocumentTagsAsync(id, request.TagIds, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }
}
