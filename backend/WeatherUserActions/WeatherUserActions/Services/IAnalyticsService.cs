using WeatherUserActions.Dtos;
using WeatherUserActions.Services.Results;

namespace WeatherUserActions.Services
{
    public interface IAnalyticsService
    {
        // UC8: verifies the JWT, validates the request against the user's own selections, then
        // queues one report per selected service. The reports are generated asynchronously.
        Task<RequestAnalyticsResult> RequestAnalyticsAsync(
            string jwt, RequestAnalyticsRequest request, CancellationToken cancellationToken = default);

        // Generates every currently-queued report, stores its per-city numbers, and emails the
        // user one summary per batch. Invoked on a timer by AnalyticsReportWorker.
        Task ProcessQueuedReportsAsync(CancellationToken cancellationToken = default);
    }
}
