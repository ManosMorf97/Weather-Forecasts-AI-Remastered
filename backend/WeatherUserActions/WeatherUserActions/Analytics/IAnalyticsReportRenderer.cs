using WeatherUserActions.Dtos;

namespace WeatherUserActions.Analytics
{
    public interface IAnalyticsReportRenderer
    {
        // UC8 Stage 2: builds the PDF (charts + tables) that gets attached to the user's email.
        byte[] RenderPdf(Guid batchId, IReadOnlyList<AnalyticsReportSection> sections);
    }
}
