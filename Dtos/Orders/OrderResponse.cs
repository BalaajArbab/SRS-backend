using SRS_backend.Models;

namespace SRS_backend.Dtos.Orders;

public sealed record OrderResponse(
    string Id,
    string OrderNumber,
    OrderCustomerResponse Customer,
    OrderShippingAddressResponse ShippingAddress,
    IReadOnlyList<OrderItemResponse> Items,
    int Subtotal,
    int DeliveryFee,
    int Total,
    OrderPaymentMethod PaymentMethod,
    OrderPaymentStatus PaymentStatus,
    OrderFulfillmentStatus FulfillmentStatus,
    bool IsViewed,
    string? BankTransferReference,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record OrderCustomerResponse(string Name, string Email, string Phone);

public sealed record OrderShippingAddressResponse(
    string AddressLine1,
    string? AddressLine2,
    string City,
    string? PostalCode,
    string? Notes);

public sealed record OrderItemResponse(
    string ProductId,
    string Name,
    string? Sku,
    int UnitPrice,
    int Quantity,
    int LineTotal);
