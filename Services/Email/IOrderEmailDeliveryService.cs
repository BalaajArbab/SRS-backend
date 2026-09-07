using SRS_backend.Dtos.Common;
using SRS_backend.Dtos.Orders;

namespace SRS_backend.Services.Email;

public interface IOrderEmailDeliveryService
{
    Task EnsureIndexesAsync(CancellationToken cancellationToken);

    Task<PagedResponse<FailedOrderEmailNotificationResponse>> GetFailedAsync(
        GetFailedOrderEmailNotificationsQuery query,
        CancellationToken cancellationToken);

    Task RetryFailedAsync(string orderId, string notificationId, CancellationToken cancellationToken);
}
