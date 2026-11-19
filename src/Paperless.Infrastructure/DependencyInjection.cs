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

namespace Paperless.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string? connectionString, IConfiguration configuration, bool runFileCleanup = true)
    {
        services.AddDbContextFactory<PaperlessDbContext>(options => options.UseNpgsql(connectionString, postgres =>
            postgres.MigrationsHistoryTable("__EFMigrationsHistory", "public")));
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
        services.AddScoped<IRepository<DocumentCategory>, Repository<DocumentCategory>>();
        services.AddScoped<IDatabaseInitializer, DatabaseInitializer>();
        return services;
    }

    public static IServiceCollection AddDocumentProcessing(this IServiceCollection services, IConfiguration configuration, bool dispatchOutbox)
    {
        services.AddOptions<ProcessingOptions>().Bind(configuration.GetSection("Processing"))
            .Validate(o => !string.IsNullOrWhiteSpace(o.HostName) && !o.HostName.Contains('/') && o.Port is >= 1 and <= 65535 &&
                !string.IsNullOrWhiteSpace(o.UserName) && o.Password is { Length: >= 8 } && !string.IsNullOrEmpty(o.VirtualHost),
                "Processing requires a broker host, port, virtual host and credentials (password: at least eight characters).")
            .Validate(o => o.BrokerTimeoutSeconds is >= 1 and <= 60 && o.PollIntervalSeconds is >= 1 and <= 60 &&
                o.LeaseSeconds is >= 60 and <= 600 && o.MaxAttempts is >= 1 and <= 10 &&
                o.RetryDelaySeconds is >= 1 and <= 300 && o.RedeliverySeconds is >= 30 and <= 3600,
                "Invalid processing timeout, lease, retry or recovery settings.")
            .Validate<IOptions<MinioStorageOptions>>((o, storage) => o.LeaseSeconds >= storage.Value.TimeoutSeconds + 30,
                "Processing:LeaseSeconds must exceed the MinIO timeout by at least 30 seconds.")
            .ValidateOnStart();
        services.AddScoped<IProcessingRepository, ProcessingRepository>();
        if (dispatchOutbox)
        {
            services.AddSingleton<OutboxRepository>();
            services.AddHostedService<OutboxPublisher>();
        }
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
