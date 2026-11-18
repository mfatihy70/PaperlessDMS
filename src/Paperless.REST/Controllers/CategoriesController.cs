using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Paperless.Application.Abstractions;
using Paperless.Application.DTOs;
using Paperless.REST.DTOs;

namespace Paperless.REST.Controllers;

/// <summary>Create, retrieve, update and delete document categories.</summary>
[ApiController]
[Route("api/[controller]")]
public class CategoriesController : ControllerBase
{
    private readonly ICategoryService _service;

    public CategoriesController(ICategoryService service) => _service = service;

    /// <summary>List all categories.</summary>
    /// <remarks>Use the returned IDs when assigning a category to a document.</remarks>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<CategoryResponseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<CategoryResponseDto>>> GetAll()
    {
        return Ok(await _service.GetAllAsync());
    }

    /// <summary>Get a category by its ID.</summary>
    /// <param name="id">A positive category ID.</param>
    /// <response code="400">The ID is not a positive integer.</response>
    /// <response code="404">No category exists with this ID.</response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(CategoryResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CategoryResponseDto>> GetById([Range(1, int.MaxValue)] int id)
    {
        var category = await _service.GetByIdAsync(id);
        return category is null ? NotFound() : Ok(category);
    }

    /// <summary>Create a category.</summary>
    /// <remarks>
    /// Example request:
    /// <code>{ "name": "Invoices", "description": "Incoming invoices" }</code>
    /// </remarks>
    /// <param name="dto">The category name and optional description.</param>
    /// <response code="201">The created category. The Location header points to its individual endpoint.</response>
    /// <response code="400">The request is invalid, or the name is blank or longer than 100 characters.</response>
    [HttpPost]
    [ProducesResponseType(typeof(CategoryResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CategoryResponseDto>> Create([FromBody] CreateCategoryDto dto)
    {
        var created = await _service.CreateAsync(dto.Name, dto.Description);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Update a category's name and description.</summary>
    /// <remarks>The name is trimmed. Set description to null to remove it.</remarks>
    /// <param name="id">A positive category ID.</param>
    /// <param name="dto">The replacement name and description.</param>
    /// <response code="204">The category was updated.</response>
    /// <response code="400">The ID or category data is invalid.</response>
    /// <response code="404">No category exists with this ID.</response>
    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update([Range(1, int.MaxValue)] int id, [FromBody] UpdateCategoryDto dto)
    {
        var updated = await _service.UpdateAsync(id, dto.Name, dto.Description);
        return updated ? NoContent() : NotFound();
    }

    /// <summary>Delete a category while preserving its documents.</summary>
    /// <remarks>The database sets categoryId to null on documents assigned to the deleted category.</remarks>
    /// <param name="id">A positive category ID.</param>
    /// <response code="204">The category was deleted. Its documents remain stored and unassigned.</response>
    /// <response code="400">The ID is not a positive integer.</response>
    /// <response code="404">No category exists with this ID.</response>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete([Range(1, int.MaxValue)] int id)
    {
        var deleted = await _service.DeleteAsync(id);
        return deleted ? NoContent() : NotFound();
    }
}
