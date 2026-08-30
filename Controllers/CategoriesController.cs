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
}
