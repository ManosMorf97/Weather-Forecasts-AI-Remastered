using FirebaseAdmin;
using FirebaseAdmin.Auth;
using Google.Apis.Auth.OAuth2;

namespace WeatherUserActions.FirebaseServices
{
    public class FirebaseAuthService : IFirebaseAuthService
    {
        private readonly Lazy<FirebaseApp> _firebaseapp;

        public FirebaseAuthService(IConfiguration configuration)
        {
            //it creates the firebaseapp via our credentials
            _firebaseapp = new Lazy<FirebaseApp>(() => getOrCreateFirebaseApp(configuration));
        }

        public async Task<string> VerifyIdTokenAsync(string idToken, CancellationToken cancellationToken = default)
        {
            try
            {
                var decodedToken = await FirebaseAuth.GetAuth(_firebaseapp.Value)
                    .VerifyIdTokenAsync(idToken, cancellationToken);
                return decodedToken.Uid;
            }
            catch (FirebaseAuthException ex)
            {
                throw new FirebaseTokenVerificationException("Firebase ID token verification failed.", ex);
            }
        }

        // Any backend that holds this same Firebase project's service-account credentials
        // (this .NET service, a Node.js service via firebase-admin, etc.) can independently
        // verify tokens issued by the same Authentication Service - verification is stateless.
        private static FirebaseApp getOrCreateFirebaseApp(IConfiguration configuration)
        {
            //explain. How DefaultInstance is initialized(static variable)
            if (FirebaseApp.DefaultInstance is not null)
            {
                return FirebaseApp.DefaultInstance;
            }
            // explain. GetSection
            var firebaseSection = configuration.GetSection("Firebase");
            var credentialsPath = Environment.GetEnvironmentVariable("YOUR_FIREBASE_CREDENTIALS_PATH")
                ?? firebaseSection["CredentialsPath"];
            var projectId = Environment.GetEnvironmentVariable("YOUR_FIREBASE_PROJECT_ID")
                ?? firebaseSection["ProjectId"];

            if (string.IsNullOrWhiteSpace(credentialsPath))
            {
                throw new InvalidOperationException(
                    "Firebase credentials are not configured. Set Firebase:CredentialsPath " +
                    "(or the YOUR_FIREBASE_CREDENTIALS_PATH environment variable) to a service-account JSON file.");
            }

            return FirebaseApp.Create(new AppOptions
            {
                Credential = CredentialFactory.FromFile(credentialsPath, JsonCredentialParameters.ServiceAccountCredentialType),
                ProjectId = string.IsNullOrWhiteSpace(projectId) ? null : projectId,
            });
        }
    }
}
