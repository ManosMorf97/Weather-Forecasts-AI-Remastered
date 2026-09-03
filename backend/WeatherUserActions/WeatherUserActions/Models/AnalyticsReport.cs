using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WeatherUserActions.Models
{
    // UC8: one report per (batch, service). A single POST /api/analytics creates one
    // AnalyticsReportBatch with one of these rows per selected service. The per-city numbers live
    // in CityMetrics; the request-level fields (user, date range, delivery state) live on the batch.
    public class AnalyticsReport
    {
        [Key]
        public int ReportId { get; set; }

        [ForeignKey(nameof(Batch))]
        public Guid BatchId { get; set; }
        public AnalyticsReportBatch Batch { get; set; } = null!;

        [ForeignKey(nameof(Service))]
        public int ServiceId { get; set; }
        public ForecastingService Service { get; set; } = null!;

        // One of AnalyticsReportStatus.
        public required string Status { get; set; }

        public ICollection<AnalyticsReportCityMetric> CityMetrics { get; set; } = new List<AnalyticsReportCityMetric>();
    }
}
