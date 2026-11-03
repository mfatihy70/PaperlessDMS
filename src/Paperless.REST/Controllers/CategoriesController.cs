using Microsoft.AspNetCore.Mvc;
using Paperless.REST.DTOs;
using Paperless.REST.Models;
using Paperless.REST.Repositories;

namespace Paperless.REST.Controllers;

/// <summary>List and create document categories.</summary>
[ApiController]
[Route("api/[controller]")]
public class CategoriesController : ControllerBase
{
    private readonly IRepository<DocumentCategory> _repo;

    public CategoriesController(IRepository<DocumentCategory> repo) => _repo = repo;

    /// <summary>List all categories.</summary>
    /// <remarks>Use the returned IDs when assigning a category to a document.</remarks>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<CategoryResponseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<CategoryResponseDto>>> GetAll()
    {
        var cats = await _repo.GetAllAsync();
        return Ok(cats.Select(c => new CategoryResponseDto(c.Id, c.Name, c.Description)));
    }

    /// <summary>Create a category.</summary>
    /// <remarks>
    /// Example request:
    /// <code>{ "name": "Invoices", "description": "Incoming invoices" }</code>
    /// </remarks>
    /// <param name="dto">The category name and optional description.</param>
    /// <response code="201">The created category. The Location header points to the category list.</response>
    /// <response code="400">The request cannot be bound to the DTO or required values are missing.</response>
    [HttpPost]
    [ProducesResponseType(typeof(CategoryResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CategoryResponseDto>> Create([FromBody] CreateCategoryDto dto)
    {
        var cat = new DocumentCategory { Name = dto.Name, Description = dto.Description };
        await _repo.AddAsync(cat);
        await _repo.SaveChangesAsync();
        return CreatedAtAction(nameof(GetAll), new { id = cat.Id }, new CategoryResponseDto(cat.Id, cat.Name, cat.Description));
    }
}
