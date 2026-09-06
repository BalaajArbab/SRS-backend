using MongoDB.Bson;
using MongoDB.Driver;
using SRS_backend.Dtos.Analytics;
using SRS_backend.Models;

namespace SRS_backend.Services.Analytics;

public sealed class PageHitService(IMongoDatabase database, TimeProvider timeProvider) : IPageHitService
{
    private const string CollectionName = "pageHits";
    private const string CategoryCollectionName = "categories";
    private const string ProductCollectionName = "products";

    private readonly IMongoCollection<PageHit> _pageHits = database.GetCollection<PageHit>(CollectionName);
    private readonly IMongoCollection<Category> _categories = database.GetCollection<Category>(CategoryCollectionName);
    private readonly IMongoCollection<Product> _products = database.GetCollection<Product>(ProductCollectionName);

    public async Task EnsureIndexesAsync(CancellationToken cancellationToken)
    {
        var indexes = new[]
        {
            new CreateIndexModel<PageHit>(
                Builders<PageHit>.IndexKeys
                    .Ascending(hit => hit.PageType)
                    .Ascending(hit => hit.ResourceId)
                    .Descending(hit => hit.ViewedAtUtc),
                new CreateIndexOptions { Name = "ix_page_hits_type_resource_viewed" }),
            new CreateIndexModel<PageHit>(
                Builders<PageHit>.IndexKeys.Descending(hit => hit.ViewedAtUtc),
                new CreateIndexOptions { Name = "ix_page_hits_viewed" })
        };

        await _pageHits.Indexes.CreateManyAsync(indexes, cancellationToken);
    }

    public async Task TrackAsync(TrackPageHitRequest request, CancellationToken cancellationToken)
    {
        if (request.PageType is null)
        {
            throw new PageHitValidationException("PageType is required.");
        }

        var resourceId = ParseOptionalResourceId(request.ResourceId);
        switch (request.PageType.Value)
        {
            case PageHitType.FrontPage:
                if (resourceId is not null)
                {
                    throw new PageHitValidationException("Front page hits must not include a ResourceId.");
                }

                break;
            case PageHitType.Category:
                await EnsureActiveCategoryAsync(resourceId, cancellationToken);
                break;
            case PageHitType.Product:
                await EnsureActiveProductAsync(resourceId, cancellationToken);
                break;
            default:
                throw new PageHitValidationException("PageType is invalid.");
        }

        await _pageHits.InsertOneAsync(
            new PageHit
            {
                Id = ObjectId.GenerateNewId(),
                PageType = request.PageType.Value,
                ResourceId = resourceId,
                ViewedAtUtc = timeProvider.GetUtcNow().UtcDateTime
            },
            cancellationToken: cancellationToken);
    }

    private async Task EnsureActiveCategoryAsync(ObjectId? resourceId, CancellationToken cancellationToken)
    {
        if (resourceId is null)
        {
            throw new PageHitValidationException("Category page hits require a ResourceId.");
        }

        var exists = await _categories
            .Find(category => category.Id == resourceId.Value && category.IsActive)
            .AnyAsync(cancellationToken);
        if (!exists)
        {
            throw new PageHitValidationException("The category page is not available.");
        }
    }

    private async Task EnsureActiveProductAsync(ObjectId? resourceId, CancellationToken cancellationToken)
    {
        if (resourceId is null)
        {
            throw new PageHitValidationException("Product page hits require a ResourceId.");
        }

        var product = await _products
            .Find(candidate => candidate.Id == resourceId.Value && candidate.IsActive)
            .FirstOrDefaultAsync(cancellationToken);
        if (product is null)
        {
            throw new PageHitValidationException("The product page is not available.");
        }

        var hasActiveCategory = await _categories
            .Find(Builders<Category>.Filter.In(category => category.Id, product.CategoryIds) &
                  Builders<Category>.Filter.Eq(category => category.IsActive, true))
            .AnyAsync(cancellationToken);
        if (!hasActiveCategory)
        {
            throw new PageHitValidationException("The product page is not available.");
        }
    }

    private static ObjectId? ParseOptionalResourceId(string? resourceId)
    {
        if (string.IsNullOrWhiteSpace(resourceId))
        {
            return null;
        }

        if (!ObjectId.TryParse(resourceId, out var parsedResourceId))
        {
            throw new PageHitValidationException("ResourceId must be a valid MongoDB ObjectId.");
        }

        return parsedResourceId;
    }
}
