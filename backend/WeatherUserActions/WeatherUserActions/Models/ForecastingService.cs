using System.ComponentModel.DataAnnotations;

namespace WeatherUserActions.Models
{
    public class ForecastingService
    {
        [Key]
        public int ServiceId { get; set; }

        public required string Name { get; set; }

        public required string ApiEndpoint { get; set; }

        public ICollection<CitySite> CitySites { get; set; } = new List<CitySite>();
        public ICollection<UserService> UserServices { get; set; } = new List<UserService>();
    }
}
