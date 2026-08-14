using System.ComponentModel.DataAnnotations;

namespace WeatherUserActions.Dtos
{
    // UC7: create-or-update the requesting user's rating for a specific forecast.
    public record RateForecastRequest([property: Range(1, 5)] int Value);
}
