using System.Security.Cryptography;
using Paperless.Application.Exceptions;

namespace Paperless.Application.Storage;

public static class FileIntegrity
{
    public static string Sha256(byte[] content) => Convert.ToHexString(SHA256.HashData(content));

    public static void Verify(byte[] content, string? expectedSha256, long expectedLength)
    {
        if (content.LongLength != expectedLength || expectedSha256 is null ||
            !string.Equals(Sha256(content), expectedSha256, StringComparison.OrdinalIgnoreCase))
            throw new FileStorageIntegrityException("The stored original failed its integrity check.");
    }
}
