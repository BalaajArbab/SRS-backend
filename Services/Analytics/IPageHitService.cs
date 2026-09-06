using SRS_backend.Dtos.Analytics;

namespace SRS_backend.Services.Analytics;

public interface IPageHitService
{
    Task EnsureIndexesAsync(CancellationToken cancellationToken);

    Task TrackAsync(TrackPageHitRequest request, CancellationToken cancellationToken);
}
