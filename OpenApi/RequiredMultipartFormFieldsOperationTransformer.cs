using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace SRS_backend.OpenApi;

internal sealed class RequiredMultipartFormFieldsOperationTransformer : IOpenApiOperationTransformer
{
    private const string MultipartFormDataContentType = "multipart/form-data";

    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (operation.RequestBody?.Content is null ||
            !operation.RequestBody.Content.TryGetValue(MultipartFormDataContentType, out var mediaType) ||
            mediaType.Schema is not OpenApiSchema schema ||
            context.Description.ActionDescriptor is not ControllerActionDescriptor actionDescriptor)
        {
            return Task.CompletedTask;
        }

        var formRequestType = actionDescriptor.MethodInfo
            .GetParameters()
            .FirstOrDefault(parameter => parameter.GetCustomAttribute<FromFormAttribute>() is not null)
            ?.ParameterType;
        if (formRequestType is null)
        {
            return Task.CompletedTask;
        }

        var requiredProperties = formRequestType
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => property.GetCustomAttribute<RequiredAttribute>() is not null)
            .ToArray();

        if (requiredProperties.Length == 0)
        {
            return Task.CompletedTask;
        }

        schema.Required ??= new HashSet<string>(StringComparer.Ordinal);
        foreach (var requiredProperty in requiredProperties)
        {
            var schemaPropertyName = GetSchemaPropertyName(schema, requiredProperty);
            if (schemaPropertyName is not null)
            {
                schema.Required.Add(schemaPropertyName);
            }
        }

        return Task.CompletedTask;
    }

    private static string? GetSchemaPropertyName(OpenApiSchema schema, PropertyInfo property)
    {
        if (schema.Properties is null)
        {
            return null;
        }

        var jsonPropertyName = property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name;
        var candidates = new[]
        {
            jsonPropertyName,
            JsonNamingPolicy.CamelCase.ConvertName(property.Name),
            property.Name
        };

        return schema.Properties.Keys.FirstOrDefault(schemaPropertyName => candidates
            .Where(candidate => candidate is not null)
            .Any(candidate => string.Equals(schemaPropertyName, candidate, StringComparison.OrdinalIgnoreCase)));
    }
}
