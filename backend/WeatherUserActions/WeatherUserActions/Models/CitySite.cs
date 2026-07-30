using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WeatherUserActions.Models
{
    // unique (CityId, ServiceId)
    public class CitySite
    {
        [Key]
        public int CitySiteId { get; set; }

        [ForeignKey(nameof(City))]
        public int CityId { get; set; }
        public City City { get; set; } = null!;

        [ForeignKey(nameof(Service))]
        public int ServiceId { get; set; }
        public ForecastingService Service { get; set; } = null!;

        public ICollection<UserCitySite> UserCitySites { get; set; } = new List<UserCitySite>();
        public ICollection<Forecast> Forecasts { get; set; } = new List<Forecast>();
    }
}
