using SRS_backend.Dtos.Common;
using SRS_backend.Dtos.Statistics;

namespace SRS_backend.Services.Statistics;

public interface IStatisticsService
{
    Task<PagedResponse<CategoryInventoryValueResponse>> GetCategoryInventoryValuesAsync(
        GetCategoryInventoryValuesQuery query,
        CancellationToken cancellationToken);

    Task<PagedResponse<CategorySalesResponse>> GetCategorySalesAsync(
        GetCategorySalesQuery query,
        DateOnly? fromDate,
        DateOnly? toDate,
        CancellationToken cancellationToken);

    Task<PagedResponse<CategorySalesResponse>> GetAllCategorySalesAsync(
        GetAllCategorySalesQuery query,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<DailyOrderStatisticsResponse>> GetDailyOrderStatisticsAsync(
        DateOnly? fromDate,
        DateOnly? toDate,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<DailyFulfillmentStatusStatisticsResponse>> GetDailyFulfillmentStatusStatisticsAsync(
        DateOnly? fromDate,
        DateOnly? toDate,
        CancellationToken cancellationToken);
}
