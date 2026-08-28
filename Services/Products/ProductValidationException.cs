namespace SRS_backend.Services.Products;

public sealed class ProductValidationException(string message) : Exception(message);
