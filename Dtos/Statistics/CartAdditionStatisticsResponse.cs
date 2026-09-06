namespace SRS_backend.Dtos.Statistics;

public sealed record CartAdditionStatisticsResponse(
    string ProductId,
    string? Name,
    string? Slug,
    long AddToCartCount,
    long QuantityAdded);
