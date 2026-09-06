using MongoDB.Bson;
using MongoDB.Driver;
using SRS_backend.Dtos.Analytics;
using SRS_backend.Models;

namespace SRS_backend.Services.Analytics;

public sealed class CartAdditionService(IMongoDatabase database, TimeProvider timeProvider) : ICartAdditionService
{
    private const string CollectionName = "cartAdditions";
    private const string ProductCollectionName = "products";
    private const string CategoryCollectionName = "categories";

    private readonly IMongoCollection<CartAddition> _cartAdditions = database.GetCollection<CartAddition>(CollectionName);
    private readonly IMongoCollection<Product> _products = database.GetCollection<Product>(ProductCollectionName);
    private readonly IMongoCollection<Category> _categories = database.GetCollection<Category>(CategoryCollectionName);

    public async Task EnsureIndexesAsync(CancellationToken cancellationToken)
    {
        var indexes = new[]
        {
            new CreateIndexModel<CartAddition>(
                Builders<CartAddition>.IndexKeys
                    .Ascending(addition => addition.ProductId)
                    .Descending(addition => addition.AddedAtUtc),
                new CreateIndexOptions { Name = "ix_cart_additions_product_added" }),
            new CreateIndexModel<CartAddition>(
                Builders<CartAddition>.IndexKeys.Descending(addition => addition.AddedAtUtc),
                new CreateIndexOptions { Name = "ix_cart_additions_added" })
        };

        await _cartAdditions.Indexes.CreateManyAsync(indexes, cancellationToken);
    }

    public async Task TrackAsync(TrackCartAdditionRequest request, CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(request.ProductId, out var productId))
        {
            throw new CartAdditionValidationException("ProductId must be a valid MongoDB ObjectId.");
        }

        if (request.QuantityAdded is < 1 or > 100)
        {
            throw new CartAdditionValidationException("QuantityAdded must be between 1 and 100.");
        }

        var product = await _products
            .Find(candidate => candidate.Id == productId && candidate.IsActive)
            .FirstOrDefaultAsync(cancellationToken);
        if (product is null)
        {
            throw new CartAdditionValidationException("The product is not available.");
        }

        var hasActiveCategory = await _categories
            .Find(Builders<Category>.Filter.In(category => category.Id, product.CategoryIds) &
                  Builders<Category>.Filter.Eq(category => category.IsActive, true))
            .AnyAsync(cancellationToken);
        if (!hasActiveCategory)
        {
            throw new CartAdditionValidationException("The product is not available.");
        }

        await _cartAdditions.InsertOneAsync(
            new CartAddition
            {
                Id = ObjectId.GenerateNewId(),
                ProductId = productId,
                QuantityAdded = request.QuantityAdded,
                AddedAtUtc = timeProvider.GetUtcNow().UtcDateTime
            },
            cancellationToken: cancellationToken);
    }
}
