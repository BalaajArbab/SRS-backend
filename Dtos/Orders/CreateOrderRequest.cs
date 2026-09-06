using System.ComponentModel.DataAnnotations;
using SRS_backend.Models;

namespace SRS_backend.Dtos.Orders;

public sealed class CreateOrderRequest
{
    [Required]
    [MinLength(1)]
    [MaxLength(50)]
    public List<CreateOrderItemRequest> Items { get; init; } = [];

    [EnumDataType(typeof(OrderPaymentMethod))]
    public OrderPaymentMethod PaymentMethod { get; init; }

    [Required]
    public OrderCustomerRequest Customer { get; init; } = new();

    [Required]
    public OrderShippingAddressRequest ShippingAddress { get; init; } = new();
}

public sealed class CreateOrderItemRequest
{
    [Required(AllowEmptyStrings = false)]
    [StringLength(24)]
    public string ProductId { get; init; } = string.Empty;

    [Range(1, 100)]
    public int Quantity { get; init; }
}

public sealed class OrderCustomerRequest
{
    [Required(AllowEmptyStrings = false)]
    [StringLength(120)]
    public string Name { get; init; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; init; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    [StringLength(32)]
    public string Phone { get; init; } = string.Empty;
}

public sealed class OrderShippingAddressRequest
{
    [Required(AllowEmptyStrings = false)]
    [StringLength(200)]
    public string AddressLine1 { get; init; } = string.Empty;

    [StringLength(200)]
    public string? AddressLine2 { get; init; }

    [Required(AllowEmptyStrings = false)]
    [StringLength(120)]
    public string City { get; init; } = string.Empty;

    [StringLength(32)]
    public string? PostalCode { get; init; }

    [StringLength(1_000)]
    public string? Notes { get; init; }
}
