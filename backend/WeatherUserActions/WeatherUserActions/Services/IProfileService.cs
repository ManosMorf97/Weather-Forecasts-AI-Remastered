using WeatherUserActions.Services.Results;

namespace WeatherUserActions.Services
{
    public interface IProfileService
    {
        // Verifies the JWT, JIT-provisions the user, and checks their city site selection.
        Task<ProfileCreationResult> CreateProfileAsync(string jwt, CancellationToken cancellationToken = default);
    }
}
