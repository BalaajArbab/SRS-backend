using System.ComponentModel.DataAnnotations;

namespace SRS_backend.Dtos.Products;

public sealed class GetProductsQuery
{
    [Range(1, 1_000_000)]
    public int Page { get; init; } = 1;

    [Range(1, 100)]
    public int PageSize { get; init; } = 20;
}
