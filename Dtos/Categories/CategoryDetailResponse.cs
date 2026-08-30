using SRS_backend.Dtos.Common;

namespace SRS_backend.Dtos.Categories;

public sealed record CategoryDetailResponse(
    CategoryResponse Category,
    PagedResponse<CategoryResponse> Subcategories);
