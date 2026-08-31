namespace SRS_backend.Dtos.Products;

public sealed record ProductResponse(
    string Id,
    string Name,
    string Slug,
    string? Sku,
    string? Description,
    IReadOnlyList<string> Tags,
    IReadOnlyList<ProductImageResponse> Images,
    IReadOnlyList<string> CategoryIds,
    int Price,
    bool IsOnSale,
    int? SalePrice,
    decimal? Width,
    decimal? Height,
    decimal? Depth,
    int StockQuantity,
    int SortOrder,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
