namespace WeatherUserActions.Dtos
{
    // UC8 - internal transfer types used while the background worker generates a queued report.

    // A queued AnalyticsReport row (one service) plus the cities its metrics must cover.
    public record QueuedAnalyticsReport(
        int ReportId,
        Guid BatchId,
        string UserId,
        int ServiceId,
        string ServiceName,
        DateOnly DateRangeStart,
        DateOnly DateRangeEnd,
        List<QueuedAnalyticsReportCity> Cities);

    public record QueuedAnalyticsReportCity(int CityId, string CityName, string Country);

    // A batch whose reports have all finished generating (none still Queued) but which has not yet
    // been emailed to the user. Rebuilt from the stored AnalyticsReportCityMetric rows so the
    // delivery pass can run - and retry - independently of the generation pass.
    public record DeliverableAnalyticsBatch(
        Guid BatchId,
        string UserId,
        List<DeliverableAnalyticsReport> Reports);

    public record DeliverableAnalyticsReport(
        string ServiceName,
        string Status,
        DateOnly DateRangeStart,
        DateOnly DateRangeEnd,
        List<AnalyticsReportCityRow> Cities);

    // One forecast row reduced to the fields the statistics are computed from.
    public record ForecastSample(
        int CityId,
        decimal Temperature,
        decimal Humidity,
        decimal WindSpeed,
        DateTime Timestamp,
        bool DangerFlag);

    // One generated report (one service) handed to the renderer: the service and its per-city rows.
    public record AnalyticsReportSection(
        string ServiceName,
        DateOnly DateRangeStart,
        DateOnly DateRangeEnd,
        List<AnalyticsReportCityRow> Cities);

    public record AnalyticsReportCityRow(string CityName, CityMetricResult Metrics);

    // The computed summary for one city within one report. Averages/spread are null when
    // SampleCount is 0.
    public record CityMetricResult(
        int CityId,
        decimal? AvgTemperature,
        decimal? StdDevTemperature,
        decimal? MinTemperature,
        decimal? MaxTemperature,
        decimal? AvgHumidity,
        decimal? StdDevHumidity,
        decimal? MinHumidity,
        decimal? MaxHumidity,
        decimal? AvgWindSpeed,
        decimal? StdDevWindSpeed,
        decimal? MinWindSpeed,
        decimal? MaxWindSpeed,
        int DangerDayCount,
        int SampleCount);
}
