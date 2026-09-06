using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;
using SRS_backend.Dtos.Common;
using SRS_backend.Dtos.Orders;
using SRS_backend.Security;
using SRS_backend.Services.Orders;

namespace SRS_backend.Controllers.Admin;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.Admin)]
[Route("api/admin/orders")]
public sealed class AdminOrdersController(IOrderService orderService) : ControllerBase
{
    [HttpGet(Name = "GetAdminOrders")]
    [EndpointSummary("Get orders.")]
    [EndpointDescription("Returns orders for administration, ordered by newest first. Results can be filtered by customer email, payment status, fulfillment status, and whether an admin has viewed the order.")]
    [ProducesResponseType(typeof(PagedResponse<OrderResponse>), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<ActionResult<PagedResponse<OrderResponse>>> GetOrders(
        [FromQuery] GetOrdersQuery query,
        CancellationToken cancellationToken) =>
        Ok(await orderService.GetAsync(query, cancellationToken));

    [HttpGet("{orderId}", Name = "GetAdminOrder")]
    [EndpointSummary("Get an order.")]
    [EndpointDescription("Returns the complete order snapshot, including customer and shipping details, for administration. Opening an order marks it as viewed.")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<OrderResponse>> GetOrder(
        [FromRoute] string orderId,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await orderService.GetByIdAsync(orderId, cancellationToken));
        }
        catch (OrderValidationException exception)
        {
            return BadRequest(ValidationFailure(exception.Message));
        }
        catch (OrderNotFoundException exception)
        {
            return NotFound(NotFoundProblem(exception.Message));
        }
    }

    [HttpPatch("{orderId}/status", Name = "UpdateOrderStatus")]
    [EndpointSummary("Update an order's payment and fulfillment status.")]
    [EndpointDescription("Updates payment and fulfillment state. Bank-transfer orders cannot move beyond Pending fulfillment until payment is paid. Cancelling an order restores its reserved stock.")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<OrderResponse>> UpdateOrderStatus(
        [FromRoute] string orderId,
        [FromBody] UpdateOrderStatusRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await orderService.UpdateStatusAsync(orderId, request, cancellationToken));
        }
        catch (OrderValidationException exception)
        {
            return BadRequest(ValidationFailure(exception.Message));
        }
        catch (OrderNotFoundException exception)
        {
            return NotFound(NotFoundProblem(exception.Message));
        }
        catch (OrderConflictException exception)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Order status could not be updated.",
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

    private static ProblemDetails NotFoundProblem(string detail) => new()
    {
        Title = "Order not found.",
        Detail = detail,
        Status = StatusCodes.Status404NotFound,
        Type = "https://httpstatuses.com/404"
    };
}
