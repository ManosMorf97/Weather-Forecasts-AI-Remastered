using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WeatherUserActions.Models
{
    // UC8: one row per POST /api/analytics request. Holds everything that is the same for every
    // service in the request - who asked, the date range, the delivery format and the delivery
    // state. The per-service work lives in the child AnalyticsReport rows.
    public class AnalyticsReportBatch
    {
        [Key]
        public Guid BatchId { get; set; }

        [ForeignKey(nameof(User))]
        [MaxLength(128)]
        public required string UserId { get; set; }
        public User User { get; set; } = null!;

        public DateOnly DateRangeStart { get; set; }

        public DateOnly DateRangeEnd { get; set; }

        // Delivery format of the generated report (UC9). Currently always "JSON".
        public required string Format { get; set; }

        public DateTime CreatedAt { get; set; }

        // When this batch was emailed to the user (a success or a failure mail). Null until
        // delivered; the worker's delivery pass retries every batch whose reports have all finished
        // generating but whose DeliveredAt is still null.
        public DateTime? DeliveredAt { get; set; }

        public ICollection<AnalyticsReport> Reports { get; set; } = new List<AnalyticsReport>();
    }
}
