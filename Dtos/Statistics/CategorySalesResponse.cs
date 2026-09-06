namespace SRS_backend.Dtos.Statistics;

public sealed record CategorySalesResponse(
    string CategoryId,
    string Name,
    string Slug,
    long Sales);
