namespace Paperless.Application.Abstractions;

/// <summary>Recognizes text from a verified original. Infrastructure owns native tools and resource limits.</summary>
public interface IOcrEngine
{
    Task<OcrExtraction> ExtractAsync(byte[] original, CancellationToken cancellationToken);
}

public sealed record OcrExtraction(string Text, int PageCount, string Language);
