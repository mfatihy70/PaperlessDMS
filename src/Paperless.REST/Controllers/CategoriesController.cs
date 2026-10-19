using Microsoft.AspNetCore.Mvc;
using Paperless.REST.DTOs;
using Paperless.REST.Models;
using Paperless.REST.Repositories;

namespace Paperless.REST.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CategoriesController : ControllerBase
{
    private readonly IRepository<DocumentCategory> _repo;

    public CategoriesController(IRepository<DocumentCategory> repo) => _repo = repo;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<CategoryResponseDto>>> GetAll()
    {
        var cats = await _repo.GetAllAsync();
        return Ok(cats.Select(c => new CategoryResponseDto(c.Id, c.Name, c.Description)));
    }

    [HttpPost]
    public async Task<ActionResult<CategoryResponseDto>> Create([FromBody] CreateCategoryDto dto)
    {
        var cat = new DocumentCategory { Name = dto.Name, Description = dto.Description };
        await _repo.AddAsync(cat);
        await _repo.SaveChangesAsync();
        return CreatedAtAction(nameof(GetAll), new { id = cat.Id }, new CategoryResponseDto(cat.Id, cat.Name, cat.Description));
    }
}