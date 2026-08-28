using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace SRS_backend.Dtos.Categories;

public sealed class UpdateCategoryRequest
{
    [Required(AllowEmptyStrings = false)]
    [StringLength(120)]
    public string Name { get; init; } = string.Empty;

    [StringLength(2_000)]
    public string? Description { get; init; }

    [StringLength(512)]
    public string? ImageBlobName { get; init; }

    [StringLength(256)]
    public string? ImageAltText { get; init; }

    [StringLength(24)]
    public string? ParentCategoryId { get; init; }

    [Range(-1_000_000, 1_000_000)]
    public int SortOrder { get; init; }

    [DefaultValue(false)]
    public bool IsActive { get; init; }
}
