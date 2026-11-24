using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Paperless.Application.Exceptions;
using Paperless.REST.ExceptionHandling;
using Xunit;

namespace Paperless.REST.Tests;

public class FileStorageExceptionHandlerTests
{
    private readonly Mock<IProblemDetailsService> _problemDetails = new();

    private FileStorageExceptionHandler CreateHandler()
    {
        _problemDetails.Setup(x => x.WriteAsync(It.IsAny<ProblemDetailsContext>()))
            .Returns(ValueTask.CompletedTask);

        return new FileStorageExceptionHandler(
            _problemDetails.Object,
            Mock.Of<ILogger<FileStorageExceptionHandler>>());
    }

    [Theory]
    [InlineData(503)]
    [InlineData(502)]
    [InlineData(409)]
    public async Task KnownException_ReturnsExpectedStatus(int status)
    {
        var exception = status switch
        {
            503 => (Exception)new FileStorageUnavailableException("Storage unavailable"),
            502 => new FileStorageIntegrityException("Invalid checksum"),
            _ => new ApplicationConflictException("Document changed")
        };
        var context = new DefaultHttpContext();
        var handler = CreateHandler();

        var handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(status, context.Response.StatusCode);
        _problemDetails.Verify(x => x.WriteAsync(It.Is<ProblemDetailsContext>(
            p => p.ProblemDetails.Status == status)), Times.Once);
    }

    [Fact]
    public async Task UnknownException_IsNotHandled()
    {
        var context = new DefaultHttpContext();
        var handler = CreateHandler();

        var handled = await handler.TryHandleAsync(
            context, new InvalidOperationException("Unknown error"), CancellationToken.None);

        Assert.False(handled);
        _problemDetails.Verify(x => x.WriteAsync(It.IsAny<ProblemDetailsContext>()), Times.Never);
    }
}
