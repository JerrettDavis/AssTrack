using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssTrack.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIngestHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastObservationAt",
                table: "DeviceGeofenceStates",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.DropIndex(
                name: "IX_Observations_DeviceId_ObservedAt",
                table: "Observations");

            migrationBuilder.CreateIndex(
                name: "IX_Observations_DeviceId_ObservedAt",
                table: "Observations",
                columns: new[] { "DeviceId", "ObservedAt" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Observations_DeviceId_ObservedAt",
                table: "Observations");

            migrationBuilder.DropColumn(
                name: "LastObservationAt",
                table: "DeviceGeofenceStates");

            migrationBuilder.CreateIndex(
                name: "IX_Observations_DeviceId_ObservedAt",
                table: "Observations",
                columns: new[] { "DeviceId", "ObservedAt" });
        }
    }
}
