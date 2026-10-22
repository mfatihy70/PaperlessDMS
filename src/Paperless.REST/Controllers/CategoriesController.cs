using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Paperless.REST.DTOs;
using Paperless.REST.Models;
using Paperless.REST.Repositories;

namespace Paperless.REST.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CategoriesController : ControllerBase
{
    private readonly IRepository<DocumentCategory> _repo;
    private readonly IDocumentRepository _docRepo;
    private readonly ILogger<CategoriesController> _logger;

    public CategoriesController(
        IRepository<DocumentCategory> repo,
        IDocumentRepository docRepo,
        ILogger<CategoriesController> logger)
    {
        _repo = repo;
        _docRepo = docRepo;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<CategoryResponseDto>>> GetAll()
    {
        var cats = await _repo.GetAllAsync();
        return Ok(cats.Select(c => new CategoryResponseDto(c.Id, c.Name, c.Description)));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<CategoryResponseDto>> GetById(int id)
    {
        var cat = await _repo.GetByIdAsync(id);
        if (cat is null) return NotFound();
        return Ok(new CategoryResponseDto(cat.Id, cat.Name, cat.Description));
    }

    [HttpPost]
    public async Task<ActionResult<CategoryResponseDto>> Create([FromBody] CreateCategoryDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest("Category name is required.");

        var cat = new DocumentCategory { Name = dto.Name, Description = dto.Description };
        await _repo.AddAsync(cat);
        await _repo.SaveChangesAsync();

        _logger.LogInformation("Category {Id} created.", cat.Id);
        return CreatedAtAction(nameof(GetById), new { id = cat.Id }, new CategoryResponseDto(cat.Id, cat.Name, cat.Description));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateCategoryDto dto)
    {
        var cat = await _repo.GetByIdAsync(id);
        if (cat is null) return NotFound();

        cat.Name = dto.Name;
        cat.Description = dto.Description;
        _repo.Update(cat);
        await _repo.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var cat = await _repo.GetByIdAsync(id);
        if (cat is null) return NotFound();

        // Nullify foreign key references in documents
        var associatedDocs = await _docRepo.FindAsync(d => d.CategoryId == id);
        foreach (var doc in associatedDocs)
        {
            doc.CategoryId = null;
            _docRepo.Update(doc);
        }

        _repo.Delete(cat);
        await _repo.SaveChangesAsync();

        _logger.LogInformation("Category {Id} deleted.", id);
        return NoContent();
    }
}