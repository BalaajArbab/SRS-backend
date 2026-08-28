namespace SRS_backend.Dtos.Categories;

public sealed record CategoryResponse(
    string Id,
    string Name,
    string Slug,
    string? Description,
    string? ImageUrl,
    string? ImageAltText,
    string? ParentCategoryId,
    int SortOrder,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
