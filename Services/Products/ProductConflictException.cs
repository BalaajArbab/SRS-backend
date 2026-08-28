namespace SRS_backend.Services.Products;

public sealed class ProductConflictException(string message) : Exception(message);
