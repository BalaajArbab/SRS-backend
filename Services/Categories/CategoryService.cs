using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using MongoDB.Bson;
using MongoDB.Driver;
using SRS_backend.Dtos.Common;
using SRS_backend.Dtos.Categories;
using SRS_backend.Models;

namespace SRS_backend.Services.Categories;

public sealed partial class CategoryService(IMongoDatabase database) : ICategoryService
{
    private const string CollectionName = "categories";
    private readonly IMongoCollection<Category> _categories = database.GetCollection<Category>(CollectionName);

    public async Task EnsureIndexesAsync(CancellationToken cancellationToken)
    {
        var indexes = new[]
        {
            new CreateIndexModel<Category>(
                Builders<Category>.IndexKeys.Ascending(category => category.Name),
                new CreateIndexOptions { Name = "ux_categories_name", Unique = true }),
            new CreateIndexModel<Category>(
                Builders<Category>.IndexKeys.Ascending(category => category.Slug),
                new CreateIndexOptions { Name = "ux_categories_slug", Unique = true }),
            new CreateIndexModel<Category>(
                Builders<Category>.IndexKeys.Ascending(category => category.ParentCategoryId),
                new CreateIndexOptions { Name = "ix_categories_parent_category_id" }),
            new CreateIndexModel<Category>(
                Builders<Category>.IndexKeys
                    .Ascending(category => category.IsActive)
                    .Ascending(category => category.SortOrder)
                    .Ascending(category => category.Name),
                new CreateIndexOptions { Name = "ix_categories_active_sort_order_name" }),
            new CreateIndexModel<Category>(
                Builders<Category>.IndexKeys
                    .Ascending(category => category.SortOrder)
                    .Ascending(category => category.Name),
                new CreateIndexOptions { Name = "ix_categories_sort_order_name" })
        };

        await _categories.Indexes.CreateManyAsync(indexes, cancellationToken);
    }

    public async Task<CategoryResponse> CreateAsync(CreateCategoryRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        var slug = CreateSlug(name);
        var parentCategoryId = await GetParentCategoryIdAsync(
            request.ParentCategoryId,
            cancellationToken: cancellationToken);
        var now = DateTime.UtcNow;
        var category = new Category
        {
            Name = name,
            Slug = slug,
            Description = TrimToNull(request.Description),
            ImageBlobName = TrimToNull(request.ImageBlobName),
            ImageAltText = TrimToNull(request.ImageAltText),
            ParentCategoryId = parentCategoryId,
            SortOrder = request.SortOrder,
            IsActive = request.IsActive,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        try
        {
            await _categories.InsertOneAsync(category, cancellationToken: cancellationToken);
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            throw new CategoryConflictException("A category with the same name or slug already exists.");
        }

        return ToResponse(category);
    }

    public async Task<CategoryResponse> UpdateAsync(
        string categoryId,
        UpdateCategoryRequest request,
        CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(categoryId, out var parsedCategoryId))
        {
            throw new CategoryValidationException("CategoryId must be a valid MongoDB ObjectId.");
        }

        var name = request.Name.Trim();
        var parentCategoryId = await GetParentCategoryIdAsync(
            request.ParentCategoryId,
            parsedCategoryId,
            cancellationToken);
        var update = Builders<Category>.Update
            .Set(category => category.Name, name)
            .Set(category => category.Slug, CreateSlug(name))
            .Set(category => category.Description, TrimToNull(request.Description))
            .Set(category => category.ImageBlobName, TrimToNull(request.ImageBlobName))
            .Set(category => category.ImageAltText, TrimToNull(request.ImageAltText))
            .Set(category => category.ParentCategoryId, parentCategoryId)
            .Set(category => category.SortOrder, request.SortOrder)
            .Set(category => category.IsActive, request.IsActive)
            .Set(category => category.UpdatedAtUtc, DateTime.UtcNow);

        try
        {
            var category = await _categories.FindOneAndUpdateAsync(
                category => category.Id == parsedCategoryId,
                update,
                new FindOneAndUpdateOptions<Category> { ReturnDocument = ReturnDocument.After },
                cancellationToken);

            return category is null
                ? throw new CategoryNotFoundException("The requested category does not exist.")
                : ToResponse(category);
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            throw new CategoryConflictException("A category with the same name or slug already exists.");
        }
    }

