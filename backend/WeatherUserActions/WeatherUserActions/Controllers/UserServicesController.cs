using Microsoft.AspNetCore.Mvc;
using WeatherUserActions.Dtos;
using WeatherUserActions.Services;

namespace WeatherUserActions.Controllers
{
    // UC5 (standalone): replaces the authenticated user's pending (no city yet) service
    // selection with the given set.
    [ApiController]
    [Route("api/[controller]")]
    public class UserServicesController : ControllerBase
    {
        private readonly IUserServicesService _userServicesService;

        public UserServicesController(IUserServicesService userServicesService)
        {
            _userServicesService = userServicesService;
        }

        [HttpPut]
        public async Task<IActionResult> SaveServices(SaveServicesRequest request, CancellationToken cancellationToken)
        {
            if (!this.TryGetBearerToken(out var idToken))
            {
                return Unauthorized();
            }

            var result = await _userServicesService.SaveServicesAsync(idToken, request.ServiceIds, cancellationToken);

            return result.Status switch
            {
                SaveServicesStatus.Unauthorized => Unauthorized(),
                SaveServicesStatus.InvalidServiceIds => Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid service selection",
                    detail: "One or more selected forecasting services do not exist."),
                SaveServicesStatus.Failed => Problem(
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "Failed to save service selection",
                    detail: "Could not persist the pending service selection. Please retry."),
                _ => NoContent(),
            };
        }
    }
}
