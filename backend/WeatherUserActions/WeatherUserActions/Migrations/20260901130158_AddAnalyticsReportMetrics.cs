using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WeatherUserActions.Migrations
{
    /// <inheritdoc />
    public partial class AddAnalyticsReportMetrics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Cities",
                table: "AnalyticsReports");

            migrationBuilder.DropColumn(
                name: "Metrics",
                table: "AnalyticsReports");

            migrationBuilder.DropColumn(
                name: "Services",
                table: "AnalyticsReports");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "AnalyticsReports",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<Guid>(
                name: "BatchId",
                table: "AnalyticsReports",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<int>(
                name: "ServiceId",
                table: "AnalyticsReports",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "AnalyticsReportCityMetrics",
                columns: table => new
                {
                    AnalyticsReportCityMetricId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReportId = table.Column<int>(type: "int", nullable: false),
                    CityId = table.Column<int>(type: "int", nullable: false),
                    AvgTemperature = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: true),
                    StdDevTemperature = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: true),
                    MinTemperature = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: true),
                    MaxTemperature = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: true),
                    AvgHumidity = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: true),
                    StdDevHumidity = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: true),
                    MinHumidity = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: true),
                    MaxHumidity = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: true),
                    AvgWindSpeed = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: true),
                    StdDevWindSpeed = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: true),
                    MinWindSpeed = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: true),
                    MaxWindSpeed = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: true),
                    DangerDayCount = table.Column<int>(type: "int", nullable: false),
                    SampleCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalyticsReportCityMetrics", x => x.AnalyticsReportCityMetricId);
                    table.ForeignKey(
                        name: "FK_AnalyticsReportCityMetrics_AnalyticsReports_ReportId",
                        column: x => x.ReportId,
                        principalTable: "AnalyticsReports",
                        principalColumn: "ReportId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AnalyticsReportCityMetrics_Cities_CityId",
                        column: x => x.CityId,
                        principalTable: "Cities",
                        principalColumn: "CityId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnalyticsReports_BatchId",
                table: "AnalyticsReports",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_AnalyticsReports_ServiceId",
                table: "AnalyticsReports",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_AnalyticsReports_Status",
                table: "AnalyticsReports",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_AnalyticsReportCityMetrics_CityId",
                table: "AnalyticsReportCityMetrics",
                column: "CityId");

            migrationBuilder.CreateIndex(
                name: "IX_AnalyticsReportCityMetrics_ReportId_CityId",
                table: "AnalyticsReportCityMetrics",
                columns: new[] { "ReportId", "CityId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AnalyticsReports_ForecastingServices_ServiceId",
                table: "AnalyticsReports",
                column: "ServiceId",
                principalTable: "ForecastingServices",
                principalColumn: "ServiceId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AnalyticsReports_ForecastingServices_ServiceId",
                table: "AnalyticsReports");

            migrationBuilder.DropTable(
                name: "AnalyticsReportCityMetrics");

            migrationBuilder.DropIndex(
                name: "IX_AnalyticsReports_BatchId",
                table: "AnalyticsReports");

            migrationBuilder.DropIndex(
                name: "IX_AnalyticsReports_ServiceId",
                table: "AnalyticsReports");

            migrationBuilder.DropIndex(
                name: "IX_AnalyticsReports_Status",
                table: "AnalyticsReports");

            migrationBuilder.DropColumn(
                name: "BatchId",
                table: "AnalyticsReports");

            migrationBuilder.DropColumn(
                name: "ServiceId",
                table: "AnalyticsReports");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "AnalyticsReports",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AddColumn<string>(
                name: "Cities",
                table: "AnalyticsReports",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Metrics",
                table: "AnalyticsReports",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Services",
                table: "AnalyticsReports",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}
