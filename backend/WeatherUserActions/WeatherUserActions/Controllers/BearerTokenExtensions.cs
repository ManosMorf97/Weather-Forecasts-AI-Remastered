using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc;

namespace WeatherUserActions.Controllers
{
    internal static class BearerTokenExtensions
    {
        public static bool TryGetBearerToken(this ControllerBase controller, out string jwt)
        {
            jwt = string.Empty;

            if (!controller.Request.Headers.TryGetValue("Authorization", out var authorizationHeader) ||
                !AuthenticationHeaderValue.TryParse(authorizationHeader, out var headerValue) ||
                !string.Equals(headerValue.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(headerValue.Parameter))
            {
                return false;
            }

            jwt = headerValue.Parameter;
            return true;
        }
    }
}
