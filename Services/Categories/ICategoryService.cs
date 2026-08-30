using SRS_backend.Dtos.Common;
using SRS_backend.Dtos.Categories;

namespace SRS_backend.Services.Categories;

public interface ICategoryService
{
    Task EnsureIndexesAsync(CancellationToken cancellationToken);

    Task<CategoryResponse> CreateAsync(CreateCategoryRequest request, CancellationToken cancellationToken);

    Task<CategoryResponse> UpdateAsync(
        string categoryId,
        UpdateCategoryRequest request,
        CancellationToken cancellationToken);

    Task DeleteAsync(string categoryId, CancellationToken cancellationToken);

    Task<PagedResponse<CategoryResponse>> GetActiveAsync(
        GetCategoriesQuery query,
        CancellationToken cancellationToken);

    Task<PagedResponse<CategoryResponse>> GetAllAsync(
        GetCategoriesQuery query,
        CancellationToken cancellationToken);

    Task<CategoryDetailResponse> GetActiveDetailAsync(
        string categoryId,
        GetCategoriesQuery query,
        CancellationToken cancellationToken);
}
