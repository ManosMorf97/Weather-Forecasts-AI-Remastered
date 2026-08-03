using System.ComponentModel.DataAnnotations;

namespace WeatherUserActions.Models
{
    public class City
    {
        [Key]
        public int CityId { get; set; }

        public required string Name { get; set; }

        public required string Country { get; set; }

        [Range(-90, 90)]
        public decimal Latitude { get; set; }

        [Range(-180, 180)]
        public decimal Longitude { get; set; }

        public ICollection<CitySite> CitySites { get; set; } = new List<CitySite>();
    }
}
