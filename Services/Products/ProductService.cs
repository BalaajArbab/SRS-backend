using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using MongoDB.Bson;
using MongoDB.Driver;
using SRS_backend.Dtos.Common;
using SRS_backend.Dtos.Products;
using SRS_backend.Models;
using SRS_backend.Services.Storage;

namespace SRS_backend.Services.Products;

public sealed partial class ProductService(
    IMongoDatabase database,
    IImageStorageService imageStorage,
    ILogger<ProductService> logger) : IProductService
{
    private const string CollectionName = "products";
    private const string CategoryCollectionName = "categories";
    private const string ImageBlobPrefix = "products";
    private const int MaximumImagesPerProduct = 8;

    private readonly IMongoCollection<Product> _products = database.GetCollection<Product>(CollectionName);
    private readonly IMongoCollection<Category> _categories = database.GetCollection<Category>(CategoryCollectionName);

    public async Task EnsureIndexesAsync(CancellationToken cancellationToken)
    {
        var indexes = new[]
        {
            new CreateIndexModel<Product>(
                Builders<Product>.IndexKeys.Ascending(product => product.Slug),
                new CreateIndexOptions { Name = "ux_products_slug", Unique = true }),
            new CreateIndexModel<Product>(
                Builders<Product>.IndexKeys.Ascending(product => product.Sku),
                new CreateIndexOptions<Product>
                {
                    Name = "ux_products_sku",
                    Unique = true,
                    PartialFilterExpression = new BsonDocument("sku", new BsonDocument("$type", "string"))
                }),
            new CreateIndexModel<Product>(
                Builders<Product>.IndexKeys
                    .Ascending(product => product.CategoryIds)
                    .Ascending(product => product.IsActive)
                    .Ascending(product => product.SortOrder)
                    .Ascending(product => product.Name),
                new CreateIndexOptions { Name = "ix_products_category_active_sort_name" }),
            new CreateIndexModel<Product>(
                Builders<Product>.IndexKeys
                    .Ascending(product => product.CategoryIds)
                    .Ascending(product => product.SortOrder)
                    .Ascending(product => product.Name),
                new CreateIndexOptions { Name = "ix_products_category_sort_name" })
        };

        await _products.Indexes.CreateManyAsync(indexes, cancellationToken);
    }

    public async Task<ProductResponse> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        var productId = ObjectId.GenerateNewId();
        var categoryIds = await GetValidatedCategoryIdsAsync(request.CategoryIds, cancellationToken);
        var salePrice = GetValidatedSalePrice(request.IsOnSale, request.SalePrice, request.Price);
        var images = await UploadImagesAsync(productId, name, request.Images, cancellationToken);
        var now = DateTime.UtcNow;
        var product = new Product
        {
            Id = productId,
            Name = name,
            Slug = CreateSlug(name),
            Sku = NormalizeSku(request.Sku),
            Description = TrimToNull(request.Description),
            Images = images,
            CategoryIds = categoryIds,
            Price = request.Price,
            IsOnSale = request.IsOnSale,
            SalePrice = salePrice,
            StockQuantity = request.StockQuantity,
            SortOrder = request.SortOrder,
            IsActive = request.IsActive,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        try
        {
            await _products.InsertOneAsync(product, cancellationToken: cancellationToken);
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            await DeleteImagesBestEffortAsync(images, cancellationToken);
            throw new ProductConflictException("A product with the same slug or SKU already exists.");
        }
        catch
        {
            await DeleteImagesBestEffortAsync(images, cancellationToken);
            throw;
        }

        return ToResponse(product);
    }

    public async Task<ProductResponse> UpdateAsync(
        string productId,
        UpdateProductRequest request,
        CancellationToken cancellationToken)
    {
        var parsedProductId = ParseProductId(productId);
        var existingProduct = await _products
            .Find(product => product.Id == parsedProductId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new ProductNotFoundException("The requested product does not exist.");

        var name = request.Name.Trim();
        var categoryIds = await GetValidatedCategoryIdsAsync(request.CategoryIds, cancellationToken);
        var salePrice = GetValidatedSalePrice(request.IsOnSale, request.SalePrice, request.Price);
        List<ProductImage>? replacementImages = null;

        if (request.ReplaceImages)
        {
            replacementImages = await UploadImagesAsync(parsedProductId, name, request.Images, cancellationToken);
        }

        var product = new Product
        {
            Id = existingProduct.Id,
            Name = name,
            Slug = CreateSlug(name),
            Sku = NormalizeSku(request.Sku),
            Description = TrimToNull(request.Description),
            Images = replacementImages ?? existingProduct.Images,
            CategoryIds = categoryIds,
            Price = request.Price,
            IsOnSale = request.IsOnSale,
            SalePrice = salePrice,
            StockQuantity = request.StockQuantity,
            SortOrder = request.SortOrder,
            IsActive = request.IsActive,
            CreatedAtUtc = existingProduct.CreatedAtUtc,
            UpdatedAtUtc = DateTime.UtcNow
        };

        try
        {
            var result = await _products.ReplaceOneAsync(
                existing => existing.Id == parsedProductId,
                product,
                cancellationToken: cancellationToken);

            if (result.MatchedCount == 0)
            {
                await DeleteImagesBestEffortAsync(replacementImages, cancellationToken);
                throw new ProductNotFoundException("The requested product does not exist.");
            }
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            await DeleteImagesBestEffortAsync(replacementImages, cancellationToken);
            throw new ProductConflictException("A product with the same slug or SKU already exists.");
        }
        catch
        {
            await DeleteImagesBestEffortAsync(replacementImages, cancellationToken);
            throw;
        }

        if (replacementImages is not null)
        {
            await DeleteImagesBestEffortAsync(existingProduct.Images, cancellationToken);
        }

        return ToResponse(product);
    }

    public async Task DeleteAsync(string productId, CancellationToken cancellationToken)
    {
        var parsedProductId = ParseProductId(productId);
        var product = await _products
            .Find(candidate => candidate.Id == parsedProductId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new ProductNotFoundException("The requested product does not exist.");

        var result = await _products.DeleteOneAsync(product => product.Id == parsedProductId, cancellationToken);
        if (result.DeletedCount == 0)
        {
            throw new ProductNotFoundException("The requested product does not exist.");
        }

        await DeleteImagesBestEffortAsync(product.Images, cancellationToken);
    }

    public Task<PagedResponse<ProductResponse>> GetAllByCategoryAsync(
        string categoryId,
        GetProductsQuery query,
        CancellationToken cancellationToken) =>
        GetByCategoryAsync(categoryId, query, includeInactiveProducts: true, cancellationToken);

    public Task<PagedResponse<ProductResponse>> GetActiveByCategoryAsync(
        string categoryId,
        GetProductsQuery query,
        CancellationToken cancellationToken) =>
        GetByCategoryAsync(categoryId, query, includeInactiveProducts: false, cancellationToken);

    public async Task<ProductResponse> GetActiveByIdAsync(string productId, CancellationToken cancellationToken)
    {
        var parsedProductId = ParseProductId(productId);
        var product = await _products
            .Find(candidate => candidate.Id == parsedProductId && candidate.IsActive)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new ProductNotFoundException("The requested product is not available.");

        var hasActiveCategory = await _categories
            .Find(category => category.IsActive && product.CategoryIds.Contains(category.Id))
            .Limit(1)
            .FirstOrDefaultAsync(cancellationToken) is not null;

        if (!hasActiveCategory)
        {
            throw new ProductNotFoundException("The requested product is not available.");
        }

        return ToResponse(product);
    }

    private async Task<PagedResponse<ProductResponse>> GetByCategoryAsync(
        string categoryId,
        GetProductsQuery query,
        bool includeInactiveProducts,
        CancellationToken cancellationToken)
    {
        var parsedCategoryId = ParseCategoryId(categoryId);
        await EnsureCategoryAvailableAsync(parsedCategoryId, requireActive: !includeInactiveProducts, cancellationToken);

        var filter = Builders<Product>.Filter.AnyEq(product => product.CategoryIds, parsedCategoryId);
        if (!includeInactiveProducts)
        {
            filter &= Builders<Product>.Filter.Eq(product => product.IsActive, true);
        }

        var sort = Builders<Product>.Sort
            .Ascending(product => product.SortOrder)
            .Ascending(product => product.Name);
        var skip = (query.Page - 1) * query.PageSize;
        var productsTask = _products
            .Find(filter)
            .Sort(sort)
            .Skip(skip)
            .Limit(query.PageSize)
            .ToListAsync(cancellationToken);
        var totalCountTask = _products.CountDocumentsAsync(filter, cancellationToken: cancellationToken);

        await Task.WhenAll(productsTask, totalCountTask);

        return new PagedResponse<ProductResponse>(
            productsTask.Result.Select(ToResponse).ToArray(),
            query.Page,
            query.PageSize,
            totalCountTask.Result);
    }

    private async Task<List<ObjectId>> GetValidatedCategoryIdsAsync(
        IReadOnlyCollection<string> categoryIds,
        CancellationToken cancellationToken)
    {
        if (categoryIds.Count == 0)
        {
            throw new ProductValidationException("At least one category is required.");
        }

        var parsedCategoryIds = new List<ObjectId>(categoryIds.Count);
        var uniqueCategoryIds = new HashSet<ObjectId>();

        foreach (var categoryId in categoryIds)
        {
            if (!ObjectId.TryParse(categoryId, out var parsedCategoryId))
            {
                throw new ProductValidationException("Each CategoryId must be a valid MongoDB ObjectId.");
            }

            if (!uniqueCategoryIds.Add(parsedCategoryId))
            {
                throw new ProductValidationException("CategoryIds must not contain duplicates.");
            }

            parsedCategoryIds.Add(parsedCategoryId);
        }

        var categoriesFound = await _categories.CountDocumentsAsync(
            Builders<Category>.Filter.In(category => category.Id, parsedCategoryIds),
            cancellationToken: cancellationToken);
        if (categoriesFound != parsedCategoryIds.Count)
        {
            throw new ProductValidationException("One or more specified categories do not exist.");
        }

        return parsedCategoryIds;
    }

    private async Task EnsureCategoryAvailableAsync(
        ObjectId categoryId,
        bool requireActive,
        CancellationToken cancellationToken)
    {
        var category = await _categories
            .Find(candidate => candidate.Id == categoryId)
            .FirstOrDefaultAsync(cancellationToken);

        if (category is null || requireActive && !category.IsActive)
        {
            throw new ProductNotFoundException("The requested category is not available.");
        }
    }

    private async Task<List<ProductImage>> UploadImagesAsync(
        ObjectId productId,
        string productName,
        IReadOnlyCollection<Microsoft.AspNetCore.Http.IFormFile> images,
        CancellationToken cancellationToken)
    {
        if (images.Count > MaximumImagesPerProduct)
        {
            throw new ProductValidationException($"A product can have at most {MaximumImagesPerProduct} images.");
        }

        var productImages = new List<ProductImage>(images.Count);

        try
        {
            int i = 1;
            foreach (var image in images)
            {
                var blobName = await imageStorage.UploadAsync(
                    new ImageStorageLocation(ImageBlobPrefix, productId),
                    image,
                    cancellationToken);
                productImages.Add(new ProductImage
                {
                    Id = ObjectId.GenerateNewId(),
                    BlobName = blobName,
                    AltText = productName,
                    SortOrder = i
                });
                i++;
            }

            return productImages;
        }
        catch
        {
            await DeleteImagesBestEffortAsync(productImages, cancellationToken);
            throw;
        }
    }

    private async Task DeleteImagesBestEffortAsync(
        IEnumerable<ProductImage>? images,
        CancellationToken cancellationToken)
    {
        if (images is null)
        {
            return;
        }

        foreach (var image in images)
        {
            try
            {
                await imageStorage.DeleteIfExistsAsync(image.BlobName, cancellationToken);
            }
            catch (ImageStorageException exception)
            {
                logger.LogWarning(exception, "Could not remove product image blob {BlobName}.", image.BlobName);
            }
        }
    }

    private ProductResponse ToResponse(Product product) => new(
        product.Id.ToString(),
        product.Name,
        product.Slug,
        product.Sku,
        product.Description,
        product.Images
            .OrderBy(image => image.SortOrder)
            .Select(image => new ProductImageResponse(
                image.Id.ToString(),
                imageStorage.GetPublicUrl(image.BlobName),
                image.AltText,
                image.SortOrder))
            .ToArray(),
        product.CategoryIds.Select(categoryId => categoryId.ToString()).ToArray(),
        product.Price,
        product.IsOnSale,
        product.SalePrice,
        product.StockQuantity,
        product.SortOrder,
        product.IsActive,
        product.CreatedAtUtc,
        product.UpdatedAtUtc);

    private static ObjectId ParseProductId(string productId)
    {
        if (!ObjectId.TryParse(productId, out var parsedProductId))
        {
            throw new ProductValidationException("ProductId must be a valid MongoDB ObjectId.");
        }

        return parsedProductId;
    }

    private static ObjectId ParseCategoryId(string categoryId)
    {
        if (!ObjectId.TryParse(categoryId, out var parsedCategoryId))
        {
            throw new ProductValidationException("CategoryId must be a valid MongoDB ObjectId.");
        }

        return parsedCategoryId;
    }

    private static string? NormalizeSku(string? sku) => TrimToNull(sku)?.ToUpperInvariant();

    private static int? GetValidatedSalePrice(bool isOnSale, int? salePrice, int price)
    {
        if (!isOnSale)
        {
            return null;
        }

        if (salePrice is null)
        {
            throw new ProductValidationException("SalePrice is required when IsOnSale is true.");
        }

        if (salePrice >= price)
        {
            throw new ProductValidationException("SalePrice must be lower than Price when a product is on sale.");
        }

        return salePrice;
    }

    private static string? TrimToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string CreateSlug(string name)
    {
        var normalizedName = name.Normalize(NormalizationForm.FormD);
        var slugSource = new StringBuilder(normalizedName.Length);

        foreach (var character in normalizedName)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                slugSource.Append(character);
            }
        }

        var slug = NonSlugCharacterSequence().Replace(slugSource.ToString().ToLowerInvariant(), "-").Trim('-');
        if (string.IsNullOrEmpty(slug))
        {
            throw new ProductValidationException("Name must contain at least one letter or number that can be used for a slug.");
        }

        return slug;
    }

    [GeneratedRegex("[^a-z0-9]+", RegexOptions.CultureInvariant)]
    private static partial Regex NonSlugCharacterSequence();
}
