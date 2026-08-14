using WeatherUserActions.Services.Results;

namespace WeatherUserActions.Services
{
    public interface IProfileService
    {
        // Verifies the ID token, JIT-provisions the user, and checks their city site selection.
        Task<ProfileCreationResult> CreateProfileAsync(string idToken, CancellationToken cancellationToken = default);
    }
}
