using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using WeatherUserActions.Dtos;

namespace WeatherUserActions.Analytics
{
    // UC8 Stage 2: renders bar charts (ScottPlot) into a PDF (QuestPDF) - one section per service,
    // one bar per city, error bars = standard deviation, plus a numbers table.
    public class AnalyticsReportRenderer : IAnalyticsReportRenderer
    {
        static AnalyticsReportRenderer()
        {
            // Free for use under the QuestPDF Community licence.
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public byte[] RenderPdf(Guid batchId, IReadOnlyList<AnalyticsReportSection> sections)
        {
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(30);
                    page.DefaultTextStyle(style => style.FontSize(10));

                    page.Header().Text("Weather analytics report").FontSize(18).SemiBold();

                    page.Content().PaddingVertical(10).Column(column =>
                    {
                        column.Spacing(18);
                        column.Item().Text($"Reference: {batchId}");

                        foreach (var section in sections)
                        {
                            column.Item().Text(
                                    $"{section.ServiceName}   ({section.DateRangeStart:yyyy-MM-dd} to {section.DateRangeEnd:yyyy-MM-dd})")
                                .FontSize(14).SemiBold();

                            column.Item().Image(BarChartPng(
                                $"Average temperature - {section.ServiceName}", "temperature",
                                section, metric => metric.AvgTemperature, metric => metric.StdDevTemperature));
                            column.Item().Image(BarChartPng(
                                $"Average humidity - {section.ServiceName}", "humidity",
                                section, metric => metric.AvgHumidity, metric => metric.StdDevHumidity));
                            column.Item().Image(BarChartPng(
                                $"Average wind speed - {section.ServiceName}", "wind speed",
                                section, metric => metric.AvgWindSpeed, metric => metric.StdDevWindSpeed));

                            column.Item().Element(element => NumbersTable(element, section));
                        }
                    });

                    page.Footer().AlignRight().Text($"Generated {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC");
                });
            }).GeneratePdf();
        }

        private static byte[] BarChartPng(
            string title,
            string yLabel,
            AnalyticsReportSection section,
            Func<CityMetricResult, decimal?> value,
            Func<CityMetricResult, decimal?> error)
        {
            ScottPlot.Plot plot = new();

            var bars = section.Cities.Select((row, index) => new ScottPlot.Bar
            {
                Position = index,
                Value = (double)(value(row.Metrics) ?? 0m),
                Error = (double)(error(row.Metrics) ?? 0m),
            }).ToList();

            plot.Add.Bars(bars);

            var ticks = section.Cities
                .Select((row, index) => new ScottPlot.Tick(index, row.CityName))
                .ToArray();
            plot.Axes.Bottom.TickGenerator = new ScottPlot.TickGenerators.NumericManual(ticks);

            plot.Title(title);
            plot.Axes.Left.Label.Text = yLabel;
            plot.HideGrid();
            plot.Axes.Margins(bottom: 0);

            return plot.GetImage(720, 380).GetImageBytes();
        }

        private static void NumbersTable(IContainer container, AnalyticsReportSection section)
        {
            container.Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(2);
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                });

                table.Header(header =>
                {
                    header.Cell().Text("City").SemiBold();
                    header.Cell().Text("Temp avg (SD)").SemiBold();
                    header.Cell().Text("Humidity avg (SD)").SemiBold();
                    header.Cell().Text("Wind avg (SD)").SemiBold();
                    header.Cell().Text("Danger days").SemiBold();
                    header.Cell().Text("Forecasts").SemiBold();
                });

                foreach (var row in section.Cities)
                {
                    var metric = row.Metrics;
                    table.Cell().Text(row.CityName);
                    table.Cell().Text(AvgWithSpread(metric.AvgTemperature, metric.StdDevTemperature));
                    table.Cell().Text(AvgWithSpread(metric.AvgHumidity, metric.StdDevHumidity));
                    table.Cell().Text(AvgWithSpread(metric.AvgWindSpeed, metric.StdDevWindSpeed));
                    table.Cell().Text(metric.DangerDayCount.ToString());
                    table.Cell().Text(metric.SampleCount.ToString());
                }
            });
        }

        private static string AvgWithSpread(decimal? average, decimal? stdDev) =>
            average is null ? "-" : $"{average:0.##} (SD {stdDev:0.##})";
    }
}
