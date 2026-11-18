using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Paperless.Application.Abstractions;
using Paperless.Application.DTOs;
using Paperless.REST.Controllers;
using Xunit;

namespace Paperless.REST.Tests;

public class DocumentsControllerTests
{
    private readonly Mock<IDocumentService> _service = new();
    private readonly DocumentsController _controller;
    private static readonly DateTime Date = new(2026, 10, 19, 0, 0, 0, DateTimeKind.Utc);

    public DocumentsControllerTests()
    {
        _controller = new DocumentsController(_service.Object, Mock.Of<ILogger<DocumentsController>>());
    }

    private static DocumentResponseDto Sample(int id = 1) =>
        new(id, "Invoice", "invoice.pdf", "application/pdf", 100, Date, null, null, true);

    [Fact]
    public async Task GetAll_ReturnsOkWithDocuments()
    {
        _service.Setup(x => x.GetAllDocumentsAsync()).ReturnsAsync(new[] { Sample() });
        var action = await _controller.GetAll();
        var ok = Assert.IsType<OkObjectResult>(action.Result);
        var items = Assert.IsAssignableFrom<IEnumerable<DocumentResponseDto>>(ok.Value);
        Assert.Single(items);
    }

    [Fact]
    public async Task GetById_Existing_ReturnsOk()
    {
        _service.Setup(x => x.GetDocumentByIdAsync(1)).ReturnsAsync(Sample());
        var action = await _controller.GetById(1);
        var ok = Assert.IsType<OkObjectResult>(action.Result);
        Assert.Equal(1, Assert.IsType<DocumentResponseDto>(ok.Value).Id);
    }

    [Fact]
    public async Task GetById_Missing_ReturnsNotFound()
    {
        _service.Setup(x => x.GetDocumentByIdAsync(404)).ReturnsAsync((DocumentResponseDto?)null);
        var action = await _controller.GetById(404);
        Assert.IsType<NotFoundResult>(action.Result);
    }

    [Fact]
    public async Task Create_ReturnsCreatedAtAction()
    {
        var file = CreateFile();
        _service.Setup(x => x.CreateDocumentAsync(
            It.Is<DocumentUpload>(upload => upload.FileName == file.FileName && upload.Length == file.Length),
            5, CancellationToken.None)).ReturnsAsync(Sample(25));
        var action = await _controller.Create(file, 5, CancellationToken.None);
        var created = Assert.IsType<CreatedAtActionResult>(action.Result);
        Assert.Equal(nameof(DocumentsController.GetById), created.ActionName);
        Assert.Equal(25, created.RouteValues!["id"]);
        Assert.Equal(25, Assert.IsType<DocumentResponseDto>(created.Value).Id);
        _service.Verify(x => x.CreateDocumentAsync(
            It.Is<DocumentUpload>(upload => upload.FileName == file.FileName && upload.Length == file.Length),
            5, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task Create_WithoutFile_ReturnsBadRequest()
    {
        var result = await _controller.Create(null, null, CancellationToken.None);

        var invalid = Assert.IsAssignableFrom<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, invalid.StatusCode);
        Assert.Contains("file", Assert.IsType<ValidationProblemDetails>(invalid.Value).Errors.Keys);
        _service.Verify(x => x.CreateDocumentAsync(
            It.IsAny<DocumentUpload>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetFile_ReturnsOriginalFile()
    {
        var bytes = new byte[] { 1, 2, 3 };
        _service.Setup(x => x.GetDocumentFileAsync(1))
            .ReturnsAsync(new DocumentFileDto("invoice.pdf", "application/pdf", bytes));

        var result = Assert.IsType<FileContentResult>(await _controller.GetFile(1));

        Assert.Equal(bytes, result.FileContents);
        Assert.Equal("invoice.pdf", result.FileDownloadName);
    }

    [Fact]
    public async Task Update_Existing_ReturnsNoContent()
    {
        _service.Setup(x => x.UpdateDocumentAsync(1, null, 5, CancellationToken.None)).ReturnsAsync(true);
        Assert.IsType<NoContentResult>(await _controller.Update(1, null, 5, CancellationToken.None));
    }

    [Fact]
    public async Task Update_Missing_ReturnsNotFound()
    {
        _service.Setup(x => x.UpdateDocumentAsync(404, null, null, CancellationToken.None)).ReturnsAsync(false);
        Assert.IsType<NotFoundResult>(await _controller.Update(404, null, null, CancellationToken.None));
    }

    [Fact]
    public async Task Delete_Existing_ReturnsNoContent()
    {
        _service.Setup(x => x.DeleteDocumentAsync(1)).ReturnsAsync(true);
        Assert.IsType<NoContentResult>(await _controller.Delete(1));
    }

    [Fact]
    public async Task Delete_Missing_ReturnsNotFound()
    {
        _service.Setup(x => x.DeleteDocumentAsync(404)).ReturnsAsync(false);
        Assert.IsType<NotFoundResult>(await _controller.Delete(404));
    }

    private static FormFile CreateFile()
    {
        var stream = new MemoryStream([1, 2, 3]);
        return new FormFile(stream, 0, stream.Length, "file", "invoice.pdf")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/pdf"
        };
    }
}
