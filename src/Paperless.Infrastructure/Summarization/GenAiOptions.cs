namespace Paperless.Infrastructure.Summarization;

public sealed class GenAiOptions
{
    public bool Enabled { get; set; }
    public string Endpoint { get; set; } = "https://generativelanguage.googleapis.com/";
    public bool AllowInsecureEndpoint { get; set; }
    public string Model { get; set; } = "gemini-3.8-flash";
    public string? ApiKey { get; set; }
    public string Language { get; set; } = "de";
    public int TimeoutSeconds { get; set; } = 60;
    public int MaxInputCharacters { get; set; } = 100_000;
}
