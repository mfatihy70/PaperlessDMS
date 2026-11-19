namespace Paperless.Application.Exceptions;

/// <summary>A safe, classified OCR failure that can be persisted without exposing native tool output.</summary>
public sealed class OcrException(string code, string message, bool retryable, Exception? inner = null) : Exception(message, inner)
{
    public string Code { get; } = code;
    public bool Retryable { get; } = retryable;
}
