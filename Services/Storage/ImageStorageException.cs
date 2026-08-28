namespace SRS_backend.Services.Storage;

public sealed class ImageStorageException(string message, Exception? innerException = null)
    : Exception(message, innerException);
