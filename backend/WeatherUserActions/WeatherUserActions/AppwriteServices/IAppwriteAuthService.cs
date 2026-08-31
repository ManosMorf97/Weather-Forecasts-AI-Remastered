namespace WeatherUserActions.AppwriteServices
{
    public interface IAppwriteAuthService
    {
        // Verifies an Appwrite JWT against the Authentication Service and returns the user's id ($id).
        // Throws AppwriteTokenVerificationException if the token is invalid or expired.
        Task<string> VerifyJwtAsync(string jwt, CancellationToken cancellationToken = default);
    }
}
