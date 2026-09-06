using SRS_backend.Dtos.Analytics;

namespace SRS_backend.Services.Analytics;

public interface ICartAdditionService
{
    Task EnsureIndexesAsync(CancellationToken cancellationToken);

    Task TrackAsync(TrackCartAdditionRequest request, CancellationToken cancellationToken);
}
