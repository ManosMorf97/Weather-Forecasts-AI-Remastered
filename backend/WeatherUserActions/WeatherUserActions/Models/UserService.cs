using System.ComponentModel.DataAnnotations.Schema;

namespace WeatherUserActions.Models
{
    // Composite PK: (UserId, ServiceId)
    // Pending service picks with no city yet (e.g. City API was down during signup).
    // Cleared once the user's first city materializes these into UserCitySite rows.
    public class UserService
    {
        public int UserId { get; set; }
        public User User { get; set; } = null!;

        [ForeignKey(nameof(Service))]
        public int ServiceId { get; set; }
        public ForecastingService Service { get; set; } = null!;

        public DateTime AddedAt { get; set; }
    }
}
