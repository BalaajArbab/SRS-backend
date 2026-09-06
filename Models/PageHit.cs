using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Text.Json.Serialization;

namespace SRS_backend.Models;

public sealed class PageHit
{
    [BsonId]
    public ObjectId Id { get; init; }

    [BsonElement("pageType")]
    [BsonRepresentation(BsonType.String)]
    public PageHitType PageType { get; init; }

    [BsonElement("resourceId")]
    [BsonIgnoreIfNull]
    public ObjectId? ResourceId { get; init; }

    [BsonElement("viewedAtUtc")]
    public DateTime ViewedAtUtc { get; init; }
}

[JsonConverter(typeof(JsonStringEnumConverter<PageHitType>))]
public enum PageHitType
{
    FrontPage,
    Category,
    Product
}
