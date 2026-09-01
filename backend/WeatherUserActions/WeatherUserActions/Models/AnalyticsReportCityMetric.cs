using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WeatherUserActions.Models
{
    // UC8: the summary statistics for one city within one AnalyticsReport (i.e. one service).
    // Averages/spread are null until the report is generated, or when no forecast data exists
    // for that city+service in the requested date range.
    public class AnalyticsReportCityMetric
    {
        [Key]
        public int AnalyticsReportCityMetricId { get; set; }

        [ForeignKey(nameof(Report))]
        public int ReportId { get; set; }
        public AnalyticsReport Report { get; set; } = null!;

        [ForeignKey(nameof(City))]
        public int CityId { get; set; }
        public City City { get; set; } = null!;

        public decimal? AvgTemperature { get; set; }
        public decimal? StdDevTemperature { get; set; }
        public decimal? MinTemperature { get; set; }
        public decimal? MaxTemperature { get; set; }

        public decimal? AvgHumidity { get; set; }
        public decimal? StdDevHumidity { get; set; }
        public decimal? MinHumidity { get; set; }
        public decimal? MaxHumidity { get; set; }

        public decimal? AvgWindSpeed { get; set; }
        public decimal? StdDevWindSpeed { get; set; }
        public decimal? MinWindSpeed { get; set; }
        public decimal? MaxWindSpeed { get; set; }

        // Distinct calendar dates in range with at least one danger-flagged forecast.
        public int DangerDayCount { get; set; }

        // How many forecast rows the statistics above are based on.
        public int SampleCount { get; set; }
    }
}
