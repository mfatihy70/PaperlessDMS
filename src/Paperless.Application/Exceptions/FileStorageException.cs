namespace Paperless.Application.Exceptions;

public sealed class FileStorageUnavailableException(string message, Exception? innerException = null)
    : Exception(message, innerException);

public sealed class FileStorageIntegrityException(string message, Exception? innerException = null)
    : Exception(message, innerException);

public sealed class ApplicationConflictException(string message, Exception? innerException = null)
    : Exception(message, innerException);
