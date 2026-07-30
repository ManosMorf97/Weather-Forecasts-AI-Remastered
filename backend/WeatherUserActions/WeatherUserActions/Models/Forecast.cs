using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WeatherUserActions.Models
{
    // unique (CitySiteId, Timestamp, Type)
    public class Forecast
    {
        [Key]
        public int ForecastId { get; set; }

        [ForeignKey(nameof(CitySite))]
        public int CitySiteId { get; set; }
        public CitySite CitySite { get; set; } = null!;

        public DateTime Timestamp { get; set; }

        public required string Type { get; set; }

        public decimal Temperature { get; set; }

        public decimal Humidity { get; set; }

        public decimal WindSpeed { get; set; }

        public bool DangerFlag { get; set; }

        public DateTime RetrievedAt { get; set; }

        public ICollection<Rating> Ratings { get; set; } = new List<Rating>();
        public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    }
}
