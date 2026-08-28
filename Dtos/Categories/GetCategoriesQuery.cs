using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace SRS_backend.Dtos.Categories;

public sealed class GetCategoriesQuery
{
    [Range(1, 1_000_000)]
    [DefaultValue(1)]
    public int Page { get; init; } = 1;

    [Range(1, 100)]
    [DefaultValue(20)]
    public int PageSize { get; init; } = 20;
}
