using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace SRS_backend.Models;

public sealed class CartAddition
{
    [BsonId]
    public ObjectId Id { get; init; }

    [BsonElement("productId")]
    public ObjectId ProductId { get; init; }

    [BsonElement("quantityAdded")]
    public int QuantityAdded { get; init; }

    [BsonElement("addedAtUtc")]
    public DateTime AddedAtUtc { get; init; }
}
