using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc;
using WeatherUserActions.Dtos;
using WeatherUserActions.Services;

namespace WeatherUserActions.Controllers
{
    // UC2: Create Profile - JIT-provisions the authenticated user's profile row
    // and reports whether they have at least one CitySite selection.
    [ApiController]
    [Route("api/[controller]")]
    public class ProfileController : ControllerBase
    {
        private readonly IProfileService _profileService;

        public ProfileController(IProfileService profileService)
        {
            _profileService = profileService;
        }

        [HttpPost]
        public async Task<ActionResult<CreateProfileResponse>> CreateProfile(CancellationToken cancellationToken)
        {
            if (!TryGetBearerToken(out var idToken))
            {
                return Unauthorized();
            }

            var result = await _profileService.CreateProfileAsync(idToken, cancellationToken);

            return result.Status switch
            {
                ProfileCreationStatus.Unauthorized => Unauthorized(),
                ProfileCreationStatus.ProvisioningFailed => Problem(
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "Failed to create profile",
                    detail: "Could not persist the user profile. Please retry."),
                ProfileCreationStatus.SelectionCheckFailed => Problem(
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "Failed to load profile",
                    detail: "Profile was created but city site selection could not be checked. Please retry."),
                _ => Ok(new CreateProfileResponse(result.HasCitySiteSelection)),
            };
        }

        private bool TryGetBearerToken(out string idToken)
        {
            idToken = string.Empty;

            if (!Request.Headers.TryGetValue("Authorization", out var authorizationHeader) ||
                !AuthenticationHeaderValue.TryParse(authorizationHeader, out var headerValue) ||
                !string.Equals(headerValue.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(headerValue.Parameter))
            {
                return false;
            }

            idToken = headerValue.Parameter;
            return true;
        }
    }
}
