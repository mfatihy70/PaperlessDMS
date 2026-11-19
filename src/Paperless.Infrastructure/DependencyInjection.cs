using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Minio;
using Paperless.Application.Abstractions;
using Paperless.Domain.Entities;
using Paperless.Infrastructure.Persistence;
using Paperless.Infrastructure.Ocr;
using Paperless.Infrastructure.Processing;
using Paperless.Infrastructure.Repositories;
using Paperless.Infrastructure.Storage;
using Paperless.Infrastructure.Search;
using Paperless.Infrastructure.Summarization;

namespace Paperless.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string? connectionString, IConfiguration configuration, bool runFileCleanup = true)
    {
        services.AddPaperlessPersistence(connectionString);
        services.AddOptions<MinioStorageOptions>().Bind(configuration.GetSection("Minio"))
            .Validate(options => Uri.TryCreate($"http://{options.Endpoint}", UriKind.Absolute, out var endpoint) &&
                endpoint.AbsolutePath == "/" && endpoint.UserInfo.Length == 0 && endpoint.Host.Length > 0 &&
                endpoint.Query.Length == 0 && endpoint.Fragment.Length == 0 && endpoint.Port > 0 && !options.Endpoint.Contains('/'),
                "Minio:Endpoint must be host:port without a scheme or path.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.AccessKey) && options.AccessKey.Length >= 3 && options.SecretKey is { Length: >= 8 },
                "Minio access credentials are required (secret: at least eight characters).")
            .Validate(options => options.BucketName is not null && System.Text.RegularExpressions.Regex.IsMatch(options.BucketName, "^[a-z0-9][a-z0-9.-]{1,61}[a-z0-9]$") &&
                !options.BucketName.Contains(".."),
                "Minio:BucketName must be a valid lowercase bucket name.")
            .Validate(options => options.TimeoutSeconds is >= 1 and <= 120 && options.CleanupIntervalSeconds is >= 1 and <= 300 &&
                options.UploadLeaseMinutes is >= 5 and <= 120, "Invalid file storage timeout, cleanup interval or upload lease.")
            .ValidateOnStart();
        services.AddSingleton<IMinioClient>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<MinioStorageOptions>>().Value;
            return new MinioClient().WithEndpoint(options.Endpoint).WithCredentials(options.AccessKey, options.SecretKey)
                .WithSSL(options.UseSsl).WithTimeout(options.TimeoutSeconds * 1000).Build();
        });
        services.AddSingleton<IFileStorage, MinioFileStorage>();
        services.AddSingleton<FileCleanupWorker>();
        if (runFileCleanup) services.AddHostedService(provider => provider.GetRequiredService<FileCleanupWorker>());
        services.AddScoped<FileMigrationService>();
        services.AddScoped<IDocumentRepository, DocumentRepository>();
        services.AddScoped<ITagRepository, TagRepository>();
        services.AddScoped<IRepository<DocumentCategory>, Repository<DocumentCategory>>();
        return services;
    }

    public static IServiceCollection AddPaperlessPersistence(this IServiceCollection services, string? connectionString)
    {
        services.AddDbContextFactory<PaperlessDbContext>(options => options.UseNpgsql(connectionString, postgres =>
            postgres.MigrationsHistoryTable("__EFMigrationsHistory", "public")));
        services.AddScoped<IDatabaseInitializer, DatabaseInitializer>();
        return services;
    }

    public static IServiceCollection AddMessaging(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ProcessingOptions>().Bind(configuration.GetSection("Processing"))
            .Validate(o => !string.IsNullOrWhiteSpace(o.HostName) && !o.HostName.Contains('/') && o.Port is >= 1 and <= 65535 &&
                !string.IsNullOrWhiteSpace(o.UserName) && o.Password is { Length: >= 8 } && !string.IsNullOrEmpty(o.VirtualHost),
                "Processing requires a broker host, port, virtual host and credentials (password: at least eight characters).")
            .Validate(o => o.BrokerTimeoutSeconds is >= 1 and <= 60 && o.PollIntervalSeconds is >= 1 and <= 60 &&
                o.LeaseSeconds is >= 60 and <= 600 && o.MaxAttempts is >= 1 and <= 10 &&
                o.RetryDelaySeconds is >= 1 and <= 300 && o.RedeliverySeconds is >= 30 and <= 3600,
                "Invalid processing timeout, lease, retry or recovery settings.")
            .ValidateOnStart();
        return services;
    }

    public static IServiceCollection AddDocumentProcessing(this IServiceCollection services, IConfiguration configuration, bool dispatchOutbox)
    {
        services.AddMessaging(configuration);
        services.AddScoped<IProcessingRepository, ProcessingRepository>();
        if (dispatchOutbox)
        {
            services.AddSingleton<IOutboxStore, OutboxRepository>();
            services.AddSingleton<IOutboxStore, SearchOutboxRepository>();
            services.AddSingleton<IOutboxStore, SummaryOutboxRepository>();
            services.AddHostedService<OutboxPublisher>();
        }
        return services;
    }

    public static IServiceCollection AddDocumentSearch(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<SearchOptions>().Bind(configuration.GetSection("Search"))
            .Validate(o => Uri.TryCreate(o.Endpoint, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" &&
                uri.UserInfo.Length == 0 && uri.AbsolutePath == "/" && uri.Query.Length == 0 && uri.Fragment.Length == 0,
                "Search:Endpoint must be an HTTP(S) service root without credentials, path or query.")
            .Validate(o => o.IndexName is not null && System.Text.RegularExpressions.Regex.IsMatch(o.IndexName, "^paperless-[a-z0-9][a-z0-9_-]{1,90}$"),
                "Search:IndexName must be a lowercase paperless- index name.")
            .Validate(o => o.TimeoutSeconds is >= 1 and <= 60 && o.LeaseSeconds is >= 30 and <= 600 &&
                o.LeaseSeconds >= o.TimeoutSeconds * 4 + 10 && o.ReconcileIntervalSeconds is >= 2 and <= 300,
                "Invalid search timeout, lease or recovery interval. Lease must cover index creation and writing.")
            .Validate(o => o.ApiKey is null || !o.ApiKey.Any(char.IsControl), "Search API key must not contain control characters.")
            .ValidateOnStart();
        services.AddHttpClient<ISearchIndex, ElasticsearchIndex>((provider, client) =>
        {
            var settings = provider.GetRequiredService<IOptions<SearchOptions>>().Value;
            client.BaseAddress = new Uri(settings.Endpoint.TrimEnd('/') + "/");
            client.Timeout = Timeout.InfiniteTimeSpan;
            client.MaxResponseContentBufferSize = 2_097_152;
            if (!string.IsNullOrEmpty(settings.ApiKey)) client.DefaultRequestHeaders.Authorization = new("ApiKey", settings.ApiKey);
        });
        services.AddScoped<ISearchRepository, SearchRepository>();
        services.AddScoped<Paperless.Application.Services.DocumentIndexing>();
        return services;
    }

    public static IServiceCollection AddDocumentSummaries(this IServiceCollection services)
    {
        services.AddScoped<ISummaryRepository, SummaryRepository>();
        return services;
    }

    public static IServiceCollection AddGenAiWorker(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<GenAiOptions>().Bind(configuration.GetSection("GenAi"))
            .Validate(o => Uri.TryCreate(o.Endpoint, UriKind.Absolute, out var uri) &&
                (uri.Scheme == "https" || o.AllowInsecureEndpoint && uri.Scheme == "http") &&
                uri.UserInfo.Length == 0 && uri.AbsolutePath == "/" && uri.Query.Length == 0 && uri.Fragment.Length == 0,
                "GenAi:Endpoint must be an HTTPS service root. HTTP is reserved for explicitly configured local fixtures.")
            .Validate(o => o.Model is not null && System.Text.RegularExpressions.Regex.IsMatch(o.Model, "^gemini-[a-z0-9.-]{1,100}$") &&
                o.Language is not null && System.Text.RegularExpressions.Regex.IsMatch(o.Language, "^[a-z]{2,3}(-[A-Za-z]{2,8})?$"),
                "GenAI requires a Gemini model ID and a language code.")
            .Validate(o => !o.Enabled || !string.IsNullOrWhiteSpace(o.ApiKey) && o.ApiKey.Length <= 256 && !o.ApiKey.Any(char.IsWhiteSpace) && !o.ApiKey.Any(char.IsControl),
                "Set a valid GenAi:ApiKey locally before enabling the GenAI worker.")
            .Validate<IOptions<ProcessingOptions>>((o, processing) => o.TimeoutSeconds is >= 1 and <= 180 &&
                o.MaxInputCharacters is >= 1 and <= 1_000_000 && processing.Value.LeaseSeconds >= o.TimeoutSeconds + 30,
                "GenAI input limit must be 1..1000000; timeout 1..180 seconds; processing lease must cover timeout plus 30 seconds.")
            .ValidateOnStart();
        services.AddHttpClient<ISummaryGenerator, GeminiSummaryGenerator>((provider, client) =>
        {
            client.BaseAddress = new Uri(provider.GetRequiredService<IOptions<GenAiOptions>>().Value.Endpoint.TrimEnd('/') + "/");
            client.Timeout = Timeout.InfiniteTimeSpan;
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false }).RemoveAllLoggers();
        services.AddScoped<Paperless.Application.Services.DocumentSummarization>();
        if (configuration.GetValue<bool>("GenAi:Enabled")) services.AddHostedService<SummaryWorker>();
        return services;
    }

    public static IServiceCollection AddOcrWorker(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<OcrOptions>().Bind(configuration.GetSection("Ocr"))
            .Validate(o => o.Languages is { Length: > 0 and <= 64 } &&
                System.Text.RegularExpressions.Regex.IsMatch(o.Languages, "^[a-z]{3}(\\+[a-z]{3})*$"),
                "Ocr:Languages must contain installed language codes such as deu+eng.")
            .Validate(o => new[] { o.TesseractPath, o.PdfInfoPath, o.PdfToPpmPath }
                .All(path => !string.IsNullOrWhiteSpace(path) && !path.Any(char.IsControl)), "OCR tool paths are required.")
            .Validate(o => o.TimeoutSeconds is >= 1 and <= 300 && o.MaxPages is >= 1 and <= 100 &&
                o.MaxTextCharacters is >= 1 and <= 1_000_000 && o.RenderDimension is >= 1000 and <= 6000 &&
                o.MaxImagePixels is >= 1_000_000 and <= 50_000_000 && (long)o.RenderDimension * o.RenderDimension <= o.MaxImagePixels,
                "Invalid OCR timeout, page, text or image limits.")
            .Validate<IOptions<ProcessingOptions>, IOptions<MinioStorageOptions>>((o, processing, storage) =>
                processing.Value.LeaseSeconds >= storage.Value.TimeoutSeconds + o.TimeoutSeconds + 30,
                "Processing:LeaseSeconds must cover the MinIO timeout, OCR timeout and at least 30 seconds for saving results.")
            .ValidateOnStart();
        services.AddSingleton<TesseractOcrEngine>();
        services.AddSingleton<IOcrEngine>(provider => provider.GetRequiredService<TesseractOcrEngine>());
        services.AddScoped<Paperless.Application.Services.DocumentPreparation>();
        services.AddHostedService<OcrRuntimeCheck>();
        return services;
    }
}
