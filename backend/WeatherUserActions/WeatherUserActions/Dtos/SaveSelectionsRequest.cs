using System.ComponentModel.DataAnnotations;

namespace WeatherUserActions.Dtos
{
    public record CityDto(
        [property: Required] string Name,
        [property: Required] string Country,
        [property: Range(-90, 90)] decimal Latitude,
        [property: Range(-180, 180)] decimal Longitude
    );

    // UC3/UC4/UC5: the frontend always sends the user's complete desired selection -
    // the backend replaces the stored selection with exactly this set (not a merge).
    public record SaveSelectionsRequest(
        [property: MinLength(1)] List<CityDto> Cities,
        [property: MinLength(1)] List<int> ServiceIds);
}
