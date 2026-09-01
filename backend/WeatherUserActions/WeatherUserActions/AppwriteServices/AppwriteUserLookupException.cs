namespace WeatherUserActions.AppwriteServices
{
    // Thrown by IAppwriteUsersService when a server-side user lookup fails. Kept independent of the
    // Appwrite SDK's own exception types so callers (and tests) don't depend on SDK internals.
    public class AppwriteUserLookupException : Exception
    {
        public AppwriteUserLookupException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
