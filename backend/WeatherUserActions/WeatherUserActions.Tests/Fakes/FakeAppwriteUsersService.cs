using WeatherUserActions.AppwriteServices;

namespace WeatherUserActions.Tests.Fakes
{
    public class FakeAppwriteUsersService : IAppwriteUsersService
    {
        private readonly string? _email;

        private FakeAppwriteUsersService(string? email)
        {
            _email = email;
        }

        public static FakeAppwriteUsersService ReturningEmail(string email) => new(email);

        public static FakeAppwriteUsersService FailingLookup() => new(null);

        public Task<string> GetEmailAsync(string userId, CancellationToken cancellationToken = default)
        {
            if (_email is null)
            {
                throw new AppwriteUserLookupException("Lookup rejected by fake users service.", new InvalidOperationException());
            }

            return Task.FromResult(_email);
        }
    }
}
