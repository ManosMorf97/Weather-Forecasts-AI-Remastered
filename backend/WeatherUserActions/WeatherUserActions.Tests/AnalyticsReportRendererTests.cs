using System.Text;
using WeatherUserActions.Analytics;
using WeatherUserActions.Dtos;
using Xunit;

namespace WeatherUserActions.Tests
{
    // UC8 Stage 2: the PDF renderer is pure (no DB/Docker) - these just check it produces a valid,
    // non-trivial PDF from the generated numbers, including the empty-city case.
    public class AnalyticsReportRendererTests
    {
        private static readonly DateOnly RangeStart = new(2026, 8, 1);
        private static readonly DateOnly RangeEnd = new(2026, 8, 31);

        [Fact]
        public void RenderPdf_WithPopulatedAndEmptyCities_ProducesValidPdf()
        {
            var renderer = new AnalyticsReportRenderer();

            var section = new AnalyticsReportSection("OpenWeather", RangeStart, RangeEnd,
            [
                new AnalyticsReportCityRow("Athens", new CityMetricResult(1,
                    18.4m, 3.1m, 12m, 25m,
                    52.0m, 8.0m, 40m, 66m,
                    9.5m, 2.2m, 4m, 14m,
                    DangerDayCount: 2, SampleCount: 30)),
                new AnalyticsReportCityRow("Thessaloniki", new CityMetricResult(2,
                    null, null, null, null,
                    null, null, null, null,
                    null, null, null, null,
                    DangerDayCount: 0, SampleCount: 0)),
            ]);

            var pdf = renderer.RenderPdf(Guid.NewGuid(), [section]);

            Assert.NotNull(pdf);
            Assert.True(pdf.Length > 1000, $"PDF unexpectedly small: {pdf.Length} bytes");
            Assert.Equal("%PDF-", Encoding.ASCII.GetString(pdf, 0, 5));
        }

        [Fact]
        public void RenderPdf_MultipleServiceSections_ProducesValidPdf()
        {
            var renderer = new AnalyticsReportRenderer();

            AnalyticsReportSection Section(string serviceName) => new(serviceName, RangeStart, RangeEnd,
            [
                new AnalyticsReportCityRow("Athens", new CityMetricResult(1,
                    20m, 0m, 20m, 20m, 50m, 0m, 50m, 50m, 10m, 0m, 10m, 10m,
                    DangerDayCount: 1, SampleCount: 5)),
            ]);

            var pdf = renderer.RenderPdf(Guid.NewGuid(), [Section("OpenWeather"), Section("WeatherAPI")]);

            Assert.Equal("%PDF-", Encoding.ASCII.GetString(pdf, 0, 5));
        }
    }
}
