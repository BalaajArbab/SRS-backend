namespace SRS_backend.Services.Storage;

public sealed class BlobStorageInitializationService(ICategoryImageStorageService categoryImageStorage) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) => categoryImageStorage.EnsureContainerAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
