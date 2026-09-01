using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WeatherUserActions.Migrations
{
    /// <inheritdoc />
    public partial class AddAnalyticsReportDeliveredAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeliveredAt",
                table: "AnalyticsReports",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnalyticsReports_DeliveredAt",
                table: "AnalyticsReports",
                column: "DeliveredAt",
                filter: "[DeliveredAt] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AnalyticsReports_DeliveredAt",
                table: "AnalyticsReports");

            migrationBuilder.DropColumn(
                name: "DeliveredAt",
                table: "AnalyticsReports");
        }
    }
}
