using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AabuReports.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class addfixdatetoreports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FixDate",
                table: "Reports",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FixDate",
                table: "Reports");
        }
    }
}
