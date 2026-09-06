using SRS_backend.Models;

namespace SRS_backend.Dtos.Statistics;

public sealed record DailyFulfillmentStatusStatisticsResponse(
    DateOnly Date,
    long OrderCount,
    long Sales,
    IReadOnlyList<DailyFulfillmentStatusSalesResponse> FulfillmentStatusSales);

public sealed record DailyFulfillmentStatusSalesResponse(
    OrderFulfillmentStatus FulfillmentStatus,
    long OrderCount,
    long Sales);
