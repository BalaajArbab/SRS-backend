namespace SRS_backend.Services.Storage;

public sealed class CategoryImageStorageException(string message, Exception? innerException = null)
    : Exception(message, innerException);
