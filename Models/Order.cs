using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Text.Json.Serialization;

namespace SRS_backend.Models;

public sealed class Order
{
    [BsonId]
    public ObjectId Id { get; init; }

    [BsonElement("orderNumber")]
    public required string OrderNumber { get; init; }

    [BsonElement("idempotencyKeyHash")]
    public required string IdempotencyKeyHash { get; init; }

    [BsonElement("customer")]
    public required OrderCustomer Customer { get; init; }

    [BsonElement("shippingAddress")]
    public required OrderShippingAddress ShippingAddress { get; init; }

    [BsonElement("items")]
    public List<OrderItem> Items { get; init; } = [];

    [BsonElement("subtotal")]
    public int Subtotal { get; init; }

    [BsonElement("deliveryFee")]
    public int DeliveryFee { get; init; }

    [BsonElement("total")]
    public int Total { get; init; }

    [BsonElement("paymentMethod")]
    [BsonRepresentation(BsonType.String)]
    public OrderPaymentMethod PaymentMethod { get; init; }

    [BsonElement("paymentStatus")]
    [BsonRepresentation(BsonType.String)]
    public OrderPaymentStatus PaymentStatus { get; init; }

    [BsonElement("fulfillmentStatus")]
    [BsonRepresentation(BsonType.String)]
    public OrderFulfillmentStatus FulfillmentStatus { get; init; }

    [BsonElement("isViewed")]
    public bool IsViewed { get; init; }

    [BsonElement("bankTransferReference")]
    [BsonIgnoreIfNull]
    public string? BankTransferReference { get; init; }

    [BsonElement("stockRestoredAtUtc")]
    [BsonIgnoreIfNull]
    public DateTime? StockRestoredAtUtc { get; init; }

    [BsonElement("createdAtUtc")]
    public DateTime CreatedAtUtc { get; init; }

    [BsonElement("updatedAtUtc")]
    public DateTime UpdatedAtUtc { get; init; }
}

public sealed class OrderCustomer
{
    [BsonElement("name")]
    public required string Name { get; init; }

    [BsonElement("email")]
    public required string Email { get; init; }

    [BsonElement("phone")]
    public required string Phone { get; init; }
}

public sealed class OrderShippingAddress
{
    [BsonElement("addressLine1")]
    public required string AddressLine1 { get; init; }

    [BsonElement("addressLine2")]
    [BsonIgnoreIfNull]
    public string? AddressLine2 { get; init; }

    [BsonElement("city")]
    public required string City { get; init; }

    [BsonElement("postalCode")]
    [BsonIgnoreIfNull]
    public string? PostalCode { get; init; }

    [BsonElement("notes")]
    [BsonIgnoreIfNull]
    public string? Notes { get; init; }
}

public sealed class OrderItem
{
    [BsonElement("productId")]
    public ObjectId ProductId { get; init; }

    [BsonElement("name")]
    public required string Name { get; init; }

    [BsonElement("sku")]
    [BsonIgnoreIfNull]
    public string? Sku { get; init; }

    [BsonElement("unitPrice")]
    public int UnitPrice { get; init; }

    [BsonElement("quantity")]
    public int Quantity { get; init; }

    [BsonElement("lineTotal")]
    public int LineTotal { get; init; }
}

[JsonConverter(typeof(JsonStringEnumConverter<OrderPaymentMethod>))]
public enum OrderPaymentMethod
{
    CashOnDelivery,
    BankTransfer
}

[JsonConverter(typeof(JsonStringEnumConverter<OrderPaymentStatus>))]
public enum OrderPaymentStatus
{
    NotApplicable,
    Pending,
    Paid,
    Rejected
}

[JsonConverter(typeof(JsonStringEnumConverter<OrderFulfillmentStatus>))]
public enum OrderFulfillmentStatus
{
    Pending,
    Confirmed,
    Processing,
    Shipped,
    Delivered,
    Cancelled
}
