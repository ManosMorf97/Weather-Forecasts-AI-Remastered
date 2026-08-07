using Microsoft.AspNetCore.Mvc;
using WeatherUserActions.Dtos;
using WeatherUserActions.Services;

namespace WeatherUserActions.Controllers
{
    // UC3/UC4/UC5: replaces the authenticated user's full city+service selection with the given set.
    [ApiController]
    [Route("api/[controller]")]
    public class SelectionsController : ControllerBase
    {
        private readonly ISelectionsService _selectionsService;

        public SelectionsController(ISelectionsService selectionsService)
        {
            _selectionsService = selectionsService;
        }

        [HttpPut]
        public async Task<IActionResult> SaveSelections(SaveSelectionsRequest request, CancellationToken cancellationToken)
        {
            if (!this.TryGetBearerToken(out var idToken))
            {
                return Unauthorized();
            }

            var result = await _selectionsService.SaveSelectionsAsync(
                idToken, request.Cities, request.ServiceIds, cancellationToken);

            return result.Status switch
            {
                SaveSelectionsStatus.Unauthorized => Unauthorized(),
                SaveSelectionsStatus.InvalidServiceIds => Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid service selection",
                    detail: "One or more selected forecasting services do not exist."),
                SaveSelectionsStatus.Failed => Problem(
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "Failed to save selections",
                    detail: "Could not persist the city/service selection. Please retry."),
                _ => NoContent(),
            };
        }
    }
}
