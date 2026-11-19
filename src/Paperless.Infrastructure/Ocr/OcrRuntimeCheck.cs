using Microsoft.Extensions.Hosting;

namespace Paperless.Infrastructure.Ocr;

internal sealed class OcrRuntimeCheck(TesseractOcrEngine engine) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) => engine.ValidateDependenciesAsync(cancellationToken);
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
