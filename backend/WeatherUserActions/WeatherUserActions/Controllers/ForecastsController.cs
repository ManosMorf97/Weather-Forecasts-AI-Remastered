using Microsoft.AspNetCore.Mvc;
using WeatherUserActions.Dtos;
using WeatherUserActions.Services;

namespace WeatherUserActions.Controllers
{
    // UC6 (main flow): returns forecasts for the authenticated user's saved cities and selected
    // services - not the A1 manual-search-by-city flow.
    [ApiController]
    [Route("api/[controller]")]
    public class ForecastsController : ControllerBase
    {
        private readonly IForecastsService _forecastsService;

        public ForecastsController(IForecastsService forecastsService)
        {
            _forecastsService = forecastsService;
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
    }
}
