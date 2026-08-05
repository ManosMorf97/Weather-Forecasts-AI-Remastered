using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WeatherUserActions.Models
{
    public class AnalyticsReport
    {
        [Key]
        public int ReportId { get; set; }

        [ForeignKey(nameof(User))]
        [MaxLength(128)]
        public required string UserId { get; set; }
        public User User { get; set; } = null!;

        public required string Cities { get; set; }

        public required string Services { get; set; }

        public DateOnly DateRangeStart { get; set; }

        public DateOnly DateRangeEnd { get; set; }

        public required string Metrics { get; set; }

        public required string Format { get; set; }

        public required string Status { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
