namespace SRS_backend.Services.Storage;

public sealed class BlobStorageInitializationService(IImageStorageService imageStorage) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) => imageStorage.EnsureContainerAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
