using SRS_backend.Services.Categories;

namespace SRS_backend.Services.Database;

public sealed class MongoDbInitializationService(ICategoryService categoryService) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) => categoryService.EnsureIndexesAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
