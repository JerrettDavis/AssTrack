using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssTrack.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMaintenanceDiagnosticTriggers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DiagnosticSensorType",
                table: "MaintenanceSchedules",
                type: "TEXT",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DiagnosticTextContains",
                table: "MaintenanceSchedules",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceSchedules_DiagnosticSensorType",
                table: "MaintenanceSchedules",
                column: "DiagnosticSensorType");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MaintenanceSchedules_DiagnosticSensorType",
                table: "MaintenanceSchedules");

            migrationBuilder.DropColumn(
                name: "DiagnosticSensorType",
                table: "MaintenanceSchedules");

            migrationBuilder.DropColumn(
                name: "DiagnosticTextContains",
                table: "MaintenanceSchedules");
        }
    }
}
