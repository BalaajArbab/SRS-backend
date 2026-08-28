using Microsoft.AspNetCore.Http;
using MongoDB.Bson;

namespace SRS_backend.Services.Storage;

public interface ICategoryImageStorageService
{
    Task<string> UploadAsync(ObjectId categoryId, IFormFile image, CancellationToken cancellationToken);

    Task DeleteIfExistsAsync(string blobName, CancellationToken cancellationToken);

    string GetPublicUrl(string blobName);

    Task EnsureContainerAsync(CancellationToken cancellationToken);
}
