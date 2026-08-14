using Microsoft.AspNetCore.Mvc;
using WeatherUserActions.Dtos;
using WeatherUserActions.Services;
using WeatherUserActions.Services.Results;

namespace WeatherUserActions.Controllers
{
    // UC6 (main flow): returns forecasts for the authenticated user's saved cities and selected
    // services - not the A1 manual-search-by-city flow.
    [ApiController]
    [Route("api/[controller]")]
    public class ForecastsController : ControllerBase
    {
        private readonly IForecastsService _forecastsService;
        private readonly IRatingsService _ratingsService;

        public ForecastsController(IForecastsService forecastsService, IRatingsService ratingsService)
        {
            _forecastsService = forecastsService;
            _ratingsService = ratingsService;
        }

        [HttpGet]
        public async Task<ActionResult<GetForecastsResponse>> GetForecasts(CancellationToken cancellationToken)
        {
            if (!this.TryGetBearerToken(out var idToken))
            {
                return Unauthorized();
            }

            var result = await _forecastsService.GetForecastsAsync(idToken, cancellationToken);

            return result.Status switch
            {
                GetForecastsStatus.Unauthorized => Unauthorized(),
                GetForecastsStatus.Failed => Problem(
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "Failed to load forecasts",
                    detail: "Could not load forecasts for your selections. Please retry."),
                _ => Ok(new GetForecastsResponse(result.Forecasts!)),
            };
        }

        // UC7 (main flow / A1): creates or updates the authenticated user's rating for this forecast.
        [HttpPut("{forecastId}/rating")]
        public async Task<IActionResult> RateForecast(int forecastId, RateForecastRequest request, CancellationToken cancellationToken)
        {
            if (!this.TryGetBearerToken(out var idToken))
            {
                return Unauthorized();
            }

            var result = await _ratingsService.RateForecastAsync(idToken, forecastId, request.Value, cancellationToken);

            return result.Status switch
            {
                RateForecastStatus.Unauthorized => Unauthorized(),
                RateForecastStatus.ForecastNotFound => Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Forecast not found",
                    detail: "No forecast exists with the given id."),
                RateForecastStatus.Failed => Problem(
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "Failed to save rating",
                    detail: "Could not save your rating. Please retry."),
                _ => Ok(),
            };
        }

        // UC7 A2: removes the authenticated user's rating for this forecast, if any (idempotent).
        [HttpDelete("{forecastId}/rating")]
        public async Task<IActionResult> RemoveRating(int forecastId, CancellationToken cancellationToken)
        {
            if (!this.TryGetBearerToken(out var idToken))
            {
                return Unauthorized();
            }

            var result = await _ratingsService.RemoveRatingAsync(idToken, forecastId, cancellationToken);

            return result.Status switch
            {
                RemoveRatingStatus.Unauthorized => Unauthorized(),
                RemoveRatingStatus.Failed => Problem(
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "Failed to remove rating",
                    detail: "Could not remove your rating. Please retry."),
                _ => Ok(),
            };
        }
    }
}
