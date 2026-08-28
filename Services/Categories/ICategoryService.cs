using SRS_backend.Dtos.Categories;

namespace SRS_backend.Services.Categories;

public interface ICategoryService
{
    Task EnsureIndexesAsync(CancellationToken cancellationToken);

    Task<CategoryResponse> CreateAsync(CreateCategoryRequest request, CancellationToken cancellationToken);
}
