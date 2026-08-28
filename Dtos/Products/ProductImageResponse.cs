namespace SRS_backend.Dtos.Products;

public sealed record ProductImageResponse(
    string Id,
    string Url,
    string? AltText,
    int SortOrder);
