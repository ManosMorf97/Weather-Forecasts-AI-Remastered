using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WeatherUserActions.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Cities",
                columns: table => new
                {
                    CityId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Country = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Latitude = table.Column<decimal>(type: "decimal(11,8)", precision: 11, scale: 8, nullable: false),
                    Longitude = table.Column<decimal>(type: "decimal(11,8)", precision: 11, scale: 8, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cities", x => x.CityId);
                    table.CheckConstraint("CK_Cities_Latitude", "[Latitude] >= -90 AND [Latitude] <= 90");
                    table.CheckConstraint("CK_Cities_Longitude", "[Longitude] >= -180 AND [Longitude] <= 180");
                });

            migrationBuilder.CreateTable(
                name: "ForecastingServices",
                columns: table => new
                {
                    ServiceId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ApiEndpoint = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ForecastingServices", x => x.ServiceId);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.UserId);
                });

            migrationBuilder.CreateTable(
                name: "CitySites",
                columns: table => new
                {
                    CitySiteId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CityId = table.Column<int>(type: "int", nullable: false),
                    ServiceId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CitySites", x => x.CitySiteId);
                    table.ForeignKey(
                        name: "FK_CitySites_Cities_CityId",
                        column: x => x.CityId,
                        principalTable: "Cities",
                        principalColumn: "CityId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CitySites_ForecastingServices_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "ForecastingServices",
                        principalColumn: "ServiceId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AnalyticsReports",
                columns: table => new
                {
                    ReportId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Cities = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Services = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateRangeStart = table.Column<DateOnly>(type: "date", nullable: false),
                    DateRangeEnd = table.Column<DateOnly>(type: "date", nullable: false),
                    Metrics = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Format = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalyticsReports", x => x.ReportId);
                    table.ForeignKey(
                        name: "FK_AnalyticsReports_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserServices",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ServiceId = table.Column<int>(type: "int", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserServices", x => new { x.UserId, x.ServiceId });
                    table.ForeignKey(
                        name: "FK_UserServices_ForecastingServices_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "ForecastingServices",
                        principalColumn: "ServiceId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserServices_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Forecasts",
                columns: table => new
                {
                    ForecastId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CitySiteId = table.Column<int>(type: "int", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Temperature = table.Column<decimal>(type: "decimal(3,1)", precision: 3, scale: 1, nullable: false),
                    Humidity = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    WindSpeed = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    DangerFlag = table.Column<bool>(type: "bit", nullable: false),
                    RetrievedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Forecasts", x => x.ForecastId);
                    table.CheckConstraint("CK_Forecasts_Humidity", "[Humidity] >= 0 AND [Humidity] <= 100");
                    table.CheckConstraint("CK_Forecasts_Temperature", "[Temperature] >= -90 AND [Temperature] <= 60");
                    table.CheckConstraint("CK_Forecasts_WindSpeed", "[WindSpeed] >= 0 AND [WindSpeed] <= 253");
                    table.ForeignKey(
                        name: "FK_Forecasts_CitySites_CitySiteId",
                        column: x => x.CitySiteId,
                        principalTable: "CitySites",
                        principalColumn: "CitySiteId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserCitySites",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CitySiteId = table.Column<int>(type: "int", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserCitySites", x => new { x.UserId, x.CitySiteId });
                    table.ForeignKey(
                        name: "FK_UserCitySites_CitySites_CitySiteId",
                        column: x => x.CitySiteId,
                        principalTable: "CitySites",
                        principalColumn: "CitySiteId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserCitySites_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    NotificationId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ForecastId = table.Column<int>(type: "int", nullable: false),
                    Channel = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.NotificationId);
                    table.ForeignKey(
                        name: "FK_Notifications_Forecasts_ForecastId",
                        column: x => x.ForecastId,
                        principalTable: "Forecasts",
                        principalColumn: "ForecastId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Notifications_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Ratings",
                columns: table => new
                {
                    RatingId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ForecastId = table.Column<int>(type: "int", nullable: false),
                    Value = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ratings", x => x.RatingId);
                    table.ForeignKey(
                        name: "FK_Ratings_Forecasts_ForecastId",
                        column: x => x.ForecastId,
                        principalTable: "Forecasts",
                        principalColumn: "ForecastId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Ratings_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnalyticsReports_UserId",
                table: "AnalyticsReports",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_CitySites_CityId_ServiceId",
                table: "CitySites",
                columns: new[] { "CityId", "ServiceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CitySites_ServiceId",
                table: "CitySites",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_Forecasts_CitySiteId_Timestamp_Type",
                table: "Forecasts",
                columns: new[] { "CitySiteId", "Timestamp", "Type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_ForecastId",
                table: "Notifications",
                column: "ForecastId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId",
                table: "Notifications",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Ratings_ForecastId",
                table: "Ratings",
                column: "ForecastId");

            migrationBuilder.CreateIndex(
                name: "IX_Ratings_UserId_ForecastId",
                table: "Ratings",
                columns: new[] { "UserId", "ForecastId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserCitySites_CitySiteId",
                table: "UserCitySites",
                column: "CitySiteId");

            migrationBuilder.CreateIndex(
                name: "IX_UserServices_ServiceId",
                table: "UserServices",
                column: "ServiceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnalyticsReports");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "Ratings");

            migrationBuilder.DropTable(
                name: "UserCitySites");

            migrationBuilder.DropTable(
                name: "UserServices");

            migrationBuilder.DropTable(
                name: "Forecasts");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "CitySites");

            migrationBuilder.DropTable(
                name: "Cities");

            migrationBuilder.DropTable(
                name: "ForecastingServices");
        }
    }
}
