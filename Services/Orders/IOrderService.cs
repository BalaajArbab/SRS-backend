using SRS_backend.Dtos.Common;
using SRS_backend.Dtos.Orders;

namespace SRS_backend.Services.Orders;

public interface IOrderService
{
    Task EnsureIndexesAsync(CancellationToken cancellationToken);

    Task<OrderResponse> CreateAsync(
        CreateOrderRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken);

    Task<PagedResponse<OrderResponse>> GetAsync(GetOrdersQuery query, CancellationToken cancellationToken);

    Task<OrderResponse> GetByIdAsync(string orderId, CancellationToken cancellationToken);

    Task<OrderResponse> UpdateStatusAsync(
        string orderId,
        UpdateOrderStatusRequest request,
        CancellationToken cancellationToken);
}
