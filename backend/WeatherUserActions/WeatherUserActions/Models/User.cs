using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WeatherUserActions.Models
{
    // No username/email/password stored here - all account data lives in the
    // Auth Service (Appwrite Authentication), keyed by this same UserId (Appwrite user id / $id).
    // Row is created lazily (JIT) on first login.
    public class User
    {
        // Appwrite Authentication user id / $id (not an internal surrogate id).
        [Key]
        [MaxLength(128)]
        public required string UserId { get; set; }

        public DateTime CreatedAt { get; set; }

        public ICollection<UserCitySite> UserCitySites { get; set; } = new List<UserCitySite>();
        public ICollection<UserService> UserServices { get; set; } = new List<UserService>();
        public ICollection<Rating> Ratings { get; set; } = new List<Rating>();
        public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
        public ICollection<AnalyticsReportBatch> AnalyticsReportBatches { get; set; } = new List<AnalyticsReportBatch>();
    }
}
