-- Seeds the ForecastingServices table with the 4 providers PredictionUpdater knows how to poll
-- (see backend/PredictionUpdater/src/providers/registry.ts). Name must match each provider's
-- `serviceName` exactly - registry.ts maps ForecastingServices.Name -> adapter, so a mismatched
-- or missing name means that service's (city, service) selections are silently skipped, not an error.
--
-- Safe to re-run: each insert is guarded so existing rows are left untouched.

SET NOCOUNT ON;

IF NOT EXISTS (SELECT 1 FROM ForecastingServices WHERE Name = N'Open-Meteo')
    INSERT INTO ForecastingServices (Name, ApiEndpoint)
    VALUES (N'Open-Meteo', N'https://api.open-meteo.com/v1/forecast');

IF NOT EXISTS (SELECT 1 FROM ForecastingServices WHERE Name = N'OpenWeatherMap')
    INSERT INTO ForecastingServices (Name, ApiEndpoint)
    VALUES (N'OpenWeatherMap', N'https://api.openweathermap.org/data/2.5/forecast');

IF NOT EXISTS (SELECT 1 FROM ForecastingServices WHERE Name = N'WeatherAPI')
    INSERT INTO ForecastingServices (Name, ApiEndpoint)
    VALUES (N'WeatherAPI', N'https://api.weatherapi.com/v1/forecast.json');

IF NOT EXISTS (SELECT 1 FROM ForecastingServices WHERE Name = N'Visual Crossing')
    INSERT INTO ForecastingServices (Name, ApiEndpoint)
    VALUES (N'Visual Crossing', N'https://weather.visualcrossing.com/VisualCrossing/rest/services/timeline');
