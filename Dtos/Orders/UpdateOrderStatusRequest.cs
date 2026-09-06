using System.ComponentModel.DataAnnotations;
using SRS_backend.Models;

namespace SRS_backend.Dtos.Orders;

public sealed class UpdateOrderStatusRequest
{
    [Required]
    public OrderPaymentStatus? PaymentStatus { get; init; }

    [Required]
    public OrderFulfillmentStatus? FulfillmentStatus { get; init; }

    [StringLength(128)]
    public string? BankTransferReference { get; init; }
}
