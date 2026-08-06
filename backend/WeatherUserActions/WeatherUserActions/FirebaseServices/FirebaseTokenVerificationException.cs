namespace WeatherUserActions.FirebaseServices
{
    // Thrown by IFirebaseAuthService when a Firebase ID token fails verification.
    // Kept independent of the FirebaseAdmin SDK's own exception types so callers
    // (and tests) don't need to depend on SDK internals.
    public class FirebaseTokenVerificationException : Exception
    {
        public FirebaseTokenVerificationException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
