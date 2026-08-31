using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace SRS_backend.Models;

public sealed class Category
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("name")]
    public required string Name { get; init; }

    [BsonElement("slug")]
    public required string Slug { get; init; }

    [BsonElement("description")]
    [BsonIgnoreIfNull]
    public string? Description { get; init; }

    [BsonElement("imageBlobName")]
    [BsonIgnoreIfNull]
    public string? ImageBlobName { get; init; }

    [BsonElement("imageAltText")]
    [BsonIgnoreIfNull]
    public string? ImageAltText { get; init; }

    [BsonElement("parentCategoryId")]
    [BsonIgnoreIfNull]
    public ObjectId? ParentCategoryId { get; init; }

    [BsonElement("sortOrder")]
    public int SortOrder { get; init; }

    [BsonElement("isActive")]
    public bool IsActive { get; init; } = false;

    [BsonElement("pastCollection")]
    public bool PastCollection { get; init; } = false;

    [BsonElement("promotedCollection")]
    public bool PromotedCollection { get; init; } = false;

    [BsonElement("navBarName")]
    [BsonIgnoreIfNull]
    public string? NavBarName { get; init; }

    [BsonElement("createdAtUtc")]
    public DateTime CreatedAtUtc { get; init; }

    [BsonElement("updatedAtUtc")]
    public DateTime UpdatedAtUtc { get; init; }
}
