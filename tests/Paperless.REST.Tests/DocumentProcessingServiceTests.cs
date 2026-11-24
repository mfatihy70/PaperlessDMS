using Moq;
using Paperless.Application.Abstractions;
using Paperless.Application.DTOs;
using Paperless.Application.Exceptions;
using Paperless.Application.Services;
using Xunit;

namespace Paperless.REST.Tests;

public class DocumentProcessingServiceTests
{
    private readonly Mock<IProcessingRepository> _repository = new();
    private readonly DocumentProcessingService _service;

    public DocumentProcessingServiceTests()
    {
        _service = new DocumentProcessingService(_repository.Object);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetAsync_InvalidId_Throws(int id)
    {
        await Assert.ThrowsAsync<ApplicationValidationException>(() =>
            _service.GetAsync(id, CancellationToken.None));

        _repository.Verify(x => x.GetAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task RetryAsync_InvalidId_Throws(int id)
    {
        await Assert.ThrowsAsync<ApplicationValidationException>(() =>
            _service.RetryAsync(id, CancellationToken.None));

        _repository.Verify(x => x.RetryAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public async Task GetTextAsync_InvalidId_Throws(int id)
    {
        await Assert.ThrowsAsync<ApplicationValidationException>(() =>
            _service.GetTextAsync(id, CancellationToken.None));

        _repository.Verify(x => x.GetTextAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetAsync_ValidId_CallsRepository()
    {
        _repository.Setup(x => x.GetAsync(10, CancellationToken.None))
            .ReturnsAsync((ProcessingStatusDto?)null);

        var result = await _service.GetAsync(10, CancellationToken.None);

        Assert.Null(result);
        _repository.Verify(x => x.GetAsync(10, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task RetryAsync_ValidId_CallsRepository()
    {
        _repository.Setup(x => x.RetryAsync(20, CancellationToken.None))
            .ReturnsAsync((ProcessingStatusDto?)null);

        var result = await _service.RetryAsync(20, CancellationToken.None);

        Assert.Null(result);
        _repository.Verify(x => x.RetryAsync(20, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task GetTextAsync_ValidId_CallsRepository()
    {
        _repository.Setup(x => x.GetTextAsync(30, CancellationToken.None))
            .ReturnsAsync((DocumentTextDto?)null);

        var result = await _service.GetTextAsync(30, CancellationToken.None);

        Assert.Null(result);
        _repository.Verify(x => x.GetTextAsync(30, CancellationToken.None), Times.Once);
    }
}
