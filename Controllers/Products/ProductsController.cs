using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;
using SRS_backend.Dtos.Common;
using SRS_backend.Dtos.Products;
using SRS_backend.Services.Products;

namespace SRS_backend.Controllers.Products;

[ApiController]
[AllowAnonymous]
[Produces("application/json", "application/problem+json")]
[Route("api/categories/{categoryId}/products")]
public sealed class ProductsController(IProductService productService) : ControllerBase
{
    [HttpGet(Name = "GetActiveProductsByCategory")]
    [EndpointSummary("Get active products in a category.")]
    [EndpointDescription("Returns active products in an active category, ordered by sort order and then name. Results are paginated.")]
    [ProducesResponseType(typeof(PagedResponse<ProductResponse>), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
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
}
