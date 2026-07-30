using System.ComponentModel.DataAnnotations.Schema;

namespace WeatherUserActions.Models
{
    // Composite PK: (UserId, CitySiteId)
    public class UserCitySite
    {
        public int UserId { get; set; }
        public User User { get; set; } = null!;

        [ForeignKey(nameof(CitySite))]
        public int CitySiteId { get; set; }
        public CitySite CitySite { get; set; } = null!;

        public DateTime AddedAt { get; set; }
    }
}
