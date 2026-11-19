using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;
using Paperless.Application.Abstractions;
using Paperless.Application.Exceptions;
using Paperless.Application.Services;

namespace Paperless.Infrastructure.Storage;

public sealed class MinioFileStorage(
    IMinioClient client,
    IOptions<MinioStorageOptions> options,
    ILogger<MinioFileStorage> logger) : IFileStorage
{
    private readonly MinioStorageOptions _options = options.Value;

    public async Task WriteAsync(string objectKey, byte[] content, string contentType, CancellationToken cancellationToken)
    {
        ValidateKey(objectKey);
        if (content.LongLength is <= 0 or > DocumentService.MaxDocumentSizeBytes)
            throw new FileStorageIntegrityException("The original has an unsupported stored size.");
        await ExecuteAsync("write", objectKey, async token =>
        {
            var bucket = new BucketExistsArgs().WithBucket(_options.BucketName);
            if (!await client.BucketExistsAsync(bucket, token))
            {
                try
                {
                    await client.MakeBucketAsync(new MakeBucketArgs().WithBucket(_options.BucketName), token);
                }
                catch (MinioException)
                {
                    // Another API instance may have created the same bucket concurrently.
                    if (!await client.BucketExistsAsync(bucket, token))
                        throw;
                }
            }

            await using var stream = new MemoryStream(content, writable: false);
            await client.PutObjectAsync(new PutObjectArgs().WithBucket(_options.BucketName).WithObject(objectKey)
                .WithStreamData(stream).WithObjectSize(content.LongLength).WithContentType(contentType), token);
        }, cancellationToken);
        logger.LogInformation("Stored original {ObjectKey} ({Size} bytes).", objectKey, content.LongLength);
    }

    public async Task<byte[]> ReadAsync(string objectKey, CancellationToken cancellationToken)
    {
        ValidateKey(objectKey);
        await using var content = new MemoryStream();
        await ExecuteAsync("read", objectKey, async token =>
        {
            await client.GetObjectAsync(new GetObjectArgs().WithBucket(_options.BucketName).WithObject(objectKey)
                .WithCallbackStream(async (stream, callbackToken) =>
                {
                    var buffer = new byte[81920];
                    int count;
                    while ((count = await stream.ReadAsync(buffer, callbackToken)) > 0)
                    {
                        if (content.Length + count > DocumentService.MaxDocumentSizeBytes)
                            throw new FileStorageIntegrityException("The stored original exceeds the supported file size.");
                        await content.WriteAsync(buffer.AsMemory(0, count), callbackToken);
                    }
                }), token);
        }, cancellationToken);
        return content.ToArray();
    }

    public async Task DeleteAsync(string objectKey, CancellationToken cancellationToken)
    {
        ValidateKey(objectKey);
        await ExecuteAsync("delete", objectKey, async token =>
        {
            await client.RemoveIncompleteUploadAsync(new RemoveIncompleteUploadArgs()
                .WithBucket(_options.BucketName).WithObject(objectKey), token);
            await client.RemoveObjectAsync(new RemoveObjectArgs().WithBucket(_options.BucketName).WithObject(objectKey), token);
        }, cancellationToken);
        logger.LogInformation("Removed retired original {ObjectKey}.", objectKey);
    }

    private async Task ExecuteAsync(string operation, string key, Func<CancellationToken, Task> action,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));
        try
        {
            await action(timeout.Token);
        }
        catch (ObjectNotFoundException exception)
        {
            if (operation == "delete")
                return;
            logger.LogError("Original {ObjectKey} is missing from object storage.", key);
            throw new FileStorageIntegrityException("The original referenced by this document is missing from file storage.", exception);
        }
        catch (BucketNotFoundException) when (operation == "delete")
        {
            // An absent bucket cannot contain the retired original or incomplete upload parts.
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is MinioException or HttpRequestException or IOException or OperationCanceledException)
        {
            logger.LogError("File storage {Operation} failed for {ObjectKey} ({FailureType}).", operation, key, exception.GetType().Name);
            throw new FileStorageUnavailableException("File storage is currently unavailable. Please try again later.", exception);
        }
    }

    private static void ValidateKey(string key)
    {
        if (!key.StartsWith("documents/", StringComparison.Ordinal) || key.Length != 42 ||
            !Guid.TryParseExact(key[10..], "N", out _))
            throw new FileStorageIntegrityException("The document has an invalid file storage reference.");
    }
}
