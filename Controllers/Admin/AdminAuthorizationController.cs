using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SRS_backend.Dtos.Admin.Authentication;
using SRS_backend.Security;
using SRS_backend.Services.Authentication;

namespace SRS_backend.Controllers.Admin;

[ApiController]
[AllowAnonymous]
[Produces("application/json", "application/problem+json")]
[Route("api/admin/auth")]
public sealed class AdminAuthorizationController(IAdminAuthenticationService authenticationService) : ControllerBase
{
    [HttpPost("login", Name = "AdminLogin")]
    [EnableRateLimiting(RateLimitPolicies.AdminLogin)]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(AdminLoginResponse), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests, "application/problem+json")]
    public ActionResult<AdminLoginResponse> Login([FromBody] AdminLoginRequest request)
    {
        var response = authenticationService.Authenticate(request);
        if (response is null)
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Authentication failed.",
                Detail = "The supplied username or password is invalid.",
                Type = "https://httpstatuses.com/401"
            });
        }

        return Ok(response);
    }
}
