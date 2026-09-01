using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WeatherUserActions.Models
{
    // UC8: one report per (request, service). A single POST /api/analytics creates one row per
    // selected service, all sharing a BatchId. The per-city numbers live in CityMetrics.
    public class AnalyticsReport
    {
        [Key]
        public int ReportId { get; set; }

        [ForeignKey(nameof(User))]
        [MaxLength(128)]
        public required string UserId { get; set; }
        public User User { get; set; } = null!;

        // Groups the per-service rows created by one analytics request.
        public Guid BatchId { get; set; }

        [ForeignKey(nameof(Service))]
        public int ServiceId { get; set; }
        public ForecastingService Service { get; set; } = null!;

        public DateOnly DateRangeStart { get; set; }

        public DateOnly DateRangeEnd { get; set; }

        // Delivery format of the generated report (UC9). Currently always "JSON".
        public required string Format { get; set; }

        // One of AnalyticsReportStatus.
        public required string Status { get; set; }

        public DateTime CreatedAt { get; set; }

        // When the batch this report belongs to was emailed to the user (a success or a failure
        // mail). Null until delivered; the worker's delivery pass retries every batch whose reports
        // have all finished generating but whose DeliveredAt is still null.
        public DateTime? DeliveredAt { get; set; }

        public ICollection<AnalyticsReportCityMetric> CityMetrics { get; set; } = new List<AnalyticsReportCityMetric>();
    }
}