    public async Task<PagedResponse<CategoryResponse>> GetActiveAsync(
        GetCategoriesQuery query,
        CancellationToken cancellationToken)
    {
        return await GetAsync(
            Builders<Category>.Filter.Eq(category => category.IsActive, true),
            query,
            cancellationToken);
    }

    public async Task<PagedResponse<CategoryResponse>> GetAllAsync(
        GetCategoriesQuery query,
        CancellationToken cancellationToken)
    {
        return await GetAsync(Builders<Category>.Filter.Empty, query, cancellationToken);
    }

    private async Task<PagedResponse<CategoryResponse>> GetAsync(
        FilterDefinition<Category> filter,
        GetCategoriesQuery query,
        CancellationToken cancellationToken)
    {
        var sort = Builders<Category>.Sort
            .Ascending(category => category.SortOrder)
            .Ascending(category => category.Name);
        var skip = (query.Page - 1) * query.PageSize;

        var categoriesTask = _categories
            .Find(filter)
            .Sort(sort)
            .Skip(skip)
            .Limit(query.PageSize)
            .ToListAsync(cancellationToken);
        var totalCountTask = _categories.CountDocumentsAsync(filter, cancellationToken: cancellationToken);

        await Task.WhenAll(categoriesTask, totalCountTask);

        return new PagedResponse<CategoryResponse>(
            categoriesTask.Result.Select(ToResponse).ToArray(),
            query.Page,
            query.PageSize,
            totalCountTask.Result);
    }

    private async Task<ObjectId?> GetParentCategoryIdAsync(
        string? parentCategoryId,
        ObjectId? categoryId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(parentCategoryId))
        {
            return null;
        }

        if (!ObjectId.TryParse(parentCategoryId, out var parsedParentCategoryId))
        {
            throw new CategoryValidationException("ParentCategoryId must be a valid MongoDB ObjectId.");
        }

        if (parsedParentCategoryId == categoryId)
        {
            throw new CategoryValidationException("A category cannot be its own parent.");
        }

        var parentExists = await _categories
            .Find(category => category.Id == parsedParentCategoryId)
            .AnyAsync(cancellationToken);

        if (!parentExists)
        {
            throw new CategoryValidationException("The specified parent category does not exist.");
        }

        if (categoryId is not null)
        {
            await EnsureParentDoesNotCreateCycleAsync(
                categoryId.Value,
                parsedParentCategoryId,
                cancellationToken);
        }

        return parsedParentCategoryId;
    }

    private async Task EnsureParentDoesNotCreateCycleAsync(
        ObjectId categoryId,
        ObjectId parentCategoryId,
        CancellationToken cancellationToken)
    {
        var currentParentCategoryId = parentCategoryId;

        for (var depth = 0; depth < 100; depth++)
        {
            if (currentParentCategoryId == categoryId)
            {
                throw new CategoryValidationException("The selected parent would create a category hierarchy cycle.");
            }

            var currentParent = await _categories
                .Find(category => category.Id == currentParentCategoryId)
                .FirstOrDefaultAsync(cancellationToken);

            if (currentParent?.ParentCategoryId is null)
            {
                return;
            }

            currentParentCategoryId = currentParent.ParentCategoryId.Value;
        }

        throw new CategoryValidationException("The category hierarchy exceeds the maximum supported depth.");
    }

    private static CategoryResponse ToResponse(Category category) => new(
        category.Id.ToString(),
        category.Name,
        category.Slug,
        category.Description,
        category.ImageBlobName,
        category.ImageAltText,
        category.ParentCategoryId?.ToString(),
        category.SortOrder,
        category.IsActive,
        category.CreatedAtUtc,
        category.UpdatedAtUtc);

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
            throw new CategoryValidationException("Name must contain at least one letter or number that can be used for a slug.");
        }

        return slug;
    }

    [GeneratedRegex("[^a-z0-9]+", RegexOptions.CultureInvariant)]
    private static partial Regex NonSlugCharacterSequence();
}
