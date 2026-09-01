using WeatherUserActions.Services;

namespace WeatherUserActions.BackgroundServices
{
    // UC8: generates queued analytics reports on a timer. Polling (rather than an in-memory queue)
    // so a restart still picks up anything left Queued. AnalyticsService is scoped, so each tick
    // resolves it from a fresh scope.
    //
    // Assumes a single running instance: a Queued report is not claimed/locked while it is being
    // processed, so two instances would double-generate and double-send. Before scaling out, add a
    // row-level claim (e.g. a Processing status plus an owner + timestamp).
    public class AnalyticsReportWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<AnalyticsReportWorker> _logger;
        private readonly TimeSpan _pollInterval;

        public AnalyticsReportWorker(
            IServiceScopeFactory scopeFactory, IConfiguration configuration, ILogger<AnalyticsReportWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;

            var pollSeconds = configuration.GetValue<int?>("Analytics:PollSeconds") ?? 5;
            _pollInterval = TimeSpan.FromSeconds(Math.Max(1, pollSeconds));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(_pollInterval);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var analyticsService = scope.ServiceProvider.GetRequiredService<IAnalyticsService>();
                    await analyticsService.ProcessQueuedReportsAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // Keep the loop alive across any single failed iteration.
                    _logger.LogError(ex, "Analytics report worker iteration failed");
                }

                try
                {
                    if (!await timer.WaitForNextTickAsync(stoppingToken))
                    {
                        break;
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }
}
