using Microsoft.AspNetCore.Mvc;
using Paperless.REST.DTOs;
using Paperless.REST.Services;

namespace Paperless.REST.Controllers;

/// <summary>Manage document metadata and category assignments.</summary>
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

    /// <summary>List all documents.</summary>
    /// <remarks>Returns document metadata including the assigned category name. No file contents are returned.</remarks>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<DocumentResponseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<DocumentResponseDto>>> GetAll()
    {
        return Ok(await _service.GetAllDocumentsAsync());
    }

    /// <summary>Get a document by its ID.</summary>
    /// <param name="id">The document ID.</param>
    /// <response code="404">No document exists with this ID.</response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(DocumentResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DocumentResponseDto>> GetById(int id)
    {
        var doc = await _service.GetDocumentByIdAsync(id);
        if (doc is null) return NotFound();
        return Ok(doc);
    }

    /// <summary>Create document metadata.</summary>
    /// <remarks>
    /// Registers metadata only; this endpoint does not upload a file. Use a category ID returned by
    /// GET /api/Categories, or null to leave the document unassigned. A null contentType defaults to application/pdf.
    /// Example request:
    /// <code>
    /// {
    ///   "title": "Invoice October 2026",
    ///   "fileName": "invoice.pdf",
    ///   "contentType": "application/pdf",
    ///   "fileSizeBytes": 4096,
    ///   "categoryId": null
    /// }
    /// </code>
    /// </remarks>
    /// <param name="dto">The document metadata to create.</param>
    /// <response code="201">The created metadata. The Location header points to the document.</response>
    /// <response code="400">The request cannot be bound to the DTO or required values are missing.</response>
    [HttpPost]
    [ProducesResponseType(typeof(DocumentResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DocumentResponseDto>> Create([FromBody] CreateDocumentDto dto)
    {
        var created = await _service.CreateDocumentAsync(dto);
        _logger.LogInformation("Document {Id} created successfully.", created.Id);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Update a document's title and category.</summary>
    /// <remarks>Set categoryId to null to remove the category assignment. File metadata is not changed.</remarks>
    /// <param name="id">The document ID.</param>
    /// <param name="dto">The new title and category assignment.</param>
    /// <response code="204">The document was updated successfully.</response>
    /// <response code="404">No document exists with this ID.</response>
    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateDocumentDto dto)
    {
        var updated = await _service.UpdateDocumentAsync(id, dto);
        if (!updated) return NotFound();
        return NoContent();
    }

    /// <summary>Delete document metadata.</summary>
    /// <param name="id">The document ID.</param>
    /// <response code="204">The document metadata was deleted successfully.</response>
    /// <response code="404">No document exists with this ID.</response>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await _service.DeleteDocumentAsync(id);
        if (!deleted) return NotFound();
        return NoContent();
    }
}
