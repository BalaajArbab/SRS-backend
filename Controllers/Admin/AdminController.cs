using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;
using SRS_backend.Dtos.Common;
using SRS_backend.Dtos.Categories;
using SRS_backend.Security;
using SRS_backend.Services.Categories;
using SRS_backend.Services.Storage;

namespace SRS_backend.Controllers.Admin;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.Admin)]
[Produces("application/json", "application/problem+json")]
[Route("api/admin")]
public sealed class AdminController(ICategoryService categoryService) : ControllerBase
{
    [HttpDelete("category/{categoryId}", Name = "DeleteCategory")]
    [EndpointSummary("Delete a category.")]
    [EndpointDescription("Permanently deletes a category that has no child categories.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> DeleteCategory(
        [FromRoute] string categoryId,
        CancellationToken cancellationToken)
    {
        try
        {
            await categoryService.DeleteAsync(categoryId, cancellationToken);
            return NoContent();
        }
        catch (CategoryValidationException exception)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Category validation failed.",
                Detail = exception.Message,
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
        catch (CategoryConflictException exception)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Category cannot be deleted.",
                Detail = exception.Message,
                Status = StatusCodes.Status409Conflict,
                Type = "https://httpstatuses.com/409"
            });
        }
    }

    [HttpPut("category/{categoryId}", Name = "UpdateCategory")]
    [EndpointSummary("Update a category.")]
    [EndpointDescription("Updates a category's editable fields, optionally replaces its single image with a normalized WebP image, and regenerates its URL slug from its name.")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(CategoryResponse), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<ActionResult<CategoryResponse>> UpdateCategory(
        [FromRoute] string categoryId,
        [FromForm] UpdateCategoryRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await categoryService.UpdateAsync(categoryId, request, cancellationToken));
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
        catch (CategoryConflictException exception)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Category already exists.",
                Detail = exception.Message,
                Status = StatusCodes.Status409Conflict,
                Type = "https://httpstatuses.com/409"
            });
        }
        catch (ImageValidationException exception)
        {
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["image"] = [exception.Message]
            })
            {
                Title = "Category image validation failed.",
                Status = StatusCodes.Status400BadRequest,
                Type = "https://httpstatuses.com/400"
            });
        }
        catch (ImageStorageException exception)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
            {
                Title = "Category image storage is unavailable.",
                Detail = exception.Message,
                Status = StatusCodes.Status503ServiceUnavailable,
                Type = "https://httpstatuses.com/503"
            });
        }
    }

    [HttpGet("category", Name = "GetAdminCategories")]
    [EndpointSummary("Get all categories.")]
    [EndpointDescription("Returns active and inactive categories ordered by sort order and then name. Results are paginated.")]
    [ProducesResponseType(typeof(PagedResponse<CategoryResponse>), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<ActionResult<PagedResponse<CategoryResponse>>> GetCategories(
        [FromQuery] GetCategoriesQuery query,
        CancellationToken cancellationToken)
    {
        return Ok(await categoryService.GetAllAsync(query, cancellationToken));
    }

    [HttpPost("category", Name = "CreateCategory")]
    [EndpointSummary("Create a category.")]
    [EndpointDescription("Creates a category with a unique name and URL slug. Submit fields as multipart form data and optionally include one JPEG, PNG, or WebP image up to 5 MB; it is resized to a 1600px maximum dimension and stored as WebP.")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(CategoryResponse), StatusCodes.Status201Created, "application/json")]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<ActionResult<CategoryResponse>> CreateCategory(
        [FromForm] CreateCategoryRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var category = await categoryService.CreateAsync(request, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, category);
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
        catch (CategoryConflictException exception)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Category already exists.",
                Detail = exception.Message,
                Status = StatusCodes.Status409Conflict,
                Type = "https://httpstatuses.com/409"
            });
        }
        catch (ImageValidationException exception)
        {
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["image"] = [exception.Message]
            })
            {
                Title = "Category image validation failed.",
                Status = StatusCodes.Status400BadRequest,
                Type = "https://httpstatuses.com/400"
            });
        }
        catch (ImageStorageException exception)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
            {
                Title = "Category image storage is unavailable.",
                Detail = exception.Message,
                Status = StatusCodes.Status503ServiceUnavailable,
                Type = "https://httpstatuses.com/503"
            });
        }
    }
}
