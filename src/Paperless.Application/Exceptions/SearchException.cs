namespace Paperless.Application.Exceptions;

/// <summary>A safe failure at the external search boundary.</summary>
public sealed class SearchException(string code, string message, bool retryable, Exception? inner = null) : Exception(message, inner)
{
    public string Code { get; } = code;
    public bool Retryable { get; } = retryable;
}
