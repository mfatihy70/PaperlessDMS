using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Paperless.Application.Abstractions;
using Paperless.Application.DTOs;
using Paperless.REST.DTOs;

namespace Paperless.REST.Controllers;

/// <summary>Manage reusable manual document labels. Tag deletion preserves documents and originals.</summary>
[ApiController]
[Route("api/Tags")]
public class TagsController(ITagService service) : ControllerBase
{
    /// <summary>List all tags, ordered by display name and ID.</summary>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<TagResponseDto>), 200)]
    public async Task<ActionResult<IReadOnlyList<TagResponseDto>>> List(CancellationToken cancellationToken) =>
        Ok(await service.ListAsync(cancellationToken));

    /// <summary>Read a single tag.</summary>
    /// <param name="id">A positive tag ID.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(TagResponseDto), 200)]
    [ProducesResponseType(typeof(ValidationProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<ActionResult<TagResponseDto>> Get([Range(1, int.MaxValue)] int id, CancellationToken cancellationToken)
    {
        var result = await service.GetAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>Create a tag with a unique name.</summary>
    /// <param name="request">The display name and optional description.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpPost]
    [ProducesResponseType(typeof(TagResponseDto), 201)]
    [ProducesResponseType(typeof(ValidationProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 409)]
    public async Task<ActionResult<TagResponseDto>> Create(TagRequest request, CancellationToken cancellationToken)
    {
        var result = await service.CreateAsync(request.Name, request.Description, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    /// <summary>Replace a tag's name and description, preserving all assignments.</summary>
    /// <param name="id">A positive tag ID.</param>
    /// <param name="request">The new name and description; null clears the description.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpPut("{id}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(ValidationProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 409)]
    public async Task<IActionResult> Update([Range(1, int.MaxValue)] int id, TagRequest request, CancellationToken cancellationToken) =>
        await service.UpdateAsync(id, request.Name, request.Description, cancellationToken) ? NoContent() : NotFound();

    /// <summary>Delete a tag and its assignments, preserving every document and original.</summary>
    /// <param name="id">A positive tag ID.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpDelete("{id}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(ValidationProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<IActionResult> Delete([Range(1, int.MaxValue)] int id, CancellationToken cancellationToken) =>
        await service.DeleteAsync(id, cancellationToken) ? NoContent() : NotFound();
}
