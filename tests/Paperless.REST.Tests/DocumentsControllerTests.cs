using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Paperless.REST.Controllers;
using Paperless.REST.DTOs;
using Paperless.REST.Services;
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
        new(id, "Invoice", "invoice.pdf", "application/pdf", 100, Date, null, null);

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
        var input = new CreateDocumentDto("Invoice", "invoice.pdf", null, 100, null);
        _service.Setup(x => x.CreateDocumentAsync(input)).ReturnsAsync(Sample(25));
        var action = await _controller.Create(input);
        var created = Assert.IsType<CreatedAtActionResult>(action.Result);
        Assert.Equal(nameof(DocumentsController.GetById), created.ActionName);
        Assert.Equal(25, created.RouteValues!["id"]);
        Assert.Equal(25, Assert.IsType<DocumentResponseDto>(created.Value).Id);
        _service.Verify(x => x.CreateDocumentAsync(input), Times.Once);
    }

    [Fact]
    public async Task Update_Existing_ReturnsNoContent()
    {
        var input = new UpdateDocumentDto("New title", null);
        _service.Setup(x => x.UpdateDocumentAsync(1, input)).ReturnsAsync(true);
        Assert.IsType<NoContentResult>(await _controller.Update(1, input));
    }

    [Fact]
    public async Task Update_Missing_ReturnsNotFound()
    {
        var input = new UpdateDocumentDto("New title", null);
        _service.Setup(x => x.UpdateDocumentAsync(404, input)).ReturnsAsync(false);
        Assert.IsType<NotFoundResult>(await _controller.Update(404, input));
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
}
