using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace SRS_backend.Models;

public sealed class Product
{
    [BsonId]
    public ObjectId Id { get; init; }

    [BsonElement("name")]
    public required string Name { get; init; }

    [BsonElement("slug")]
    public required string Slug { get; init; }

    [BsonElement("sku")]
    [BsonIgnoreIfNull]
    public string? Sku { get; init; }

    [BsonElement("description")]
    [BsonIgnoreIfNull]
    public string? Description { get; init; }

    [BsonElement("tags")]
    public List<string> Tags { get; init; } = [];

    [BsonElement("images")]
    public List<ProductImage> Images { get; init; } = [];

    [BsonElement("categoryIds")]
    public List<ObjectId> CategoryIds { get; init; } = [];

    [BsonElement("price")]
    public int Price { get; init; }

    [BsonElement("isOnSale")]
    public bool IsOnSale { get; init; } = false;

    [BsonElement("salePrice")]
    [BsonIgnoreIfNull]
    public int? SalePrice { get; init; }

    [BsonElement("width")]
    [BsonIgnoreIfNull]
    public decimal? Width { get; init; }

    [BsonElement("height")]
    [BsonIgnoreIfNull]
    public decimal? Height { get; init; }

    [BsonElement("depth")]
    [BsonIgnoreIfNull]
    public decimal? Depth { get; init; }

    [BsonElement("stockQuantity")]
    public int StockQuantity { get; init; }

    [BsonElement("sortOrder")]
    public int SortOrder { get; init; }

    [BsonElement("isActive")]
    public bool IsActive { get; init; } = false;

    [BsonElement("createdAtUtc")]
    public DateTime CreatedAtUtc { get; init; }

    [BsonElement("updatedAtUtc")]
    public DateTime UpdatedAtUtc { get; init; }
}

public sealed class ProductImage
{
    [BsonElement("id")]
    public ObjectId Id { get; init; }

    [BsonElement("blobName")]
    public required string BlobName { get; init; }

    [BsonElement("altText")]
    [BsonIgnoreIfNull]
    public string? AltText { get; init; }

    [BsonElement("sortOrder")]
    public int SortOrder { get; init; }
}
