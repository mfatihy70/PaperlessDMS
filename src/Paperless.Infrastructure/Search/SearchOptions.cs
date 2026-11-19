namespace Paperless.Infrastructure.Search;

public sealed class SearchOptions
{
    public string Endpoint { get; set; } = "http://localhost:9200";
    public string IndexName { get; set; } = "paperless-documents-v1";
    public string? ApiKey { get; set; }
    public int TimeoutSeconds { get; set; } = 10;
    public int LeaseSeconds { get; set; } = 60;
    public int ReconcileIntervalSeconds { get; set; } = 30;
}
