using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WeatherUserActions.Models
{
    // unique (UserId, ForecastId)
    public class Rating
    {
        [Key]
        public int RatingId { get; set; }

        [ForeignKey(nameof(User))]
        [MaxLength(128)]
        public required string UserId { get; set; }
        public User User { get; set; } = null!;

        [ForeignKey(nameof(Forecast))]
        public int ForecastId { get; set; }
        public Forecast Forecast { get; set; } = null!;

        public int Value { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }
    }
}
