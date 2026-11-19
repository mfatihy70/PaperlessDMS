namespace Paperless.Infrastructure.Persistence;

/// <summary>A durable unclaimed-upload lease or retired-object deletion job.</summary>
public sealed class FileCleanupJob
{
    public string ObjectKey { get; set; } = string.Empty;
    public DateTime NotBefore { get; set; }
    public int Attempts { get; set; }
}
