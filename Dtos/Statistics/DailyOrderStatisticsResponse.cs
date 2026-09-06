namespace SRS_backend.Dtos.Statistics;

public sealed record DailyOrderStatisticsResponse(
    DateOnly Date,
    long OrderCount,
    long Sales,
    IReadOnlyList<DailyCategorySalesResponse> CategorySales);

public sealed record DailyCategorySalesResponse(
    string CategoryId,
    string Name,
    string Slug,
    long Sales);
