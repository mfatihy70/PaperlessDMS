using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Paperless.Application.Abstractions;
using Paperless.Application.DTOs;

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
    /// <param name="id">A positive document ID.</param>
    /// <response code="400">The ID is not a positive integer.</response>
    /// <response code="404">No document exists with this ID.</response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(DocumentResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DocumentResponseDto>> GetById([Range(1, int.MaxValue)] int id)
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
    /// <param name="categoryId">An existing positive category ID, or empty to leave the document unassigned.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <response code="201">The document was stored; Location points to its metadata.</response>
    /// <response code="400">The file is missing, empty, invalid or larger than 25 MiB, or the category is invalid.</response>
    /// <response code="413">The entire multipart request exceeds the 27 MiB request limit.</response>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(28_311_552)]
    [ProducesResponseType(typeof(DocumentResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    public async Task<ActionResult<DocumentResponseDto>> Create(
        [Required] IFormFile? file,
        [FromForm, Range(1, int.MaxValue)] int? categoryId,
        CancellationToken cancellationToken)
    {
        if (file is null)
        {
            ModelState.AddModelError("file", "A file is required.");
            return ValidationProblem(statusCode: StatusCodes.Status400BadRequest, modelStateDictionary: ModelState);
        }

        await using var content = file.OpenReadStream();
        var upload = new DocumentUpload(file.FileName, file.ContentType, file.Length, content);
        var created = await _service.CreateDocumentAsync(upload, categoryId, cancellationToken);

        _logger.LogInformation("Document {Id} uploaded successfully.", created.Id);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Download a document's original file bytes.</summary>
    /// <remarks>Returns the stored media type and an attachment file name. Legacy records without bytes return 404.</remarks>
    /// <param name="id">A positive document ID.</param>
    /// <response code="400">The ID is not a positive integer.</response>
    /// <response code="404">No stored file exists with this ID.</response>
    [HttpGet("{id}/file")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK, "application/octet-stream")]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetFile([Range(1, int.MaxValue)] int id)
    {
        var file = await _service.GetDocumentFileAsync(id);
        return file is null
            ? NotFound()
            : File(file.Content, file.ContentType, file.FileName);
    }

    /// <summary>Replace a document's original file and/or update its category.</summary>
    /// <remarks>Omitting file keeps the original bytes. Omitting categoryId or sending an empty value removes the assignment.</remarks>
    /// <param name="id">A positive document ID.</param>
    /// <param name="file">An optional replacement file.</param>
    /// <param name="categoryId">An existing positive category ID, or empty to leave unassigned.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <response code="204">The document was updated.</response>
    /// <response code="400">The ID, replacement file or category is invalid.</response>
    /// <response code="413">The entire multipart request exceeds the 27 MiB request limit.</response>
    /// <response code="404">No document exists with this ID.</response>
    [HttpPut("{id}")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(28_311_552)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    public async Task<IActionResult> Update(
        [Range(1, int.MaxValue)] int id,
        IFormFile? file,
        [FromForm, Range(1, int.MaxValue)] int? categoryId,
        CancellationToken cancellationToken)
    {
        await using var content = file?.OpenReadStream();
        var upload = file is null ? null : new DocumentUpload(file.FileName, file.ContentType, file.Length, content!);
        var updated = await _service.UpdateDocumentAsync(id, upload, categoryId, cancellationToken);
        return updated ? NoContent() : NotFound();
    }

    /// <summary>Delete a document and its stored file.</summary>
    /// <param name="id">A positive document ID.</param>
    /// <response code="400">The ID is not a positive integer.</response>
    /// <response code="204">The document was deleted.</response>
    /// <response code="404">No document exists with this ID.</response>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete([Range(1, int.MaxValue)] int id)
    {
        var deleted = await _service.DeleteDocumentAsync(id);
        return deleted ? NoContent() : NotFound();
    }
}
