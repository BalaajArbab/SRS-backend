using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace SRS_backend.OpenApi;

internal sealed class ResponseExampleOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        switch (operation.OperationId)
        {
            case "AdminLogin":
                AddJsonExample(operation, "200", CreateLoginResponseExample());
                break;
            case "CreateCategory":
                AddJsonExample(operation, "201", CreateCategoryExample());
                break;
            case "UpdateCategory":
                AddJsonExample(operation, "200", CreateCategoryExample());
                break;
            case "GetCategories":
            case "GetAdminCategories":
                AddJsonExample(operation, "200", CreatePagedCategoryExample());
                break;
            case "CreateProduct":
                AddJsonExample(operation, "201", CreateProductExample());
                break;
            case "UpdateProduct":
                AddJsonExample(operation, "200", CreateProductExample());
                break;
            case "GetActiveProductsByCategory":
            case "GetAdminProductsByCategory":
                AddJsonExample(operation, "200", CreatePagedProductExample());
                break;
        }

        return Task.CompletedTask;
    }

    private static void AddJsonExample(OpenApiOperation operation, string statusCode, JsonNode example)
    {
        if (operation.Responses is null ||
            !operation.Responses.TryGetValue(statusCode, out var response) ||
            response.Content is null ||
            !response.Content.TryGetValue("application/json", out var mediaType))
        {
            return;
        }

        mediaType.Example = example;
    }

    private static JsonObject CreateLoginResponseExample() => new()
    {
        ["accessToken"] = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.example.signature",
        ["tokenType"] = "Bearer",
        ["expiresAt"] = "2026-09-28T12:00:00+00:00"
    };

    private static JsonObject CreateCategoryExample() => new()
    {
        ["id"] = "66d0a1b2c3d4e5f607182930",
        ["name"] = "Summer Collection",
        ["slug"] = "summer-collection",
        ["description"] = "Lightweight seasonal essentials.",
        ["imageUrl"] = "http://127.0.0.1:10000/devstoreaccount1/images/categories/66d0a1b2c3d4e5f607182930/example.webp",
        ["imageAltText"] = "Summer collection",
        ["parentCategoryId"] = null,
        ["sortOrder"] = 0,
        ["isActive"] = true,
        ["createdAtUtc"] = "2026-08-29T12:00:00Z",
        ["updatedAtUtc"] = "2026-08-29T12:00:00Z"
    };

    private static JsonObject CreatePagedCategoryExample() => new()
    {
        ["items"] = new JsonArray(CreateCategoryExample()),
        ["page"] = 1,
        ["pageSize"] = 20,
        ["totalCount"] = 1
    };

    private static JsonObject CreateProductExample() => new()
    {
        ["id"] = "66d0a1b2c3d4e5f607182931",
        ["name"] = "Cotton Summer Shirt",
        ["slug"] = "cotton-summer-shirt",
        ["sku"] = "SHIRT-SUMMER-001",
        ["description"] = "A breathable cotton shirt for everyday wear.",
        ["images"] = new JsonArray
        {
            new JsonObject
            {
                ["id"] = "66d0a1b2c3d4e5f607182932",
                ["url"] = "http://127.0.0.1:10000/devstoreaccount1/images/products/66d0a1b2c3d4e5f607182931/example.webp",
                ["altText"] = "Cotton Summer Shirt",
                ["sortOrder"] = 0
            }
        },
        ["categoryIds"] = new JsonArray("66d0a1b2c3d4e5f607182930"),
        ["price"] = 2999,
        ["stockQuantity"] = 12,
        ["sortOrder"] = 0,
        ["isActive"] = true,
        ["createdAtUtc"] = "2026-08-29T12:00:00Z",
        ["updatedAtUtc"] = "2026-08-29T12:00:00Z"
    };

    private static JsonObject CreatePagedProductExample() => new()
    {
        ["items"] = new JsonArray(CreateProductExample()),
        ["page"] = 1,
        ["pageSize"] = 20,
        ["totalCount"] = 1
    };
}
