using MongoDB.Bson;

namespace SRS_backend.Services.Storage;

public sealed record ImageStorageLocation(string BlobPrefix, ObjectId OwnerId);
