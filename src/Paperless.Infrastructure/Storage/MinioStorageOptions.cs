namespace Paperless.Infrastructure.Storage;

public sealed class MinioStorageOptions
{
    public string Endpoint { get; set; } = string.Empty;
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string BucketName { get; set; } = "paperless-documents";
    public bool UseSsl { get; set; }
    public int TimeoutSeconds { get; set; } = 30;
    public bool CleanupEnabled { get; set; } = true;
    public int CleanupIntervalSeconds { get; set; } = 30;
    public int UploadLeaseMinutes { get; set; } = 10;
}
