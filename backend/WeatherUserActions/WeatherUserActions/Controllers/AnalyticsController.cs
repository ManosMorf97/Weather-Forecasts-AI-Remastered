using Microsoft.AspNetCore.Mvc;
using WeatherUserActions.Dtos;
using WeatherUserActions.Services;
using WeatherUserActions.Services.Results;

namespace WeatherUserActions.Controllers
{
    // UC8: Request Analytics. Queues one report per selected service and returns 202 - the finished
    // report (with charts, Stage 2) is emailed to the user.
    [ApiController]
    [Route("api/[controller]")]
    public class AnalyticsController : ControllerBase
    {
        private readonly IAnalyticsService _analyticsService;

        public AnalyticsController(IAnalyticsService analyticsService)
        {
            _analyticsService = analyticsService;
        }

        [HttpPost]
        public async Task<IActionResult> RequestAnalytics(RequestAnalyticsRequest request, CancellationToken cancellationToken)
        {
            if (!this.TryGetBearerToken(out var jwt))
            {
                return Unauthorized();
            }

            var result = await _analyticsService.RequestAnalyticsAsync(jwt, request, cancellationToken);

            return result.Status switch
            {
                RequestAnalyticsStatus.Unauthorized => Unauthorized(),
                RequestAnalyticsStatus.InvalidCities => Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid city selection",
                    detail: "One or more requested cities are not part of your selection."),
                RequestAnalyticsStatus.InvalidServices => Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid service selection",
                    detail: "One or more requested services are not part of your selection."),
                RequestAnalyticsStatus.InvalidDateRange => Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid date range",
                    detail: "Start must be on or before end, and the range must not exceed 366 days."),
                RequestAnalyticsStatus.Failed => Problem(
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "Failed to queue analytics",
                    detail: "Could not queue the analytics report. Please retry."),
                _ => Accepted(new RequestAnalyticsResponse(result.BatchId)),
            };
        }
    }
}
