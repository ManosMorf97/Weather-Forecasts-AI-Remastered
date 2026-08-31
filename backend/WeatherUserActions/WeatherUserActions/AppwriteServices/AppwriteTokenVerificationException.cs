namespace WeatherUserActions.AppwriteServices
{
    // Thrown by IAppwriteAuthService when an Appwrite JWT fails verification.
    // Kept independent of the Appwrite SDK's own exception types so callers
    // (and tests) don't need to depend on SDK internals.
    public class AppwriteTokenVerificationException : Exception
    {
        public AppwriteTokenVerificationException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
