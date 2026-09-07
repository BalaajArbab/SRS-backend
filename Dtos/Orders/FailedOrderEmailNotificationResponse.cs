using SRS_backend.Models;

namespace SRS_backend.Dtos.Orders;

public sealed record FailedOrderEmailNotificationResponse(
    string OrderId,
    string OrderNumber,
    string NotificationId,
    OrderEmailNotificationKind Kind,
    string RecipientAddress,
    int AttemptCount);
