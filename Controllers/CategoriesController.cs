using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SRS_backend.Dtos.Common;
using SRS_backend.Dtos.Categories;
using SRS_backend.Security;
using SRS_backend.Services.Categories;

namespace SRS_backend.Controllers.Categories;

[ApiController]
[AllowAnonymous]
[EnableRateLimiting(RateLimitPolicies.PublicApi)]
[Route("api/categories")]
public sealed class CategoriesController(ICategoryService categoryService) : ControllerBase
{
    [HttpGet(Name = "GetCategories")]
    [EndpointSummary("Get active categories.")]
    [EndpointDescription("Returns active categories ordered by sort order and then name. Results are paginated.")]
    [ProducesResponseType(typeof(PagedResponse<CategoryResponse>), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests, "application/problem+json")]
    public async Task<ActionResult<PagedResponse<CategoryResponse>>> GetCategories(
        [FromQuery] GetCategoriesQuery query,
        CancellationToken cancellationToken)
    {
        return Ok(await categoryService.GetActiveAsync(query, cancellationToken));
    }

    [HttpGet("{categoryId}", Name = "GetActiveCategory")]
    [EndpointSummary("Get an active category and its subcategories.")]
    [EndpointDescription("Returns an active category together with its active direct subcategories, ordered by sort order and then name. Subcategories are paginated.")]
    [ProducesResponseType(typeof(CategoryDetailResponse), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests, "application/problem+json")]
    public async Task<ActionResult<CategoryDetailResponse>> GetActiveCategory(
        [FromRoute] string categoryId,
        [FromQuery] GetCategoriesQuery query,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await categoryService.GetActiveDetailAsync(categoryId, query, cancellationToken));
        }
        catch (CategoryValidationException exception)
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
        catch (CategoryNotFoundException exception)
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
