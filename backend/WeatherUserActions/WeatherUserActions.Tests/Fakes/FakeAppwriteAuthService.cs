using WeatherUserActions.AppwriteServices;

namespace WeatherUserActions.Tests.Fakes
{
    public class FakeAppwriteAuthService : IAppwriteAuthService
    {
        private readonly string? _uid;

        private FakeAppwriteAuthService(string? uid)
        {
            _uid = uid;
        }

        public static FakeAppwriteAuthService ReturningUid(string uid) => new(uid);

        public static FakeAppwriteAuthService RejectingToken() => new(null);

        public Task<string> VerifyJwtAsync(string jwt, CancellationToken cancellationToken = default)
        {
            if (_uid is null)
            {
                throw new AppwriteTokenVerificationException("Token rejected by fake auth service.", new InvalidOperationException());
            }

            return Task.FromResult(_uid);
        }
    }
}
