using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SRS_backend.Dtos.Analytics;
using SRS_backend.Security;
using SRS_backend.Services.Analytics;

namespace SRS_backend.Controllers;

[ApiController]
[AllowAnonymous]
[EnableRateLimiting(RateLimitPolicies.PublicApi)]
[Route("api/analytics")]
public sealed class AnalyticsController(IPageHitService pageHitService) : ControllerBase
{
    [HttpPost("page-hits", Name = "TrackPageHit")]
    [EndpointSummary("Track a public page hit.")]
    [EndpointDescription("Records an anonymous front-page, category-page, or product-page hit. The request records no visitor identifier, IP address, cookie, or personal data.")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests, "application/problem+json")]
    public async Task<IActionResult> TrackPageHit(
        [FromBody] TrackPageHitRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await pageHitService.TrackAsync(request, cancellationToken);
            return NoContent();
        }
        catch (PageHitValidationException exception)
        {
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["pageHit"] = [exception.Message]
            })
            {
                Title = "Page hit validation failed.",
                Status = StatusCodes.Status400BadRequest,
                Type = "https://httpstatuses.com/400"
            });
        }
    }
}
