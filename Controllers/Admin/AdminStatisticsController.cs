using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;
using SRS_backend.Dtos.Common;
using SRS_backend.Dtos.Statistics;
using SRS_backend.Security;
using SRS_backend.Services.Statistics;

namespace SRS_backend.Controllers.Admin;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.Admin)]
[Route("api/admin/stats")]
public sealed class AdminStatisticsController(IStatisticsService statisticsService) : ControllerBase
{
    [HttpGet("categories/inventory-value", Name = "GetCategoryInventoryValues")]
    [EndpointSummary("Get current category inventory values.")]
    [EndpointDescription("Returns each category's current inventory value as the sum of each primary-category product's stock quantity multiplied by its regular price. A product's first category assignment is its primary category.")]
    [ProducesResponseType(typeof(PagedResponse<CategoryInventoryValueResponse>), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<ActionResult<PagedResponse<CategoryInventoryValueResponse>>> GetCategoryInventoryValues(
        [FromQuery] GetCategoryInventoryValuesQuery query,
        CancellationToken cancellationToken) =>
        Ok(await statisticsService.GetCategoryInventoryValuesAsync(query, cancellationToken));

    [HttpGet("categories/sales", Name = "GetCategorySales")]
    [EndpointSummary("Get category sales.")]
    [EndpointDescription("Returns category sales for the inclusive Pakistan calendar-date range. Sales are calculated from historical order-item primary-category snapshots and exclude cancelled orders. A product's first category assignment is its primary category.")]
    [ProducesResponseType(typeof(PagedResponse<CategorySalesResponse>), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<ActionResult<PagedResponse<CategorySalesResponse>>> GetCategorySales(
        [FromQuery(Name = "fromDate"), Required] DateOnly? fromDate,
        [FromQuery(Name = "toDate"), Required] DateOnly? toDate,
        [FromQuery] GetCategorySalesQuery query,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await statisticsService.GetCategorySalesAsync(query, fromDate, toDate, cancellationToken));
        }
        catch (StatisticsValidationException exception)
        {
            return BadRequest(ValidationFailure(exception.Message));
        }
    }

    [HttpGet("categories/sales/all", Name = "GetAllCategorySales")]
    [EndpointSummary("Get all-time category sales.")]
    [EndpointDescription("Returns all-time category sales without a date filter. Sales are calculated from historical order-item primary-category snapshots and exclude cancelled orders. A product's first category assignment is its primary category.")]
    [ProducesResponseType(typeof(PagedResponse<CategorySalesResponse>), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<ActionResult<PagedResponse<CategorySalesResponse>>> GetAllCategorySales(
        [FromQuery] GetAllCategorySalesQuery query,
        CancellationToken cancellationToken) =>
        Ok(await statisticsService.GetAllCategorySalesAsync(query, cancellationToken));

    [HttpGet("orders/daily", Name = "GetDailyOrderStatistics")]
    [EndpointSummary("Get daily order statistics.")]
    [EndpointDescription("Returns one Pakistan calendar-day bucket for every day in the inclusive Pakistan date range, including days with zero orders. Each bucket includes sales grouped by the historical primary-category snapshot for stacked category charts. Order count and sales exclude cancelled orders. The range cannot exceed 366 days.")]
    [ProducesResponseType(typeof(IReadOnlyList<DailyOrderStatisticsResponse>), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<ActionResult<IReadOnlyList<DailyOrderStatisticsResponse>>> GetDailyOrderStatistics(
        [FromQuery(Name = "fromDate"), Required] DateOnly? fromDate,
        [FromQuery(Name = "toDate"), Required] DateOnly? toDate,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await statisticsService.GetDailyOrderStatisticsAsync(fromDate, toDate, cancellationToken));
        }
        catch (StatisticsValidationException exception)
        {
            return BadRequest(ValidationFailure(exception.Message));
        }
    }

    [HttpGet("orders/daily/fulfillment-status", Name = "GetDailyFulfillmentStatusStatistics")]
    [EndpointSummary("Get daily fulfillment-status statistics.")]
    [EndpointDescription("Returns one Pakistan calendar-day bucket for every day in the inclusive Pakistan date range, including days with zero orders. Each bucket includes order count and sales grouped by fulfillment status for stacked status charts. Cancelled orders appear in the Cancelled segment with zero sales. The range cannot exceed 366 days.")]
    [ProducesResponseType(typeof(IReadOnlyList<DailyFulfillmentStatusStatisticsResponse>), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<ActionResult<IReadOnlyList<DailyFulfillmentStatusStatisticsResponse>>> GetDailyFulfillmentStatusStatistics(
        [FromQuery(Name = "fromDate"), Required] DateOnly? fromDate,
        [FromQuery(Name = "toDate"), Required] DateOnly? toDate,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await statisticsService.GetDailyFulfillmentStatusStatisticsAsync(fromDate, toDate, cancellationToken));
        }
        catch (StatisticsValidationException exception)
        {
            return BadRequest(ValidationFailure(exception.Message));
        }
    }

    private static ValidationProblemDetails ValidationFailure(string detail) => new(
        new Dictionary<string, string[]> { ["statistics"] = [detail] })
    {
        Title = "Statistics validation failed.",
        Status = StatusCodes.Status400BadRequest,
        Type = "https://httpstatuses.com/400"
    };
}
