using SRS_backend.Services.Categories;
using SRS_backend.Services.Products;

namespace SRS_backend.Services.Database;

public sealed class MongoDbInitializationService(
    ICategoryService categoryService,
    IProductService productService) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await categoryService.EnsureIndexesAsync(cancellationToken);
        await productService.EnsureIndexesAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
