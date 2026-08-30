using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;
using SRS_backend.Dtos.Common;
using SRS_backend.Dtos.Products;
using SRS_backend.Security;
using SRS_backend.Services.Products;
using SRS_backend.Services.Storage;

namespace SRS_backend.Controllers.Admin;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.Admin)]
[Route("api/admin")]
public sealed class AdminProductsController(IProductService productService) : ControllerBase
{
    private const long MaximumProductUploadSizeInBytes = 45L * 1024 * 1024;

    [HttpPost("product", Name = "CreateProduct")]
    [EndpointSummary("Create a product.")]
    [EndpointDescription("Creates a product in one or more existing categories. Submit fields as multipart form data and optionally include up to eight images; each is normalized and stored as WebP.")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaximumProductUploadSizeInBytes)]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status201Created, "application/json")]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<ActionResult<ProductResponse>> CreateProduct(
        [FromForm] CreateProductRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var product = await productService.CreateAsync(request, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, product);
        }
        catch (ProductValidationException exception)
        {
            return ValidationFailure(exception.Message);
        }
        catch (ProductConflictException exception)
        {
            return ConflictProblem("Product already exists.", exception.Message);
        }
        catch (ImageValidationException exception)
        {
            return ImageValidationFailure(exception.Message);
        }
        catch (ImageStorageException exception)
        {
            return ImageStorageUnavailable(exception.Message);
        }
    }

    [HttpPut("product/{productId}", Name = "UpdateProduct")]
    [EndpointSummary("Update a product.")]
    [EndpointDescription("Updates a product's editable fields. Set ReplaceImages to true to replace all stored images with the supplied Images; submit no Images with ReplaceImages true to remove all images.")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaximumProductUploadSizeInBytes)]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<ActionResult<ProductResponse>> UpdateProduct(
        [FromRoute] string productId,
        [FromForm] UpdateProductRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await productService.UpdateAsync(productId, request, cancellationToken));
        }
        catch (ProductValidationException exception)
        {
            return ValidationFailure(exception.Message);
        }
        catch (ProductNotFoundException exception)
        {
            return NotFoundProblem(exception.Message);
        }
        catch (ProductConflictException exception)
        {
            return ConflictProblem("Product already exists.", exception.Message);
        }
        catch (ImageValidationException exception)
        {
            return ImageValidationFailure(exception.Message);
        }
        catch (ImageStorageException exception)
        {
            return ImageStorageUnavailable(exception.Message);
        }
    }

    [HttpDelete("product/{productId}", Name = "DeleteProduct")]
    [EndpointSummary("Delete a product.")]
    [EndpointDescription("Permanently deletes a product and attempts to remove its associated image blobs.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> DeleteProduct(
        [FromRoute] string productId,
        CancellationToken cancellationToken)
    {
        try
        {
            await productService.DeleteAsync(productId, cancellationToken);
            return NoContent();
        }
        catch (ProductValidationException exception)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Product validation failed.",
                Detail = exception.Message,
                Status = StatusCodes.Status400BadRequest,
                Type = "https://httpstatuses.com/400"
            });
        }
        catch (ProductNotFoundException exception)
        {
            return CategoryNotFoundProblem(exception.Message);
        }
    }

    [HttpGet("category/{categoryId}/products", Name = "GetAdminProductsByCategory")]
    [EndpointSummary("Get all products in a category.")]
    [EndpointDescription("Returns active and inactive products assigned to the requested category, ordered by sort order and then name. Results are paginated.")]
    [ProducesResponseType(typeof(PagedResponse<ProductResponse>), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<PagedResponse<ProductResponse>>> GetProductsByCategory(
        [FromRoute] string categoryId,
        [FromQuery] GetProductsQuery query,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await productService.GetAllByCategoryAsync(categoryId, query, cancellationToken));
        }
        catch (ProductValidationException exception)
        {
            return ValidationFailure(exception.Message);
        }
        catch (ProductNotFoundException exception)
        {
            return NotFoundProblem(exception.Message);
        }
    }

    private static ActionResult ValidationFailure(string detail) => new BadRequestObjectResult(new ValidationProblemDetails(
        new Dictionary<string, string[]> { ["product"] = [detail] })
    {
        Title = "Product validation failed.",
        Status = StatusCodes.Status400BadRequest,
        Type = "https://httpstatuses.com/400"
    });

    private static ActionResult ImageValidationFailure(string detail) => new BadRequestObjectResult(new ValidationProblemDetails(
        new Dictionary<string, string[]> { ["images"] = [detail] })
    {
        Title = "Product image validation failed.",
        Status = StatusCodes.Status400BadRequest,
        Type = "https://httpstatuses.com/400"
    });

    private static ActionResult NotFoundProblem(string detail) => new NotFoundObjectResult(new ProblemDetails
    {
        Title = "Product not found.",
        Detail = detail,
        Status = StatusCodes.Status404NotFound,
        Type = "https://httpstatuses.com/404"
    });

    private static ActionResult CategoryNotFoundProblem(string detail) => new NotFoundObjectResult(new ProblemDetails
    {
        Title = "Category not found.",
        Detail = detail,
        Status = StatusCodes.Status404NotFound,
        Type = "https://httpstatuses.com/404"
    });

    private static ActionResult ConflictProblem(string title, string detail) => new ConflictObjectResult(new ProblemDetails
    {
        Title = title,
        Detail = detail,
        Status = StatusCodes.Status409Conflict,
        Type = "https://httpstatuses.com/409"
    });

    private static ActionResult ImageStorageUnavailable(string detail) => new ObjectResult(new ProblemDetails
    {
        Title = "Product image storage is unavailable.",
        Detail = detail,
        Status = StatusCodes.Status503ServiceUnavailable,
        Type = "https://httpstatuses.com/503"
    })
    {
        StatusCode = StatusCodes.Status503ServiceUnavailable
    };
}
