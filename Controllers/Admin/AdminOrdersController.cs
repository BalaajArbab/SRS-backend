using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;
using SRS_backend.Dtos.Common;
using SRS_backend.Dtos.Orders;
using SRS_backend.Security;
using SRS_backend.Services.Email;
using SRS_backend.Services.Orders;

namespace SRS_backend.Controllers.Admin;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.Admin)]
[Route("api/admin/orders")]
public sealed class AdminOrdersController(
    IOrderService orderService,
    IOrderEmailDeliveryService orderEmailDeliveryService) : ControllerBase
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

    [HttpGet("email-notifications/failed", Name = "GetFailedOrderEmailNotifications")]
    [EndpointSummary("Get failed order email notifications.")]
    [EndpointDescription("Returns terminally failed order email notifications for administration. Each notification can be manually resent using its order ID and notification ID.")]
    [ProducesResponseType(typeof(PagedResponse<FailedOrderEmailNotificationResponse>), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<ActionResult<PagedResponse<FailedOrderEmailNotificationResponse>>> GetFailedEmailNotifications(
        [FromQuery] GetFailedOrderEmailNotificationsQuery query,
        CancellationToken cancellationToken) =>
        Ok(await orderEmailDeliveryService.GetFailedAsync(query, cancellationToken));

    [HttpPost("{orderId}/email-notifications/{notificationId}/resend", Name = "ResendFailedOrderEmailNotification")]
    [EndpointSummary("Resend a failed order email notification.")]
    [EndpointDescription("Attempts to resend a terminally failed order email notification to its original recipient. It is marked as sent only after the configured email provider accepts it; otherwise it remains failed for another manual retry.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<IActionResult> ResendFailedEmailNotification(
        [FromRoute] string orderId,
        [FromRoute] string notificationId,
        CancellationToken cancellationToken)
    {
        try
        {
            await orderEmailDeliveryService.RetryFailedAsync(orderId, notificationId, cancellationToken);
            return NoContent();
        }
        catch (OrderEmailNotificationNotFoundException exception)
        {
            return NotFound(NotFoundProblem(exception.Message));
        }
        catch (OrderEmailNotificationConflictException exception)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Order email notification could not be resent.",
                Detail = exception.Message,
                Status = StatusCodes.Status409Conflict,
                Type = "https://httpstatuses.com/409"
            });
        }
        catch (OrderEmailDeliveryException exception)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
            {
                Title = "Email provider unavailable.",
                Detail = exception.Message,
                Status = StatusCodes.Status503ServiceUnavailable,
                Type = "https://httpstatuses.com/503"
            });
        }
    }

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
