using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using MongoDB.Bson;
using MongoDB.Driver;
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
                new CreateIndexOptions { Name = "ix_categories_parent_category_id" })
        };

        await _categories.Indexes.CreateManyAsync(indexes, cancellationToken);
    }

    public async Task<CategoryResponse> CreateAsync(CreateCategoryRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        var slug = CreateSlug(name);
        var parentCategoryId = await GetParentCategoryIdAsync(request.ParentCategoryId, cancellationToken);
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

    private async Task<ObjectId?> GetParentCategoryIdAsync(string? parentCategoryId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(parentCategoryId))
        {
            return null;
        }

        if (!ObjectId.TryParse(parentCategoryId, out var parsedParentCategoryId))
        {
            throw new CategoryValidationException("ParentCategoryId must be a valid MongoDB ObjectId.");
        }

        var parentExists = await _categories
            .Find(category => category.Id == parsedParentCategoryId)
            .AnyAsync(cancellationToken);

        if (!parentExists)
        {
            throw new CategoryValidationException("The specified parent category does not exist.");
        }

        return parsedParentCategoryId;
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
