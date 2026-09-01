namespace WeatherUserActions.Dtos
{
    // UC8: analytics generation is asynchronous - the response only acknowledges the queued
    // request. The finished report is emailed to the user (Stage 2 attaches the charts).
    public record RequestAnalyticsResponse(Guid BatchId);
}
