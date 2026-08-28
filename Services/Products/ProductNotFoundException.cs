namespace SRS_backend.Services.Products;

public sealed class ProductNotFoundException(string message) : Exception(message);
