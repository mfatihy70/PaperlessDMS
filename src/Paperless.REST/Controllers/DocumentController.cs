using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Paperless.REST.DTOs;
using Paperless.REST.Services;

namespace Paperless.REST.Controllers;

/// <summary>Manage uploaded documents and their category assignments.</summary>
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

    /// <summary>List stored documents and their derived file metadata.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<DocumentResponseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<DocumentResponseDto>>> GetAll()
    {
        return Ok(await _service.GetAllDocumentsAsync());
    }

    /// <summary>Get a document's metadata by ID.</summary>
    /// <param name="id">The document ID.</param>
    /// <response code="404">No document exists with this ID.</response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(DocumentResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DocumentResponseDto>> GetById(int id)
    {
        var document = await _service.GetDocumentByIdAsync(id);
        return document is null ? NotFound() : Ok(document);
    }

    /// <summary>Upload and store an original document file.</summary>
    /// <remarks>
    /// The file name, content type, size, and title are derived from the uploaded file.
    /// The original bytes are preserved in the database. All file types are accepted up to 25 MiB.
    /// </remarks>
    /// <param name="file">The original file to store.</param>
    /// <param name="categoryId">An optional category ID.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <response code="201">The document was stored; Location points to its metadata.</response>
    /// <response code="400">The file is empty, invalid, or exceeds the size limit.</response>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(28_311_552)]
    [ProducesResponseType(typeof(DocumentResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    public async Task<ActionResult<DocumentResponseDto>> Create(
        [Required] IFormFile? file,
        [FromForm] int? categoryId,
        CancellationToken cancellationToken)
    {
        if (file is null)
            return BadRequest(new ProblemDetails { Title = "A file is required", Status = StatusCodes.Status400BadRequest });

        DocumentResponseDto created;
        try
        {
            created = await _service.CreateDocumentAsync(file, categoryId, cancellationToken);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Rejected document upload: {Reason}", ex.Message);
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid document upload",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }

        _logger.LogInformation("Document {Id} uploaded successfully.", created.Id);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Download a document's original file bytes.</summary>
    /// <param name="id">The document ID.</param>
    /// <response code="404">No stored file exists with this ID.</response>
    [HttpGet("{id}/file")]
    [Produces("application/octet-stream")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetFile(int id)
    {
        var file = await _service.GetDocumentFileAsync(id);
        return file is null
            ? NotFound()
            : File(file.Content, file.ContentType, file.FileName);
    }

    /// <summary>Replace a document's original file and/or update its category.</summary>
    /// <param name="id">The document ID.</param>
    /// <param name="file">An optional replacement file.</param>
    /// <param name="categoryId">The category ID, or empty to leave unassigned.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <response code="204">The document was updated.</response>
    /// <response code="400">The replacement file is invalid or exceeds the size limit.</response>
    /// <response code="404">No document exists with this ID.</response>
    [HttpPut("{id}")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(28_311_552)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    public async Task<IActionResult> Update(
        int id,
        IFormFile? file,
        [FromForm] int? categoryId,
        CancellationToken cancellationToken)
    {
        try
        {
            var updated = await _service.UpdateDocumentAsync(id, file, categoryId, cancellationToken);
            return updated ? NoContent() : NotFound();
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Rejected document update for {Id}: {Reason}", id, ex.Message);
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid document upload",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
    }

    /// <summary>Delete a document and its stored file.</summary>
    /// <param name="id">The document ID.</param>
    /// <response code="204">The document was deleted.</response>
    /// <response code="404">No document exists with this ID.</response>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await _service.DeleteDocumentAsync(id);
        return deleted ? NoContent() : NotFound();
    }
}
