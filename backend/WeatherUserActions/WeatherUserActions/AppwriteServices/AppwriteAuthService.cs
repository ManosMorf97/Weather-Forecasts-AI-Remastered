using Appwrite;
using Appwrite.Services;

namespace WeatherUserActions.AppwriteServices
{
    public class AppwriteAuthService : IAppwriteAuthService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _endpoint;
        private readonly string _projectId;

        public AppwriteAuthService(IConfiguration configuration, IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;

            var appwriteSection = configuration.GetSection("Appwrite");
            _endpoint = Environment.GetEnvironmentVariable("YOUR_APPWRITE_ENDPOINT")
                ?? appwriteSection["Endpoint"]
                ?? throw new InvalidOperationException(
                    "Appwrite endpoint is not configured. Set Appwrite:Endpoint " +
                    "(or the YOUR_APPWRITE_ENDPOINT environment variable).");
            _projectId = Environment.GetEnvironmentVariable("YOUR_APPWRITE_PROJECT_ID")
                ?? appwriteSection["ProjectId"]
                ?? throw new InvalidOperationException(
                    "Appwrite project id is not configured. Set Appwrite:ProjectId " +
                    "(or the YOUR_APPWRITE_PROJECT_ID environment variable).");
        }

        // Verification is stateless: any backend holding this project's endpoint + id can
        // exchange a caller's JWT for the user's identity by asking the Authentication Service.
        public async Task<string> VerifyJwtAsync(string jwt, CancellationToken cancellationToken = default)
        {
            // A fresh Client per call - the JWT is per-request and short-lived (~15 min).
            // The HttpClient is pooled via IHttpClientFactory so this stays cheap.
            // The Appwrite SDK has no CancellationToken support, so it is not forwarded.
            var client = new Client(_endpoint, selfSigned: false, _httpClientFactory.CreateClient("appwrite"))
                .SetProject(_projectId)
                .SetJWT(jwt);

            try
            {
                var user = await new Account(client).Get();
                return user.Id;
            }
            catch (AppwriteException ex)
            {
                throw new AppwriteTokenVerificationException("Appwrite JWT verification failed.", ex);
            }
        }
    }
}
