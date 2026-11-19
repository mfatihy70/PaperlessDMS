namespace Paperless.Application.Abstractions;

/// <summary>Stores immutable original files independently of the metadata database.</summary>
public interface IFileStorage
{
    Task WriteAsync(string objectKey, byte[] content, string contentType, CancellationToken cancellationToken);
    Task<byte[]> ReadAsync(string objectKey, CancellationToken cancellationToken);
    Task DeleteAsync(string objectKey, CancellationToken cancellationToken);
}
