using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WeatherUserActions.Models
{
    public class Notification
    {
        [Key]
        public int NotificationId { get; set; }

        [ForeignKey(nameof(User))]
        public int UserId { get; set; }
        public User User { get; set; } = null!;

        [ForeignKey(nameof(Forecast))]
        public int ForecastId { get; set; }
        public Forecast Forecast { get; set; } = null!;

        public required string Channel { get; set; }

        public required string Status { get; set; }

        public DateTime SentAt { get; set; }
    }
}
