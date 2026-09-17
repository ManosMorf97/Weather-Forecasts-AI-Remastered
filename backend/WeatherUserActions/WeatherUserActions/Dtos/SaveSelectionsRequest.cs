using System.ComponentModel.DataAnnotations;

namespace WeatherUserActions.Dtos
{
    public record CityDto(
        [Required] string Name,
        [Required] string Country,
        [Range(-90, 90)] decimal Latitude,
        [Range(-180, 180)] decimal Longitude
    );

    // UC3/UC4/UC5: the frontend always sends the user's complete desired selection -
    // the backend replaces the stored selection with exactly this set (not a merge).
    public record SaveSelectionsRequest(
        [MinLength(1)] List<CityDto> Cities,
        [MinLength(1)] List<int> ServiceIds);
}
