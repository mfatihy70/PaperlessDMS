namespace Paperless.Infrastructure.Processing;

public sealed class ProcessingOptions
{
    public string HostName { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string UserName { get; set; } = "paperless_queue";
    public string Password { get; set; } = "paperless_queue_password";
    public string VirtualHost { get; set; } = "/";
    public bool UseSsl { get; set; }
    public bool DispatchEnabled { get; set; } = true;
    public int BrokerTimeoutSeconds { get; set; } = 10;
    public int PollIntervalSeconds { get; set; } = 2;
    public int LeaseSeconds { get; set; } = 120;
    public int MaxAttempts { get; set; } = 3;
    public int RetryDelaySeconds { get; set; } = 10;
    public int RedeliverySeconds { get; set; } = 300;
}
