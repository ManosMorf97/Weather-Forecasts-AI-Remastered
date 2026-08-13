using System.ComponentModel.DataAnnotations;

namespace WeatherUserActions.Dtos
{
    // UC5 (standalone, no city yet): the frontend sends the user's complete desired set of
    // pending service picks - the backend replaces the stored set with exactly this (not a merge).
    public record SaveServicesRequest(
        [property: MinLength(1)] List<int> ServiceIds);
}
