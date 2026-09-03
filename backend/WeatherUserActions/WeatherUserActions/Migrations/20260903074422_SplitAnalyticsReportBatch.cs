using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WeatherUserActions.Migrations
{
    /// <inheritdoc />
    public partial class SplitAnalyticsReportBatch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AnalyticsReports_Users_UserId",
                table: "AnalyticsReports");

            migrationBuilder.DropIndex(
                name: "IX_AnalyticsReports_DeliveredAt",
                table: "AnalyticsReports");

            migrationBuilder.DropIndex(
                name: "IX_AnalyticsReports_UserId",
                table: "AnalyticsReports");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "AnalyticsReports");

            migrationBuilder.DropColumn(
                name: "DateRangeEnd",
                table: "AnalyticsReports");

            migrationBuilder.DropColumn(
                name: "DateRangeStart",
                table: "AnalyticsReports");

            migrationBuilder.DropColumn(
                name: "DeliveredAt",
                table: "AnalyticsReports");

            migrationBuilder.DropColumn(
                name: "Format",
                table: "AnalyticsReports");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "AnalyticsReports");

            migrationBuilder.CreateTable(
                name: "AnalyticsReportBatches",
                columns: table => new
                {
                    BatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    DateRangeStart = table.Column<DateOnly>(type: "date", nullable: false),
                    DateRangeEnd = table.Column<DateOnly>(type: "date", nullable: false),
                    Format = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeliveredAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalyticsReportBatches", x => x.BatchId);
                    table.ForeignKey(
                        name: "FK_AnalyticsReportBatches_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnalyticsReportBatches_DeliveredAt",
                table: "AnalyticsReportBatches",
                column: "DeliveredAt",
                filter: "[DeliveredAt] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AnalyticsReportBatches_UserId",
                table: "AnalyticsReportBatches",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_AnalyticsReports_AnalyticsReportBatches_BatchId",
                table: "AnalyticsReports",
                column: "BatchId",
                principalTable: "AnalyticsReportBatches",
                principalColumn: "BatchId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AnalyticsReports_AnalyticsReportBatches_BatchId",
                table: "AnalyticsReports");

            migrationBuilder.DropTable(
                name: "AnalyticsReportBatches");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "AnalyticsReports",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateOnly>(
                name: "DateRangeEnd",
                table: "AnalyticsReports",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AddColumn<DateOnly>(
                name: "DateRangeStart",
                table: "AnalyticsReports",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeliveredAt",
                table: "AnalyticsReports",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Format",
                table: "AnalyticsReports",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "AnalyticsReports",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_AnalyticsReports_DeliveredAt",
                table: "AnalyticsReports",
                column: "DeliveredAt",
                filter: "[DeliveredAt] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AnalyticsReports_UserId",
                table: "AnalyticsReports",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_AnalyticsReports_Users_UserId",
                table: "AnalyticsReports",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
