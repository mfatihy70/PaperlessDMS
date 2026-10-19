using Microsoft.AspNetCore.Mvc;
using Paperless.REST.DTOs;
using Paperless.REST.Services;

namespace Paperless.REST.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DocumentsController : ControllerBase
{
    private readonly IDocumentService _service;
    private readonly ILogger<DocumentsController> _logger;

    public DocumentsController(IDocumentService service, ILogger<DocumentsController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<DocumentResponseDto>>> GetAll()
    {
        return Ok(await _service.GetAllDocumentsAsync());
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<DocumentResponseDto>> GetById(int id)
    {
        var doc = await _service.GetDocumentByIdAsync(id);
        if (doc is null) return NotFound();
        return Ok(doc);
    }

    [HttpPost]
    public async Task<ActionResult<DocumentResponseDto>> Create([FromBody] CreateDocumentDto dto)
    {
        var created = await _service.CreateDocumentAsync(dto);
        _logger.LogInformation("Document {Id} created successfully.", created.Id);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateDocumentDto dto)
    {
        var updated = await _service.UpdateDocumentAsync(id, dto);
        if (!updated) return NotFound();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await _service.DeleteDocumentAsync(id);
        if (!deleted) return NotFound();
        return NoContent();
    }
}