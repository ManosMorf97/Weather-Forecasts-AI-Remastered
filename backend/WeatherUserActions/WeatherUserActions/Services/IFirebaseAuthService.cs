namespace WeatherUserActions.Services
{
    public interface IFirebaseAuthService
    {
        // Verifies a Firebase ID token against the Authentication Service and returns the user's uid.
        // Throws FirebaseTokenVerificationException if the token is invalid or expired.
        Task<string> VerifyIdTokenAsync(string idToken, CancellationToken cancellationToken = default);
    }
}
