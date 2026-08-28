using SRS_backend.Dtos.Common;
using SRS_backend.Dtos.Products;

namespace SRS_backend.Services.Products;

public interface IProductService
{
    Task EnsureIndexesAsync(CancellationToken cancellationToken);

    Task<ProductResponse> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken);

    Task<ProductResponse> UpdateAsync(string productId, UpdateProductRequest request, CancellationToken cancellationToken);

    Task DeleteAsync(string productId, CancellationToken cancellationToken);

    Task<PagedResponse<ProductResponse>> GetAllByCategoryAsync(
        string categoryId,
        GetProductsQuery query,
        CancellationToken cancellationToken);

    Task<PagedResponse<ProductResponse>> GetActiveByCategoryAsync(
        string categoryId,
        GetProductsQuery query,
        CancellationToken cancellationToken);
}
