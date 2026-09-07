namespace SRS_backend.Services.Email;

public interface IOrderEmailDeliveryService
{
    Task EnsureIndexesAsync(CancellationToken cancellationToken);
}
