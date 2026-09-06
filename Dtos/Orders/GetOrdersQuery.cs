using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using SRS_backend.Models;

namespace SRS_backend.Dtos.Orders;

public sealed class GetOrdersQuery
{
    [Range(1, 1_000_000)]
    [DefaultValue(1)]
    public int Page { get; init; } = 1;

    [Range(1, 100)]
    [DefaultValue(20)]
    public int PageSize { get; init; } = 20;

    [StringLength(256)]
    public string? CustomerEmail { get; init; }

    public OrderPaymentStatus? PaymentStatus { get; init; }

    public OrderFulfillmentStatus? FulfillmentStatus { get; init; }

    public bool? IsViewed { get; init; }
}
