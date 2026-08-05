using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WeatherUserActions.Models
{
    // No username/email/password stored here - all account data lives in the
    // Auth Service (Firebase Authentication), keyed by this same UserId (Firebase uid).
    // Row is created lazily (JIT) on first login.
    public class User
    {
        // Firebase Authentication uid (not an internal surrogate id).
        [Key]
        [MaxLength(128)]
        public required string UserId { get; set; }

        public DateTime CreatedAt { get; set; }

        public ICollection<UserCitySite> UserCitySites { get; set; } = new List<UserCitySite>();
        public ICollection<UserService> UserServices { get; set; } = new List<UserService>();
        public ICollection<Rating> Ratings { get; set; } = new List<Rating>();
        public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
        public ICollection<AnalyticsReport> AnalyticsReports { get; set; } = new List<AnalyticsReport>();
    }
}
