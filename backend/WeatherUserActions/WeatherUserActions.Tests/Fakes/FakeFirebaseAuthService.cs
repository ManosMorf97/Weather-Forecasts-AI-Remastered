using WeatherUserActions.Services;

namespace WeatherUserActions.Tests.Fakes
{
    public class FakeFirebaseAuthService : IFirebaseAuthService
    {
        private readonly string? _uid;

        private FakeFirebaseAuthService(string? uid)
        {
            _uid = uid;
        }

        public static FakeFirebaseAuthService ReturningUid(string uid) => new(uid);

        public static FakeFirebaseAuthService RejectingToken() => new(null);

        public Task<string> VerifyIdTokenAsync(string idToken, CancellationToken cancellationToken = default)
        {
            if (_uid is null)
            {
                throw new FirebaseTokenVerificationException("Token rejected by fake auth service.", new InvalidOperationException());
            }

            return Task.FromResult(_uid);
        }
    }
}
