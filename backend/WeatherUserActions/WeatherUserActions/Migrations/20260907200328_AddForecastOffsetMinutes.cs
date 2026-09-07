using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WeatherUserActions.Migrations
{
    /// <inheritdoc />
    public partial class AddForecastOffsetMinutes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OffsetMinutes",
                table: "Forecasts",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OffsetMinutes",
                table: "Forecasts");
        }
    }
}
