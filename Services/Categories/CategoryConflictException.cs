namespace SRS_backend.Services.Categories;

public sealed class CategoryConflictException(string message) : Exception(message);
