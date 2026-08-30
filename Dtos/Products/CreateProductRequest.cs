using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace SRS_backend.Dtos.Products;

public sealed class CreateProductRequest
{
    [Required(AllowEmptyStrings = false)]
    [StringLength(160)]
    public string Name { get; init; } = string.Empty;

    [StringLength(64)]
    public string? Sku { get; init; }

    [StringLength(10_000)]
    public string? Description { get; init; }

    [Required]
    [MinLength(1)]
    public List<string> CategoryIds { get; init; } = [];

    [Range(0, int.MaxValue)]
    public int Price { get; init; }

    [DefaultValue(false)]
    public bool IsOnSale { get; init; } = false;

    [Range(0, int.MaxValue)]
    public int? SalePrice { get; init; }

    [Range(0, int.MaxValue)]
    public int StockQuantity { get; init; }

    [Range(-1_000_000, 1_000_000)]
    public int SortOrder { get; init; }

    [DefaultValue(false)]
    public bool IsActive { get; init; } = false;

    public List<IFormFile> Images { get; init; } = [];
}
