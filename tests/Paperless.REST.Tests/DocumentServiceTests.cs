using Microsoft.AspNetCore.Http;
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
    private readonly DocumentService _service;

    public DocumentServiceTests()
    {
        _service = new DocumentService(_documents.Object);
    }

    [Fact]
    public async Task GetAllDocumentsAsync_MapsDocumentsAndFileAvailability()
    {
        var date = new DateTime(2026, 10, 19, 12, 0, 0, DateTimeKind.Utc);
        _documents.Setup(x => x.GetAllWithCategoriesAsync()).ReturnsAsync(new[]
        {
            new Document
            {
                Id = 1, Title = "Invoice", FileName = "invoice.pdf", ContentType = "application/pdf",
                FileSizeBytes = 500, FileContent = [1, 2], CreatedAt = date, CategoryId = 3,
                Category = new DocumentCategory { Id = 3, Name = "Finance" }
            },
            new Document { Id = 2, Title = "Notes", FileName = "notes.md", FileSizeBytes = 0 }
        });

        var result = (await _service.GetAllDocumentsAsync()).ToList();

        Assert.Equal(2, result.Count);
        Assert.Equal("Finance", result[0].CategoryName);
        Assert.Equal(date, result[0].CreatedAt);
        Assert.True(result[0].HasFile);
        Assert.False(result[1].HasFile);
    }

    [Fact]
    public async Task GetDocumentByIdAsync_WhenMissing_ReturnsNull()
    {
        _documents.Setup(x => x.GetByIdWithCategoryAsync(99)).ReturnsAsync((Document?)null);
        Assert.Null(await _service.GetDocumentByIdAsync(99));
    }

    [Fact]
    public async Task CreateDocumentAsync_StoresOriginalBytesAndDerivesMetadata()
    {
        var bytes = new byte[] { 0x50, 0x4b, 0x03, 0x04, 0xff };
        var file = CreateFile(bytes, @"C:\upload\report.odt", "application/vnd.oasis.opendocument.text");
        Document? saved = null;
        _documents.Setup(x => x.AddAsync(It.IsAny<Document>()))
            .Callback<Document>(document => { saved = document; document.Id = 10; })
            .Returns(Task.CompletedTask);
        _documents.Setup(x => x.GetByIdWithCategoryAsync(10))
            .ReturnsAsync(() => saved);
        _documents.Setup(x => x.SaveChangesAsync()).ReturnsAsync(1);

        var before = DateTime.UtcNow;
        var result = await _service.CreateDocumentAsync(file, 5, CancellationToken.None);
        var after = DateTime.UtcNow;

        Assert.NotNull(saved);
        Assert.Equal("report", saved.Title);
        Assert.Equal("report.odt", saved.FileName);
        Assert.Equal("application/vnd.oasis.opendocument.text", saved.ContentType);
        Assert.Equal(bytes.Length, saved.FileSizeBytes);
        Assert.Equal(bytes, saved.FileContent);
        Assert.Equal(5, saved.CategoryId);
        Assert.InRange(saved.CreatedAt, before, after);
        Assert.True(result.HasFile);
        Assert.Equal(5, result.CategoryId);
        _documents.Verify(x => x.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task CreateDocumentAsync_EmptyFile_ThrowsWithoutSaving()
    {
        var file = CreateFile([], "empty.txt", "text/plain");

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateDocumentAsync(file, null, CancellationToken.None));

        _documents.Verify(x => x.AddAsync(It.IsAny<Document>()), Times.Never);
        _documents.Verify(x => x.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task CreateDocumentAsync_FileLargerThanLimit_ThrowsWithoutSaving()
    {
        var file = CreateFile(
            new byte[(int)DocumentService.MaxDocumentSizeBytes + 1],
            "large.pdf",
            "application/pdf");

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateDocumentAsync(file, null, CancellationToken.None));

        _documents.Verify(x => x.AddAsync(It.IsAny<Document>()), Times.Never);
    }

    [Fact]
    public async Task GetDocumentFileAsync_ReturnsOriginalBytes()
    {
        var bytes = new byte[] { 0, 1, 255 };
        _documents.Setup(x => x.GetByIdAsync(7)).ReturnsAsync(new Document
        {
            Id = 7,
            FileName = "archive.bin",
            ContentType = "application/octet-stream",
            FileSizeBytes = bytes.Length,
            FileContent = bytes
        });

        var result = await _service.GetDocumentFileAsync(7);

        Assert.NotNull(result);
        Assert.Equal("archive.bin", result.FileName);
        Assert.Equal("application/octet-stream", result.ContentType);
        Assert.Equal(bytes, result.Content);
    }

    [Fact]
    public async Task GetDocumentFileAsync_WhenFileIsMissing_ReturnsNull()
    {
        _documents.Setup(x => x.GetByIdAsync(8)).ReturnsAsync(new Document
        {
            Id = 8,
            FileName = "legacy.pdf",
            FileContent = null
        });

        Assert.Null(await _service.GetDocumentFileAsync(8));
    }

    [Fact]
    public async Task UpdateDocumentAsync_WithFile_ReplacesBytesAndDerivedMetadata()
    {
        var existing = new Document
        {
            Id = 12,
            Title = "Old",
            FileName = "old.pdf",
            FileContent = [1],
            CategoryId = 1
        };
        var replacement = CreateFile([4, 5, 6], "new.md", "text/markdown");
        _documents.Setup(x => x.GetByIdAsync(12)).ReturnsAsync(existing);
        _documents.Setup(x => x.SaveChangesAsync()).ReturnsAsync(1);

        var result = await _service.UpdateDocumentAsync(12, replacement, 7, CancellationToken.None);

        Assert.True(result);
        Assert.Equal("new", existing.Title);
        Assert.Equal("new.md", existing.FileName);
        Assert.Equal("text/markdown", existing.ContentType);
        Assert.Equal(3, existing.FileSizeBytes);
        Assert.Equal(new byte[] { 4, 5, 6 }, existing.FileContent);
        Assert.Equal(7, existing.CategoryId);
        _documents.Verify(x => x.Update(existing), Times.Once);
        _documents.Verify(x => x.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UpdateDocumentAsync_WithoutFile_UpdatesCategoryOnly()
    {
        var originalBytes = new byte[] { 7, 8 };
        var existing = new Document
        {
            Id = 12,
            Title = "Old",
            FileName = "old.pdf",
            FileContent = originalBytes,
            CategoryId = 1
        };
        _documents.Setup(x => x.GetByIdAsync(12)).ReturnsAsync(existing);
        _documents.Setup(x => x.SaveChangesAsync()).ReturnsAsync(1);

        var result = await _service.UpdateDocumentAsync(12, null, 2, CancellationToken.None);

        Assert.True(result);
        Assert.Equal("Old", existing.Title);
        Assert.Equal("old.pdf", existing.FileName);
        Assert.Equal(originalBytes, existing.FileContent);
        Assert.Equal(2, existing.CategoryId);
    }

    [Fact]
    public async Task UpdateDocumentAsync_MissingDocument_ReturnsFalseWithoutSaving()
    {
        _documents.Setup(x => x.GetByIdAsync(404)).ReturnsAsync((Document?)null);

        Assert.False(await _service.UpdateDocumentAsync(404, null, null, CancellationToken.None));
        _documents.Verify(x => x.Update(It.IsAny<Document>()), Times.Never);
        _documents.Verify(x => x.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task DeleteDocumentAsync_ExistingDocument_DeletesAndSaves()
    {
        var existing = new Document { Id = 13, Title = "Delete me", FileContent = [1] };
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

    private static FormFile CreateFile(byte[] bytes, string fileName, string contentType)
    {
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }
}
