using Appwrite;
using Appwrite.Services;

namespace WeatherUserActions.AppwriteServices
{
    public class AppwriteUsersService : IAppwriteUsersService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string? _endpoint;
        private readonly string? _projectId;
        private readonly string? _apiKey;

        public AppwriteUsersService(IConfiguration configuration, IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;

            var appwriteSection = configuration.GetSection("Appwrite");
            _endpoint = Environment.GetEnvironmentVariable("YOUR_APPWRITE_ENDPOINT") ?? appwriteSection["Endpoint"];
            _projectId = Environment.GetEnvironmentVariable("YOUR_APPWRITE_PROJECT_ID") ?? appwriteSection["ProjectId"];
            _apiKey = Environment.GetEnvironmentVariable("YOUR_APPWRITE_API_KEY") ?? appwriteSection["ApiKey"];
        }

        public async Task<string> GetEmailAsync(string userId, CancellationToken cancellationToken = default)
        {
            // Missing config surfaces as a lookup failure (caught by the caller), not a startup crash -
            // the analytics worker still runs, it just can't email until Appwrite:ApiKey is set.
            if (string.IsNullOrWhiteSpace(_endpoint) || string.IsNullOrWhiteSpace(_projectId) || string.IsNullOrWhiteSpace(_apiKey))
            {
                throw new AppwriteUserLookupException(
                    "Appwrite is not fully configured for server-side user lookup. Set Appwrite:Endpoint, " +
                    "Appwrite:ProjectId and Appwrite:ApiKey (or the matching YOUR_APPWRITE_* environment variables).",
                    new InvalidOperationException("Missing Appwrite configuration."));
            }

            // The Appwrite SDK has no CancellationToken support, so it is not forwarded.
            var client = new Client(_endpoint, selfSigned: false, _httpClientFactory.CreateClient("appwrite"))
                .SetProject(_projectId)
                .SetKey(_apiKey);

            try
            {
                var user = await new Users(client).Get(userId);
                return user.Email;
            }
            catch (AppwriteException ex)
            {
                throw new AppwriteUserLookupException($"Appwrite user lookup failed for {userId}.", ex);
            }
        }
    }
}
