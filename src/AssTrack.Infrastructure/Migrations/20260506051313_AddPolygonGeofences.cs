using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssTrack.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPolygonGeofences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PolygonJson",
                table: "Geofences",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShapeType",
                table: "Geofences",
                type: "TEXT",
                maxLength: 32,
                nullable: false,
                defaultValue: "circle");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PolygonJson",
                table: "Geofences");

            migrationBuilder.DropColumn(
                name: "ShapeType",
                table: "Geofences");
        }
    }
}
