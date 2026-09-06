using SRS_backend.Services.Categories;
using SRS_backend.Services.Analytics;
using SRS_backend.Services.Orders;
using SRS_backend.Services.Products;

namespace SRS_backend.Services.Database;

public sealed class MongoDbInitializationService(
    ICategoryService categoryService,
    IProductService productService,
    IOrderService orderService,
    IPageHitService pageHitService,
    ICartAdditionService cartAdditionService) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await categoryService.EnsureIndexesAsync(cancellationToken);
        await productService.EnsureIndexesAsync(cancellationToken);
        await orderService.EnsureIndexesAsync(cancellationToken);
        await pageHitService.EnsureIndexesAsync(cancellationToken);
        await cartAdditionService.EnsureIndexesAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
