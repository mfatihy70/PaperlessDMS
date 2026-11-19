using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Minio;
using Paperless.Application.Abstractions;
using Paperless.Domain.Entities;
using Paperless.Infrastructure.Persistence;
using Paperless.Infrastructure.Repositories;
using Paperless.Infrastructure.Storage;

namespace Paperless.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string? connectionString, IConfiguration configuration)
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
        services.AddHostedService(provider => provider.GetRequiredService<FileCleanupWorker>());
        services.AddScoped<FileMigrationService>();
        services.AddScoped<IDocumentRepository, DocumentRepository>();
        services.AddScoped<IRepository<DocumentCategory>, Repository<DocumentCategory>>();
        services.AddScoped<IDatabaseInitializer, DatabaseInitializer>();
        return services;
    }
}
