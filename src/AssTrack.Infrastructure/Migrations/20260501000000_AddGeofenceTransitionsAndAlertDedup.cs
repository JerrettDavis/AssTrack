using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssTrack.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGeofenceTransitionsAndAlertDedup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EventType",
                table: "GeofenceBreaches",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "DeviceGeofenceStates",
                columns: table => new
                {
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    GeofenceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    IsInside = table.Column<bool>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceGeofenceStates", x => new { x.DeviceId, x.GeofenceId });
                    table.ForeignKey(
                        name: "FK_DeviceGeofenceStates_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DeviceGeofenceStates_Geofences_GeofenceId",
                        column: x => x.GeofenceId,
                        principalTable: "Geofences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceGeofenceStates_GeofenceId",
                table: "DeviceGeofenceStates",
                column: "GeofenceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeviceGeofenceStates");

            migrationBuilder.DropColumn(
                name: "EventType",
                table: "GeofenceBreaches");
        }
    }
}
