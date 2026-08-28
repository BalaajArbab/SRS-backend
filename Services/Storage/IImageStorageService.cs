using Microsoft.AspNetCore.Http;
using MongoDB.Bson;

namespace SRS_backend.Services.Storage;

public interface IImageStorageService
{
    Task<string> UploadAsync(
        ImageStorageLocation location,
        IFormFile image,
        CancellationToken cancellationToken);

    Task DeleteIfExistsAsync(string blobName, CancellationToken cancellationToken);

    string GetPublicUrl(string blobName);

    Task EnsureContainerAsync(CancellationToken cancellationToken);
}
