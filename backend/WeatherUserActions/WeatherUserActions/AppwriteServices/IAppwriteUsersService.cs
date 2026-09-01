namespace WeatherUserActions.AppwriteServices
{
    public interface IAppwriteUsersService
    {
        // Looks up a user's email via the Appwrite server API (an API key, not a caller JWT).
        // Email is never stored locally - the Authentication Service is the only source.
        // Throws AppwriteUserLookupException if the lookup fails.
        Task<string> GetEmailAsync(string userId, CancellationToken cancellationToken = default);
    }
}
