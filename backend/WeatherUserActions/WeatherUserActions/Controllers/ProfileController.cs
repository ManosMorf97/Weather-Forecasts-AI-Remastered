using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WeatherUserActions.Data;
using WeatherUserActions.Dtos;
using WeatherUserActions.Models;
using WeatherUserActions.Services;

namespace WeatherUserActions.Controllers
{
    // UC2: Create Profile - JIT-provisions the authenticated user's profile row
    // and reports whether they have at least one CitySite selection.
    [ApiController]
    [Route("api/[controller]")]
    public class ProfileController : ControllerBase
    {
        private readonly WeatherUserActionsDbContext _db;
        private readonly IFirebaseAuthService _firebaseAuthService;
        private readonly ILogger<ProfileController> _logger;

        public ProfileController(
            WeatherUserActionsDbContext db,
            IFirebaseAuthService firebaseAuthService,
            ILogger<ProfileController> logger)
        {
            _db = db;
            _firebaseAuthService = firebaseAuthService;
            _logger = logger;
        }

        [HttpPost]
        public async Task<ActionResult<CreateProfileResponse>> CreateProfile(CancellationToken cancellationToken)
        {
            if (!TryGetBearerToken(out var idToken))
            {
                return Unauthorized();
            }

            string userId;
            try
            {
                userId = await _firebaseAuthService.VerifyIdTokenAsync(idToken, cancellationToken);
            }
            catch (FirebaseTokenVerificationException ex)
            {
                _logger.LogWarning(ex, "Firebase ID token verification failed");
                return Unauthorized();
            }

            if (!await EnsureUserProvisionedAsync(userId, cancellationToken))
            {
                return Problem(
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "Failed to create profile",
                    detail: "Could not persist the user profile. Please retry.");
            }

            var hasCitySiteSelection = await _db.UserCitySites
                .AnyAsync(userCitySite => userCitySite.UserId == userId, cancellationToken);

            return Ok(new CreateProfileResponse(hasCitySiteSelection));
        }

        private bool TryGetBearerToken(out string idToken)
        {
            idToken = string.Empty;

            if (!Request.Headers.TryGetValue("Authorization", out var authorizationHeader) ||
                !AuthenticationHeaderValue.TryParse(authorizationHeader, out var headerValue) ||
                !string.Equals(headerValue.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(headerValue.Parameter))
            {
                return false;
            }

            idToken = headerValue.Parameter;
            return true;
        }

        // Idempotent upsert: insert if absent, no-op if already present.
        private async Task<bool> EnsureUserProvisionedAsync(string userId, CancellationToken cancellationToken)
        {
            if (await _db.Users.FindAsync([userId], cancellationToken) is not null)
            {
                return true;
            }

            _db.Users.Add(new User { UserId = userId, CreatedAt = DateTime.UtcNow });

            try
            {
                await _db.SaveChangesAsync(cancellationToken);
                return true;
            }
            catch (DbUpdateException ex)
            {
                // A concurrent request may have already inserted the same user; that's success too.
                _db.ChangeTracker.Clear();
                if (await _db.Users.FindAsync([userId], cancellationToken) is not null)
                {
                    return true;
                }

                _logger.LogError(ex, "Failed to upsert user profile for {UserId}", userId);
                return false;
            }
        }
    }
}
