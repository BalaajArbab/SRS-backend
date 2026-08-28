namespace SRS_backend.Services.Categories;

public sealed class CategoryValidationException(string message) : Exception(message);
