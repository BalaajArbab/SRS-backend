using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SRS_backend.Dtos.Common;
using SRS_backend.Dtos.Products;
using SRS_backend.Security;
using SRS_backend.Services.Products;

namespace SRS_backend.Controllers.Products;

[ApiController]
[AllowAnonymous]
[EnableRateLimiting(RateLimitPolicies.PublicApi)]
[Route("api")]
public sealed class ProductsController(IProductService productService) : ControllerBase
{
    [HttpGet("categories/{categoryId}/products", Name = "GetActiveProductsByCategory")]
    [EndpointSummary("Get active products in a category.")]
    [EndpointDescription("Returns active products in an active category, ordered by sort order and then name. Results are paginated.")]
    [ProducesResponseType(typeof(PagedResponse<ProductResponse>), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests, "application/problem+json")]
    public async Task<ActionResult<PagedResponse<ProductResponse>>> GetActiveProductsByCategory(
        [FromRoute] string categoryId,
        [FromQuery] GetProductsQuery query,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await productService.GetActiveByCategoryAsync(categoryId, query, cancellationToken));
        }
        catch (ProductValidationException exception)
        {
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["category"] = [exception.Message]
            })
            {
                Title = "Category validation failed.",
                Status = StatusCodes.Status400BadRequest,
                Type = "https://httpstatuses.com/400"
            });
        }
        catch (ProductNotFoundException exception)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Category not found.",
                Detail = exception.Message,
                Status = StatusCodes.Status404NotFound,
                Type = "https://httpstatuses.com/404"
            });
        }
    }

    [HttpGet("products/{productId}", Name = "GetActiveProduct")]
    [EndpointSummary("Get an active product.")]
    [EndpointDescription("Returns an active product when it belongs to at least one active category.")]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests, "application/problem+json")]
    public async Task<ActionResult<ProductResponse>> GetActiveProduct(
        [FromRoute] string productId,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await productService.GetActiveByIdAsync(productId, cancellationToken));
        }
        catch (ProductValidationException exception)
        {
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["product"] = [exception.Message]
            })
            {
                Title = "Product validation failed.",
                Status = StatusCodes.Status400BadRequest,
                Type = "https://httpstatuses.com/400"
            });
        }
        catch (ProductNotFoundException exception)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Product not found.",
                Detail = exception.Message,
                Status = StatusCodes.Status404NotFound,
                Type = "https://httpstatuses.com/404"
            });
        }
    }
}
