using Moq;
using Paperless.REST.DTOs;
using Paperless.REST.Models;
using Paperless.REST.Repositories;
using Paperless.REST.Services;
using Xunit;

namespace Paperless.REST.Tests;

public class DocumentServiceTests
{
    private readonly Mock<IDocumentRepository> _mockDocRepo;
    private readonly Mock<IRepository<DocumentCategory>> _mockCatRepo;
    private readonly DocumentService _service;

    public DocumentServiceTests()
    {
        _mockDocRepo = new Mock<IDocumentRepository>();
        _mockCatRepo = new Mock<IRepository<DocumentCategory>>();
        _service = new DocumentService(_mockDocRepo.Object, _mockCatRepo.Object);
    }

    [Fact]
    public async Task GetAllDocumentsAsync_ReturnsAllDocuments()
    {
        // Arrange
        var documents = new List<Document>
        {
            new() { Id = 1, Title = "Invoice A", FileName = "inv_a.pdf", FileSizeBytes = 1024 },
            new() { Id = 2, Title = "Contract B", FileName = "con_b.pdf", FileSizeBytes = 2048 }
        };
        _mockDocRepo.Setup(r => r.GetAllWithCategoriesAsync()).ReturnsAsync(documents);

        // Act
        var result = (await _service.GetAllDocumentsAsync()).ToList();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal("Invoice A", result[0].Title);
        _mockDocRepo.Verify(r => r.GetAllWithCategoriesAsync(), Times.Once);
    }

    [Fact]
    public async Task CreateDocumentAsync_PersistsAndReturnsDto()
    {
        // Arrange
        var dto = new CreateDocumentDto("Tax Report", "tax.pdf", "application/pdf", 4096, null);
        var createdEntity = new Document { Id = 10, Title = dto.Title, FileName = dto.FileName, FileSizeBytes = dto.FileSizeBytes };

        _mockDocRepo.Setup(r => r.AddAsync(It.IsAny<Document>())).Returns(Task.CompletedTask);
        _mockDocRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);
        _mockDocRepo.Setup(r => r.GetByIdWithCategoryAsync(It.IsAny<int>())).ReturnsAsync(createdEntity);

        // Act
        var result = await _service.CreateDocumentAsync(dto);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(10, result.Id);
        Assert.Equal("Tax Report", result.Title);
        _mockDocRepo.Verify(r => r.AddAsync(It.IsAny<Document>()), Times.Once);
        _mockDocRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task DeleteDocumentAsync_WhenNotFound_ReturnsFalse()
    {
        // Arrange
        _mockDocRepo.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((Document?)null);

        // Act
        var result = await _service.DeleteDocumentAsync(999);

        // Assert
        Assert.False(result);
        _mockDocRepo.Verify(r => r.Delete(It.IsAny<Document>()), Times.Never);
    }
}