using Moq;
using Paperless.REST.DTOs;
using Paperless.REST.Models;
using Paperless.REST.Repositories;
using Paperless.REST.Services;
using Xunit;

namespace Paperless.REST.Tests;

public class DocumentServiceTests
{
    private readonly Mock<IDocumentRepository> _documents = new();
    private readonly Mock<IRepository<DocumentCategory>> _categories = new();
    private readonly DocumentService _service;

    public DocumentServiceTests()
    {
        _service = new DocumentService(_documents.Object, _categories.Object);
    }

    [Fact]
    public async Task GetAllDocumentsAsync_WhenEmpty_ReturnsEmptyList()
    {
        _documents.Setup(x => x.GetAllWithCategoriesAsync())
            .ReturnsAsync(Array.Empty<Document>());

        var result = (await _service.GetAllDocumentsAsync()).ToList();

        Assert.Empty(result);
        _documents.Verify(x => x.GetAllWithCategoriesAsync(), Times.Once);
    }

    [Fact]
    public async Task GetAllDocumentsAsync_MapsDocumentsAndCategoryNames()
    {
        var date = new DateTime(2026, 10, 19, 12, 0, 0, DateTimeKind.Utc);
        _documents.Setup(x => x.GetAllWithCategoriesAsync()).ReturnsAsync(new[]
        {
            new Document { Id = 1, Title = "Invoice", FileName = "invoice.pdf", ContentType = "application/pdf",
                FileSizeBytes = 500, CreatedAt = date, CategoryId = 3,
                Category = new DocumentCategory { Id = 3, Name = "Finance" } },
            new Document { Id = 2, Title = "Notes", FileName = "notes.pdf", FileSizeBytes = 100 }
        });

        var result = (await _service.GetAllDocumentsAsync()).ToList();

        Assert.Equal(2, result.Count);
        Assert.Equal(1, result[0].Id);
        Assert.Equal("Invoice", result[0].Title);
        Assert.Equal("invoice.pdf", result[0].FileName);
        Assert.Equal("application/pdf", result[0].ContentType);
        Assert.Equal(500, result[0].FileSizeBytes);
        Assert.Equal(date, result[0].CreatedAt);
        Assert.Equal(3, result[0].CategoryId);
        Assert.Equal("Finance", result[0].CategoryName);
        Assert.Null(result[1].CategoryName);
        _documents.Verify(x => x.GetAllWithCategoriesAsync(), Times.Once);
    }

    [Fact]
    public async Task GetDocumentByIdAsync_WhenFound_ReturnsMappedDto()
    {
        _documents.Setup(x => x.GetByIdWithCategoryAsync(7)).ReturnsAsync(new Document
        {
            Id = 7,
            Title = "Contract",
            FileName = "contract.pdf",
            FileSizeBytes = 2048,
            CategoryId = 4,
            Category = new DocumentCategory { Id = 4, Name = "Legal" }
        });

        var result = await _service.GetDocumentByIdAsync(7);

        Assert.NotNull(result);
        Assert.Equal(7, result.Id);
        Assert.Equal("Contract", result.Title);
        Assert.Equal("Legal", result.CategoryName);
        _documents.Verify(x => x.GetByIdWithCategoryAsync(7), Times.Once);
    }

    [Fact]
    public async Task GetDocumentByIdAsync_WhenMissing_ReturnsNull()
    {
        _documents.Setup(x => x.GetByIdWithCategoryAsync(99)).ReturnsAsync((Document?)null);

        Assert.Null(await _service.GetDocumentByIdAsync(99));
    }

    [Fact]
    public async Task CreateDocumentAsync_ValidInput_SavesAndReturnsMappedDocument()
    {
        var input = new CreateDocumentDto("Tax report", "tax.pdf", "application/pdf", 4096, 5);
        Document? saved = null;
        _documents.Setup(x => x.AddAsync(It.IsAny<Document>()))
            .Callback<Document>(d => { saved = d; d.Id = 10; })
            .Returns(Task.CompletedTask);
        _documents.Setup(x => x.SaveChangesAsync()).ReturnsAsync(1);
        _documents.Setup(x => x.GetByIdWithCategoryAsync(10))
            .ReturnsAsync(() => new Document
            {
                Id = saved!.Id,
                Title = saved.Title,
                FileName = saved.FileName,
                ContentType = saved.ContentType,
                FileSizeBytes = saved.FileSizeBytes,
                CreatedAt = saved.CreatedAt,
                CategoryId = saved.CategoryId,
                Category = new DocumentCategory { Id = 5, Name = "Finance" }
            });

        var before = DateTime.UtcNow;
        var result = await _service.CreateDocumentAsync(input);
        var after = DateTime.UtcNow;

        Assert.Equal(10, result.Id);
        Assert.Equal("Tax report", result.Title);
        Assert.Equal("tax.pdf", result.FileName);
        Assert.Equal("application/pdf", result.ContentType);
        Assert.Equal(4096, result.FileSizeBytes);
        Assert.Equal(5, result.CategoryId);
        Assert.Equal("Finance", result.CategoryName);
        Assert.InRange(result.CreatedAt, before, after);
        _documents.Verify(x => x.AddAsync(It.IsAny<Document>()), Times.Once);
        _documents.Verify(x => x.SaveChangesAsync(), Times.Once);
        _documents.Verify(x => x.GetByIdWithCategoryAsync(10), Times.Once);
    }

