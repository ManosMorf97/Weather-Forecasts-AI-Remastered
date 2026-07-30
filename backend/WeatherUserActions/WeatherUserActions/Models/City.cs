using System.ComponentModel.DataAnnotations;

namespace WeatherUserActions.Models
{
    public class City
    {
        [Key]
        public int CityId { get; set; }

        public required string Name { get; set; }

        public required string Country { get; set; }

        public decimal Latitude { get; set; }

        public decimal Longitude { get; set; }

        public ICollection<CitySite> CitySites { get; set; } = new List<CitySite>();
    }
}
