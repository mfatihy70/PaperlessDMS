namespace Paperless.Application.Exceptions;

/// <summary>A rejected application input. The delivery layer decides how to present it to its caller.</summary>
public sealed class ApplicationValidationException(string field, string message) : Exception(message)
{
    public string Field { get; } = field;
}
