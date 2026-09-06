using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SRS_backend.Dtos.Orders;
using SRS_backend.Security;
using SRS_backend.Services.Orders;

namespace SRS_backend.Controllers;

[ApiController]
[AllowAnonymous]
[EnableRateLimiting(RateLimitPolicies.Checkout)]
[Route("api/orders")]
public sealed class OrdersController(IOrderService orderService) : ControllerBase
{
    [HttpPost(Name = "CreateOrder")]
    [EndpointSummary("Create an order.")]
    [EndpointDescription("Creates a cash-on-delivery or bank-transfer order. Prices, totals, product availability, and stock are calculated by the server. An Idempotency-Key header is required to safely retry checkout.")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status201Created, "application/json")]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests, "application/problem+json")]
    public async Task<ActionResult<OrderResponse>> CreateOrder(
        [FromBody] CreateOrderRequest request,
        [FromHeader(Name = "Idempotency-Key")]
        [Required(AllowEmptyStrings = false)] string idempotencyKey,
        CancellationToken cancellationToken)
    {
        try
        {
            var order = await orderService.CreateAsync(request, idempotencyKey, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, order);
        }
        catch (OrderValidationException exception)
        {
            return BadRequest(ValidationFailure(exception.Message));
        }
        catch (OrderConflictException exception)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Order could not be created.",
                Detail = exception.Message,
                Status = StatusCodes.Status409Conflict,
                Type = "https://httpstatuses.com/409"
            });
        }
    }

    private static ValidationProblemDetails ValidationFailure(string detail) => new(
        new Dictionary<string, string[]> { ["order"] = [detail] })
    {
        Title = "Order validation failed.",
        Status = StatusCodes.Status400BadRequest,
        Type = "https://httpstatuses.com/400"
    };
}
