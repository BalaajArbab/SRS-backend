namespace SRS_backend.Services.Categories;

public sealed class CategoryNotFoundException(string message) : Exception(message);
