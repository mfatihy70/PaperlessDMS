namespace Paperless.Application.Exceptions;

// Only adapter-authored safe messages may be supplied; never use provider response bodies.
public sealed class SummaryException(string code, string message, bool retryable = false) : Exception(message)
{
    public string Code { get; } = code;
    public bool Retryable { get; } = retryable;
}