    [Fact]
    public async Task CreateDocumentAsync_NullContentType_DefaultsToPdf()
    {
        Document? saved = null;
        _documents.Setup(x => x.AddAsync(It.IsAny<Document>()))
            .Callback<Document>(d => { saved = d; d.Id = 8; })
            .Returns(Task.CompletedTask);
        _documents.Setup(x => x.GetByIdWithCategoryAsync(8))
            .ReturnsAsync(() => saved);

        var result = await _service.CreateDocumentAsync(
            new CreateDocumentDto("Title", "file.pdf", null, 10, null));

        Assert.Equal("application/pdf", saved!.ContentType);
        Assert.Equal("application/pdf", result.ContentType);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public async Task CreateDocumentAsync_BlankTitle_ThrowsAndDoesNotSave(string title)
    {
        var input = new CreateDocumentDto(title, "file.pdf", "application/pdf", 10, null);

        await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateDocumentAsync(input));

        _documents.Verify(x => x.AddAsync(It.IsAny<Document>()), Times.Never);
        _documents.Verify(x => x.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task CreateDocumentAsync_WhenReloadFails_ThrowsNullReferenceException()
    {
        _documents.Setup(x => x.AddAsync(It.IsAny<Document>())).Returns(Task.CompletedTask);
        _documents.Setup(x => x.GetByIdWithCategoryAsync(It.IsAny<int>()))
            .ReturnsAsync((Document?)null);

        await Assert.ThrowsAsync<NullReferenceException>(() => _service.CreateDocumentAsync(
            new CreateDocumentDto("Title", "file.pdf", null, 10, null)));
    }

    [Fact]
    public async Task UpdateDocumentAsync_ExistingDocument_UpdatesTitleAndCategory()
    {
        var existing = new Document { Id = 12, Title = "Old", FileName = "old.pdf", CategoryId = 1 };
        _documents.Setup(x => x.GetByIdAsync(12)).ReturnsAsync(existing);
        _documents.Setup(x => x.SaveChangesAsync()).ReturnsAsync(1);

        var result = await _service.UpdateDocumentAsync(12, new UpdateDocumentDto("New", 7));

        Assert.True(result);
        Assert.Equal("New", existing.Title);
        Assert.Equal(7, existing.CategoryId);
        Assert.Equal("old.pdf", existing.FileName);
        _documents.Verify(x => x.Update(existing), Times.Once);
        _documents.Verify(x => x.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UpdateDocumentAsync_MissingDocument_ReturnsFalseWithoutSaving()
    {
        _documents.Setup(x => x.GetByIdAsync(404)).ReturnsAsync((Document?)null);

        Assert.False(await _service.UpdateDocumentAsync(404, new UpdateDocumentDto("New", null)));
        _documents.Verify(x => x.Update(It.IsAny<Document>()), Times.Never);
        _documents.Verify(x => x.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task DeleteDocumentAsync_ExistingDocument_DeletesAndSaves()
    {
        var existing = new Document { Id = 13, Title = "Delete me" };
        _documents.Setup(x => x.GetByIdAsync(13)).ReturnsAsync(existing);
        _documents.Setup(x => x.SaveChangesAsync()).ReturnsAsync(1);

        Assert.True(await _service.DeleteDocumentAsync(13));
        _documents.Verify(x => x.Delete(existing), Times.Once);
        _documents.Verify(x => x.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task DeleteDocumentAsync_MissingDocument_ReturnsFalseWithoutDeleting()
    {
        _documents.Setup(x => x.GetByIdAsync(404)).ReturnsAsync((Document?)null);

        Assert.False(await _service.DeleteDocumentAsync(404));
        _documents.Verify(x => x.Delete(It.IsAny<Document>()), Times.Never);
        _documents.Verify(x => x.SaveChangesAsync(), Times.Never);
    }
}
