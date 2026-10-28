using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace aabu_reports.infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class maketheCoordinatesunique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Locations_Lat_Lng",
                table: "Locations",
                columns: new[] { "Lat", "Lng" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Locations_Lat_Lng",
                table: "Locations");
        }
    }
}
