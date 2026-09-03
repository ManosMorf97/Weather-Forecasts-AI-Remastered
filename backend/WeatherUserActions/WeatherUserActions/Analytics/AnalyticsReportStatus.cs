namespace WeatherUserActions.Analytics
{
    // Lifecycle of a single AnalyticsReport row (UC8). Stored as text in AnalyticsReport.Status.
    public static class AnalyticsReportStatus
    {
        public const string Queued = "Queued";
        public const string Completed = "Completed";
        public const string Failed = "Failed";
    }
}
